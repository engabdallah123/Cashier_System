using ClosedXML.Excel;
using Inventory.Domain;
using Inventory.Domain.Catalog.Categories;
using Inventory.Domain.Catalog.Products.Entities;
using Inventory.Domain.Catalog.Units;
using Microsoft.AspNetCore.Http;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using System.IO.Compression;

namespace Inventory.Application.Catalog.Products.Commands.ImportProductsFromExcel
{
    internal sealed class ImportProductsFromExcelCommandHandler
        : ICommandHandler<ImportProductsFromExcelCommand, ProductImportResultDto>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IFileService _fileService;
        private readonly ICacheService _cacheService;

        public ImportProductsFromExcelCommandHandler(
            IInventoryUnitOfWork unitOfWork,
            IFileService fileService,
            ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _fileService = fileService;
            _cacheService = cacheService;
        }

        public async Task<Result<ProductImportResultDto>> Handle(
            ImportProductsFromExcelCommand request,
            CancellationToken cancellationToken)
        {
            var result = new ProductImportResultDto();

            if (request.FileBytes == null || request.FileBytes.Length == 0)
            {
                return Result<ProductImportResultDto>.Failure(
                    new Error("Excel.EmptyFile", "ملف الإكسيل فارغ أو غير صالح."));
            }

            // 1. Detect if file is a ZIP archive containing products.xlsx + images/ folder
            byte[] excelBytes = request.FileBytes;
            var zipImagesByBarcode = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

            if (IsZipFile(request.FileBytes))
            {
                try
                {
                    using var zipStream = new MemoryStream(request.FileBytes);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

                    // Find excel entry (*.xlsx or *.xls) inside ZIP archive
                    var excelEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));

                    if (excelEntry != null)
                    {
                        using var ms = new MemoryStream();
                        using var entryStream = excelEntry.Open();
                        await entryStream.CopyToAsync(ms, cancellationToken);
                        excelBytes = ms.ToArray();
                    }

                    // Index image files by barcode (filename without extension)
                    foreach (var entry in archive.Entries)
                    {
                        var ext = Path.GetExtension(entry.FullName).ToLowerInvariant();
                        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")
                        {
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(entry.FullName).Trim();
                            if (!string.IsNullOrWhiteSpace(fileNameWithoutExt) && !zipImagesByBarcode.ContainsKey(fileNameWithoutExt))
                            {
                                zipImagesByBarcode[fileNameWithoutExt] = entry;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    return Result<ProductImportResultDto>.Failure(
                        new Error("Zip.Invalid", $"فشل قراءة ملف الـ ZIP المضغوط: {ex.Message}"));
                }
            }

            // 2. Pre-fetch existing Categories, Units, and Products for high-speed Bulk operations
            var existingCategories = await _unitOfWork.CategoryRepository.GetAllAsync();
            var categoryMap = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            foreach (var cat in existingCategories)
            {
                if (!string.IsNullOrWhiteSpace(cat.NameAr) && !categoryMap.ContainsKey(cat.NameAr.Trim()))
                    categoryMap[cat.NameAr.Trim()] = cat;
                if (!string.IsNullOrWhiteSpace(cat.NameEn) && !categoryMap.ContainsKey(cat.NameEn.Trim()))
                    categoryMap[cat.NameEn.Trim()] = cat;
            }

            var existingUnits = await _unitOfWork.UnitRepository.GetAllAsync();
            var unitMap = new Dictionary<string, Unit>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in existingUnits)
            {
                if (!string.IsNullOrWhiteSpace(u.NameAr) && !unitMap.ContainsKey(u.NameAr.Trim()))
                    unitMap[u.NameAr.Trim()] = u;
                if (!string.IsNullOrWhiteSpace(u.NameEn) && !unitMap.ContainsKey(u.NameEn.Trim()))
                    unitMap[u.NameEn.Trim()] = u;
                if (!string.IsNullOrWhiteSpace(u.Symbol) && !unitMap.ContainsKey(u.Symbol.Trim()))
                    unitMap[u.Symbol.Trim()] = u;
            }

            var allExistingProducts = await _unitOfWork.ProductRepository.GetAllAsync(cancellationToken);
            var productMap = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in allExistingProducts)
            {
                if (!string.IsNullOrWhiteSpace(p.Barcode))
                {
                    productMap[p.Barcode.Trim()] = p;
                }
            }

            // 3. Open Excel Workbook
            using var excelStream = new MemoryStream(excelBytes);
            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                return Result<ProductImportResultDto>.Failure(
                    new Error("Excel.NoWorksheet", "لم يتم العثور على ورقة عمل في ملف الإكسيل."));
            }

            var range = worksheet.RangeUsed();
            if (range == null || range.RowCount() < 2)
            {
                return Result<ProductImportResultDto>.Failure(
                    new Error("Excel.NoDataRows", "لا تحتوي ورقة العمل على صفوف بيانات بعد الترويسة."));
            }

            // 4. Dynamic Column Header Detection (Row 1)
            var headerRow = worksheet.Row(1);
            int lastCellNum = Math.Max(20, headerRow.LastCellUsed()?.Address.ColumnNumber ?? 20);

            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int col = 1; col <= lastCellNum; col++)
            {
                var h = GetCellValue(headerRow.Cell(col)).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(h)) continue;

                if (!colMap.ContainsKey("barcode") && (h.Contains("باركود") || h.Contains("barcode"))) 
                    colMap["barcode"] = col;
                else if (!colMap.ContainsKey("nameEn") && (h.Contains("انجليز") || h.Contains("إنجليز") || h.Contains("english") || h.Contains("nameen"))) 
                    colMap["nameEn"] = col;
                else if (!colMap.ContainsKey("nameAr") && (h.Contains("عربي") || h.Contains("اسم") || h.Contains("namear") || h.Contains("productname") || h.Contains("product"))) 
                    colMap["nameAr"] = col;
                else if (!colMap.ContainsKey("category") && (h.Contains("تصنيف") || h.Contains("فئة") || h.Contains("فئه") || h.Contains("category"))) 
                    colMap["category"] = col;
                else if (!colMap.ContainsKey("parentUnit") && (h.Contains("كبرى") || h.Contains("تعبئة") || h.Contains("تعبئه") || h.Contains("parentunit") || (h.Contains("كرتون") && !h.Contains("معامل")))) 
                    colMap["parentUnit"] = col;
                else if (!colMap.ContainsKey("baseUnit") && (h.Contains("صغرى") || h.Contains("اساسية") || h.Contains("أساسية") || h.Contains("baseunit") || (h.Contains("وحدة") && !h.Contains("كبرى")))) 
                    colMap["baseUnit"] = col;
                else if (!colMap.ContainsKey("conversionFactor") && (h.Contains("معامل") || h.Contains("تحويل") || h.Contains("conversion") || h.Contains("factor"))) 
                    colMap["conversionFactor"] = col;
                else if (!colMap.ContainsKey("purchasePrice") && (h.Contains("شراء") || h.Contains("تكلفة") || h.Contains("تكلفه") || h.Contains("cost") || h.Contains("purchase"))) 
                    colMap["purchasePrice"] = col;
                else if (!colMap.ContainsKey("wholesalePrice") && (h.Contains("جملة") || h.Contains("جمله") || h.Contains("wholesale"))) 
                    colMap["wholesalePrice"] = col;
                else if (!colMap.ContainsKey("sellingPrice") && (h.Contains("قطاعي") || (h.Contains("بيع") && !h.Contains("جملة")) || h.Contains("selling") || h.Contains("price"))) 
                    colMap["sellingPrice"] = col;
                else if (!colMap.ContainsKey("reorderLevel") && (h.Contains("إعادة") || h.Contains("اعادة") || h.Contains("طلب") || h.Contains("reorder"))) 
                    colMap["reorderLevel"] = col;
                else if (!colMap.ContainsKey("maxStockLevel") && (h.Contains("أقصى") || h.Contains("اقصى") || h.Contains("max"))) 
                    colMap["maxStockLevel"] = col;
                else if (!colMap.ContainsKey("initialStock") && (h.Contains("أولي") || h.Contains("اولي") || h.Contains("رصيد") || h.Contains("مخزون") || h.Contains("stock") || h.Contains("qty") || h.Contains("quantity"))) 
                    colMap["initialStock"] = col;
                else if (!colMap.ContainsKey("taxRate") && (h.Contains("ضريب") || h.Contains("tax"))) 
                    colMap["taxRate"] = col;
                else if (!colMap.ContainsKey("isWeighable") && (h.Contains("وزن") || h.Contains("ميزان") || h.Contains("weigh"))) 
                    colMap["isWeighable"] = col;
                else if (!colMap.ContainsKey("expiryAlertDays") && (h.Contains("تنبيه") || h.Contains("alert"))) 
                    colMap["expiryAlertDays"] = col;
                else if (!colMap.ContainsKey("shelfLifeDays") && (h.Contains("مدة") || h.Contains("مده") || (h.Contains("صلاحية") && (h.Contains("يوم") || h.Contains("أيام") || h.Contains("ايام"))) || h.Contains("shelflife"))) 
                    colMap["shelfLifeDays"] = col;
                else if (!colMap.ContainsKey("trackExpiry") && (h.Contains("صلاحية") || h.Contains("صلاحيه") || h.Contains("انتهاء") || h.Contains("expiry") || h.Contains("track"))) 
                    colMap["trackExpiry"] = col;
                else if (!colMap.ContainsKey("imageUrl") && (h.Contains("صورة") || h.Contains("صوره") || h.Contains("image") || h.Contains("url") || h.Contains("رابط"))) 
                    colMap["imageUrl"] = col;
                else if (!colMap.ContainsKey("description") && (h.Contains("وصف") || h.Contains("description") || h.Contains("تفاصيل") || h.Contains("ملاحظات"))) 
                    colMap["description"] = col;
            }

            int GetCol(string key, int fallback) => colMap.TryGetValue(key, out int col) ? col : fallback;

            int lastRow = range.LastRow().RowNumber();
            result.TotalRows = lastRow - 1; // Exclude header

            var batchBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newProductsToAdd = new List<Product>();
            var newCategoriesToAdd = new List<Category>();
            var newUnitsToAdd = new List<Unit>();

            for (int rowNum = 2; rowNum <= lastRow; rowNum++)
            {
                var row = worksheet.Row(rowNum);

                string barcode = GetCellValue(row.Cell(GetCol("barcode", 1)));
                string nameAr = GetCellValue(row.Cell(GetCol("nameAr", 2)));
                string nameEn = GetCellValue(row.Cell(GetCol("nameEn", 3)));
                string categoryName = GetCellValue(row.Cell(GetCol("category", 4)));
                
                string baseUnit = colMap.ContainsKey("baseUnit") ? GetCellValue(row.Cell(colMap["baseUnit"])) : GetCellValue(row.Cell(5));
                if (string.IsNullOrWhiteSpace(baseUnit)) baseUnit = "قطعة";

                bool isWeighable = colMap.ContainsKey("isWeighable") ? ParseBool(GetCellValue(row.Cell(colMap["isWeighable"]))) : ParseBool(GetCellValue(row.Cell(15)));

                string parentUnit = colMap.ContainsKey("parentUnit") ? GetCellValue(row.Cell(colMap["parentUnit"])) : GetCellValue(row.Cell(6));
                if (string.IsNullOrWhiteSpace(parentUnit)) parentUnit = isWeighable ? null : "كرتونة";

                int conversionFactor = colMap.ContainsKey("conversionFactor")
                    ? (int)ParseDecimal(GetCellValue(row.Cell(colMap["conversionFactor"])), 1)
                    : (int)ParseDecimal(GetCellValue(row.Cell(7)), 1);
                if (conversionFactor < 1) conversionFactor = 1;

                decimal purchasePrice = colMap.ContainsKey("purchasePrice") ? ParseDecimal(GetCellValue(row.Cell(colMap["purchasePrice"])), 0) : ParseDecimal(GetCellValue(row.Cell(8)), 0);
                decimal sellingPrice = colMap.ContainsKey("sellingPrice") ? ParseDecimal(GetCellValue(row.Cell(colMap["sellingPrice"])), 0) : ParseDecimal(GetCellValue(row.Cell(9)), 0);
                decimal wholesalePrice = colMap.ContainsKey("wholesalePrice") ? ParseDecimal(GetCellValue(row.Cell(colMap["wholesalePrice"])), 0) : ParseDecimal(GetCellValue(row.Cell(10)), 0);
                decimal initialStock = colMap.ContainsKey("initialStock") ? ParseDecimal(GetCellValue(row.Cell(colMap["initialStock"])), 0) : ParseDecimal(GetCellValue(row.Cell(11)), 0);
                decimal reorderLevel = colMap.ContainsKey("reorderLevel") ? ParseDecimal(GetCellValue(row.Cell(colMap["reorderLevel"])), 5) : ParseDecimal(GetCellValue(row.Cell(12)), 5);
                decimal maxStockLevel = colMap.ContainsKey("maxStockLevel") ? ParseDecimal(GetCellValue(row.Cell(colMap["maxStockLevel"])), 100) : ParseDecimal(GetCellValue(row.Cell(13)), 100);
                decimal taxRate = colMap.ContainsKey("taxRate") ? ParseDecimal(GetCellValue(row.Cell(colMap["taxRate"])), 0) : ParseDecimal(GetCellValue(row.Cell(14)), 0);

                bool trackExpiry = colMap.ContainsKey("trackExpiry") ? ParseBool(GetCellValue(row.Cell(colMap["trackExpiry"]))) : ParseBool(GetCellValue(row.Cell(16)));
                int shelfLifeDays = colMap.ContainsKey("shelfLifeDays") ? (int)ParseDecimal(GetCellValue(row.Cell(colMap["shelfLifeDays"])), 0) : (int)ParseDecimal(GetCellValue(row.Cell(17)), 0);
                int expiryAlertDays = colMap.ContainsKey("expiryAlertDays") ? (int)ParseDecimal(GetCellValue(row.Cell(colMap["expiryAlertDays"])), 3) : (int)ParseDecimal(GetCellValue(row.Cell(18)), 3);

                string? rawImageUrl = colMap.ContainsKey("imageUrl") ? GetCellValue(row.Cell(colMap["imageUrl"]), checkHyperlink: true) : GetCellValue(row.Cell(19), checkHyperlink: true);
                string? description = colMap.ContainsKey("description") ? GetCellValue(row.Cell(colMap["description"])) : GetCellValue(row.Cell(20));

                // Fallback smart detection if header matching did not yield an image URL
                if (string.IsNullOrWhiteSpace(rawImageUrl))
                {
                    string col15 = GetCellValue(row.Cell(15), checkHyperlink: true);
                    string col16 = GetCellValue(row.Cell(16), checkHyperlink: true);
                    if (IsPossibleImageUrl(col15))
                    {
                        rawImageUrl = col15;
                        if (string.IsNullOrWhiteSpace(description)) description = col16;
                    }
                    else if (IsPossibleImageUrl(col16))
                    {
                        rawImageUrl = col16;
                        if (string.IsNullOrWhiteSpace(description)) description = col15;
                    }
                    else if (string.IsNullOrWhiteSpace(description))
                    {
                        description = !string.IsNullOrWhiteSpace(col15) ? col15 : col16;
                    }
                }

                // Basic Validations
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    result.ErrorCount++;
                    result.Errors.Add(new ProductImportErrorDto
                    {
                        RowNumber = rowNum,
                        Barcode = string.Empty,
                        ProductName = nameAr,
                        ErrorMessage = "البار كود مطلوب ولا يمكن أن يكون فارغاً."
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(nameAr))
                {
                    result.ErrorCount++;
                    result.Errors.Add(new ProductImportErrorDto
                    {
                        RowNumber = rowNum,
                        Barcode = barcode,
                        ProductName = string.Empty,
                        ErrorMessage = "اسم المنتج بالعربية مطلوب."
                    });
                    continue;
                }

                if (sellingPrice < 0)
                {
                    result.ErrorCount++;
                    result.Errors.Add(new ProductImportErrorDto
                    {
                        RowNumber = rowNum,
                        Barcode = barcode,
                        ProductName = nameAr,
                        ErrorMessage = "سعر البيع لا يمكن أن يكون بالسالب."
                    });
                    continue;
                }

                // Check duplicates within same Excel file
                if (!batchBarcodes.Add(barcode))
                {
                    result.ErrorCount++;
                    result.Errors.Add(new ProductImportErrorDto
                    {
                        RowNumber = rowNum,
                        Barcode = barcode,
                        ProductName = nameAr,
                        ErrorMessage = "الباركود مكرر أكثر من مرة في نفس ملف الإكسيل."
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(nameEn))
                {
                    nameEn = nameAr;
                }

                // 1. Resolve Category
                Guid categoryId;
                string catKey = string.IsNullOrWhiteSpace(categoryName) ? "عام" : categoryName.Trim();

                if (categoryMap.TryGetValue(catKey, out var category))
                {
                    categoryId = category.Id;
                }
                else
                {
                    var newCatResult = Category.Create(catKey, catKey);
                    if (newCatResult.IsFailure)
                    {
                        result.ErrorCount++;
                        result.Errors.Add(new ProductImportErrorDto
                        {
                            RowNumber = rowNum,
                            Barcode = barcode,
                            ProductName = nameAr,
                            ErrorMessage = $"فشل إنشاء التصنيف '{catKey}': {newCatResult.Error.Name}"
                        });
                        continue;
                    }

                    var newCat = newCatResult.Value!;
                    newCategoriesToAdd.Add(newCat);
                    categoryMap[catKey] = newCat;
                    categoryId = newCat.Id;
                }

                // 2. Resolve Unit
                Guid unitId;
                string uKey = string.IsNullOrWhiteSpace(baseUnit) ? "قطعة" : baseUnit.Trim();

                if (unitMap.TryGetValue(uKey, out var unit))
                {
                    unitId = unit.Id;
                }
                else
                {
                    var newUnitResult = Unit.Create(uKey, uKey, uKey);
                    if (newUnitResult.IsFailure)
                    {
                        result.ErrorCount++;
                        result.Errors.Add(new ProductImportErrorDto
                        {
                            RowNumber = rowNum,
                            Barcode = barcode,
                            ProductName = nameAr,
                            ErrorMessage = $"فشل إنشاء الوحدة '{uKey}': {newUnitResult.Error.Name}"
                        });
                        continue;
                    }

                    var newUnit = newUnitResult.Value!;
                    newUnitsToAdd.Add(newUnit);
                    unitMap[uKey] = newUnit;
                    unitId = newUnit.Id;
                }

                // 3. Resolve Image (ZIP matched by Barcode OR Text URL string directly)
                string? resolvedImageUrl = await ProcessProductImageAsync(barcode, rawImageUrl, zipImagesByBarcode, cancellationToken);

                // 4. Resolve Product in Database
                if (productMap.TryGetValue(barcode.Trim(), out var existingProduct))
                {
                    if (!request.UpdateExisting)
                    {
                        result.ErrorCount++;
                        result.Errors.Add(new ProductImportErrorDto
                        {
                            RowNumber = rowNum,
                            Barcode = barcode,
                            ProductName = nameAr,
                            ErrorMessage = $"المنتج بالباركود '{barcode}' موجود بالفعل في الكتالوج."
                        });
                        continue;
                    }

                    string? finalImageUrl = !string.IsNullOrWhiteSpace(resolvedImageUrl)
                        ? resolvedImageUrl
                        : existingProduct.ImageUrl;

                    var updateRes = existingProduct.Update(
                        barcode, nameAr, nameEn, description,
                        categoryId, unitId, null,
                        baseUnit, parentUnit, conversionFactor,
                        shelfLifeDays > 0 ? shelfLifeDays : existingProduct.ShelfLifeDays,
                        expiryAlertDays > 0 ? expiryAlertDays : existingProduct.ExpiryAlertDays,
                        purchasePrice, sellingPrice, wholesalePrice,
                        reorderLevel, maxStockLevel,
                        isWeighable, true, trackExpiry, taxRate,
                        finalImageUrl);

                    if (updateRes.IsFailure)
                    {
                        result.ErrorCount++;
                        result.Errors.Add(new ProductImportErrorDto
                        {
                            RowNumber = rowNum,
                            Barcode = barcode,
                            ProductName = nameAr,
                            ErrorMessage = updateRes.Error.Name
                        });
                        continue;
                    }

                    if (initialStock > 0)
                    {
                        decimal delta = initialStock - existingProduct.QuantityInStock;
                        if (delta != 0)
                        {
                            existingProduct.AdjustStock(delta, allowNegativeStock: true);
                        }
                    }

                    result.SuccessCount++;
                }
                else
                {
                    // Create New Product
                    var productRes = Product.Create(
                        barcode, nameAr, nameEn,
                        categoryId, unitId,
                        purchasePrice, sellingPrice, wholesalePrice,
                        null, description,
                        baseUnit, parentUnit, conversionFactor,
                        shelfLifeDays, expiryAlertDays,
                        reorderLevel, maxStockLevel,
                        isWeighable, true, trackExpiry, taxRate, resolvedImageUrl);

                    if (productRes.IsFailure)
                    {
                        result.ErrorCount++;
                        result.Errors.Add(new ProductImportErrorDto
                        {
                            RowNumber = rowNum,
                            Barcode = barcode,
                            ProductName = nameAr,
                            ErrorMessage = productRes.Error.Name
                        });
                        continue;
                    }

                    var newProd = productRes.Value!;
                    if (initialStock > 0)
                    {
                        newProd.AdjustStock(initialStock, allowNegativeStock: true);
                    }

                    newProductsToAdd.Add(newProd);
                    productMap[barcode.Trim()] = newProd;
                    result.SuccessCount++;
                }
            }

            // Bulk Insert Categories and Units if any missing ones were auto-created
            if (newCategoriesToAdd.Count > 0)
            {
                await _unitOfWork.CategoryRepository.AddRangeAsync(newCategoriesToAdd);
            }

            if (newUnitsToAdd.Count > 0)
            {
                await _unitOfWork.UnitRepository.AddRangeAsync(newUnitsToAdd);
            }

            if (newCategoriesToAdd.Count > 0 || newUnitsToAdd.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Chunked Insert Products for fast database performance
            const int BatchSize = 500;
            for (int i = 0; i < newProductsToAdd.Count; i += BatchSize)
            {
                var batch = newProductsToAdd.Skip(i).Take(BatchSize).ToList();
                await _unitOfWork.ProductRepository.AddRangeAsync(batch, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // If there were modified products that were not in newProductsToAdd, save any remaining tracked changes
            if (request.UpdateExisting)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _cacheService.RemoveByPrefixAsync("products_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("product_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result<ProductImportResultDto>.Success(result);
        }

        private async Task<string?> ProcessProductImageAsync(
            string barcode,
            string? rawUrl,
            Dictionary<string, ZipArchiveEntry> zipImages,
            CancellationToken ct)
        {
            // 1. First priority: Image in ZIP package matching product Barcode
            if (!string.IsNullOrWhiteSpace(barcode) && zipImages.TryGetValue(barcode.Trim(), out var zipEntry))
            {
                try
                {
                    using var ms = new MemoryStream();
                    using var entryStream = zipEntry.Open();
                    await entryStream.CopyToAsync(ms, ct);
                    var imageBytes = ms.ToArray();

                    if (imageBytes.Length > 0)
                    {
                        var ext = Path.GetExtension(zipEntry.FullName);
                        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                        var fileName = $"{barcode.Trim()}_{Guid.NewGuid():N}{ext}";
                        ms.Position = 0;
                        IFormFile formFile = new FormFile(ms, 0, imageBytes.Length, "file", fileName)
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = GetContentTypeFromExtension(ext)
                        };

                        var uploadResult = await _fileService.UploadFileAsync(formFile, "uploads/products");
                        if (uploadResult.IsSuccess)
                        {
                            return uploadResult.Value;
                        }
                    }
                }
                catch { }
            }

            // 2. Direct text URL string mode (save directly to DB)
            if (!string.IsNullOrWhiteSpace(rawUrl))
            {
                var trimmed = rawUrl.Trim();
                if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = "https://" + trimmed;
                }
                else if (trimmed.StartsWith("//", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = "https:" + trimmed;
                }
                return trimmed;
            }

            return null;
        }

        private static bool IsZipFile(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < 4) return false;
            return fileBytes[0] == 0x50 && fileBytes[1] == 0x4B && fileBytes[2] == 0x03 && fileBytes[3] == 0x04;
        }

        private static string GetContentTypeFromExtension(string ext)
        {
            ext = ext.ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
        }

        private static bool IsPossibleImageUrl(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            var v = val.Trim().ToLowerInvariant();
            return v.StartsWith("http://") ||
                   v.StartsWith("https://") ||
                   v.StartsWith("//") ||
                   v.StartsWith("www.") ||
                   v.EndsWith(".jpg") || v.EndsWith(".jpeg") || v.EndsWith(".png") || v.EndsWith(".webp") || v.EndsWith(".gif") || v.EndsWith(".avif") || v.EndsWith(".svg") ||
                   v.Contains(".jpg?") || v.Contains(".jpeg?") || v.Contains(".png?") || v.Contains(".webp?") ||
                   v.Contains("/images/") || v.Contains("/image/") || v.Contains("/img/");
        }

        private static string GetCellValue(IXLCell cell, bool checkHyperlink = false)
        {
            if (cell == null || cell.IsEmpty()) return string.Empty;

            // Direct string conversion first (instantaneous, avoids XML relation scans)
            var val = cell.Value;
            if (val.IsBlank) return string.Empty;

            var text = val.ToString()?.Trim() ?? string.Empty;

            if (checkHyperlink || string.IsNullOrEmpty(text))
            {
                try
                {
                    if (cell.HasHyperlink)
                    {
                        var hyperlink = cell.GetHyperlink();
                        if (hyperlink != null)
                        {
                            var address = hyperlink.ExternalAddress?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(address)) return address;

                            var href = hyperlink.InternalAddress?.Trim();
                            if (!string.IsNullOrWhiteSpace(href)) return href;
                        }
                    }
                }
                catch { }
            }

            return text;
        }

        private static decimal ParseDecimal(string val, decimal defaultValue)
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultValue;
            if (decimal.TryParse(val, out var result)) return result;
            return defaultValue;
        }

        private static bool ParseBool(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            val = val.Trim().ToLower();
            return val == "1" || val == "true" || val == "yes" || val == "نعم" || val == "صحيح";
        }
    }
}

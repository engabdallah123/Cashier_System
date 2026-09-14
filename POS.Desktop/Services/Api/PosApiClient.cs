using System.Net.Http;
using System.Net.Http.Json;

namespace POS.Desktop.Services.Api
{
    public class PosApiClient
    {
        private readonly HttpClient _http;

        public PosApiClient(HttpClient http)
        {
            _http = http;
        }

        public string GetImageFullUrl(string? relativeOrUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeOrUrl)) return string.Empty;

            var normalized = relativeOrUrl.Trim().Replace('\\', '/');

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var baseUri = _http.BaseAddress?.ToString() ?? "https://localhost:7198/";
            return new Uri(new Uri(baseUri), normalized.TrimStart('/')).ToString();
        }

        // Auth
        public async Task<bool> CheckInitialSetupRequiredAsync()
        {
            try
            {
                var res = await _http.GetFromJsonAsync<InitialSetupStatusResponse>("api/auth/initial-setup-required");
                return res?.SetupRequired ?? false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(AuthResponse? Auth, string? Error)> SetupInitialAdminAsync(SetupAdminRequest request)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/auth/setup-admin", request);
                if (res.IsSuccessStatusCode)
                {
                    var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
                    return (auth, null);
                }
                var err = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(err, "فشل إنشاء حساب المدير المسؤول."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/auth/login", request);
                return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<AuthResponse>() : null;
            }
            catch
            {
                return null;
            }
        }

        // Products
        public async Task<List<ProductDto>> GetProductsAsync(string? search = null)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(search) ? "api/inventory/products?pageSize=1000" : $"api/inventory/products?pageSize=1000&searchTerm={search}";
                return await _http.GetFromJsonAsync<List<ProductDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode)
        {
            try
            {
                return await _http.GetFromJsonAsync<ProductDto>($"api/inventory/products/barcode/{barcode}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<OnlineProductLookupResult?> LookupProductOnlineAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                client.DefaultRequestHeaders.Add("User-Agent", "CashierSystemPOS - WindowsDesktop - 1.0");
                var url = $"https://world.openfoodfacts.org/api/v2/product/{barcode.Trim()}.json";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var root = doc.RootElement;
                if (!root.TryGetProperty("status", out var statusProp) || statusProp.GetInt32() != 1) return null;

                if (!root.TryGetProperty("product", out var product)) return null;

                string? nameAr = null;
                string? nameEn = null;
                string? imageUrl = null;

                if (product.TryGetProperty("product_name_ar", out var nameArProp) && !string.IsNullOrWhiteSpace(nameArProp.GetString()))
                    nameAr = nameArProp.GetString();

                if (product.TryGetProperty("product_name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
                {
                    if (string.IsNullOrWhiteSpace(nameAr)) nameAr = nameProp.GetString();
                    nameEn = nameProp.GetString();
                }

                if (product.TryGetProperty("product_name_en", out var nameEnProp) && !string.IsNullOrWhiteSpace(nameEnProp.GetString()))
                    nameEn = nameEnProp.GetString();

                if (product.TryGetProperty("image_front_url", out var imgFront) && !string.IsNullOrWhiteSpace(imgFront.GetString()))
                    imageUrl = imgFront.GetString();
                else if (product.TryGetProperty("image_url", out var imgUrl) && !string.IsNullOrWhiteSpace(imgUrl.GetString()))
                    imageUrl = imgUrl.GetString();

                byte[]? imgBytes = null;
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    try
                    {
                        imgBytes = await client.GetByteArrayAsync(imageUrl);
                    }
                    catch { }
                }

                return new OnlineProductLookupResult(barcode, nameAr ?? "منتج مجلوب بالباركود", nameEn ?? nameAr ?? "Scanned Product", imageUrl, imgBytes);
            }
            catch
            {
                return null;
            }
        }

        public async Task<(ProductImportResultDto? Result, string? Error)> ImportProductsExcelAsync(byte[] fileBytes, string fileName, bool updateExisting)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var byteContent = new ByteArrayContent(fileBytes);
                byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                content.Add(byteContent, "file", fileName);

                var url = $"api/inventory/products/import-excel?updateExisting={updateExisting}";
                var response = await _http.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errText = await response.Content.ReadAsStringAsync();
                    return (null, string.IsNullOrWhiteSpace(errText) ? "فشل استيراد ملف الإكسيل." : errText);
                }

                var importResult = await response.Content.ReadFromJsonAsync<ProductImportResultDto>();
                return (importResult, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<byte[]?> DownloadProductExcelTemplateAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/inventory/products/excel-template");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<(Guid? ProductId, string? Error)> CreateProductAsync(CreateProductFormModel model, byte[]? imageBytes = null, string? fileName = null)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(model.Barcode ?? ""), nameof(model.Barcode));
                content.Add(new StringContent(model.NameAr ?? ""), nameof(model.NameAr));
                content.Add(new StringContent(model.NameEn ?? ""), nameof(model.NameEn));
                if (!string.IsNullOrWhiteSpace(model.Description))
                    content.Add(new StringContent(model.Description), nameof(model.Description));

                content.Add(new StringContent(model.CategoryId.ToString()), nameof(model.CategoryId));
                content.Add(new StringContent(model.UnitId.ToString()), nameof(model.UnitId));
                if (model.SupplierId.HasValue && model.SupplierId.Value != Guid.Empty)
                    content.Add(new StringContent(model.SupplierId.Value.ToString()), nameof(model.SupplierId));

                content.Add(new StringContent(model.PurchasePrice.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(model.PurchasePrice));
                content.Add(new StringContent(model.SellingPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(model.SellingPrice));
                content.Add(new StringContent(model.WholesalePrice.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(model.WholesalePrice));
                content.Add(new StringContent(model.BaseUnit ?? "قطعة"), nameof(model.BaseUnit));
                if (!string.IsNullOrWhiteSpace(model.ParentUnit))
                    content.Add(new StringContent(model.ParentUnit), nameof(model.ParentUnit));
                content.Add(new StringContent(model.ConversionFactor.ToString()), nameof(model.ConversionFactor));
                content.Add(new StringContent(model.ShelfLifeDays.ToString()), nameof(model.ShelfLifeDays));
                content.Add(new StringContent(model.ExpiryAlertDays.ToString()), nameof(model.ExpiryAlertDays));
                content.Add(new StringContent(model.ReorderLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(model.ReorderLevel));
                content.Add(new StringContent(model.MaxStockLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(model.MaxStockLevel));
                content.Add(new StringContent(model.TaxRate.ToString(System.Globalization.CultureInfo.InvariantCulture)), nameof(model.TaxRate));

                content.Add(new StringContent(model.IsWeighable.ToString()), nameof(model.IsWeighable));
                content.Add(new StringContent(model.IsActive.ToString()), nameof(model.IsActive));
                content.Add(new StringContent(model.TrackExpiry.ToString()), nameof(model.TrackExpiry));

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    var fileContent = new ByteArrayContent(imageBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    content.Add(fileContent, "ImageFile", fileName ?? "product.jpg");
                }

                var res = await _http.PostAsync("api/inventory/products", content);
                if (res.IsSuccessStatusCode)
                {
                    var id = await res.Content.ReadFromJsonAsync<Guid>();
                    return (id, null);
                }

                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (null, "انتهت صلاحية جلسة تسجيل الدخول (Session Expired). يرجى تسجيل الخروج والدخول مجدداً.");
                }

                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return (null, "عفواً، لا يملك حسابك الحالي الصلاحيات الكافية لإضافة منتج (مطلوب صلاحية مدير أو مشرف).");
                }

                var errContent = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(errContent, "فشل حفظ المنتج. الرجاء التحقق من المدخلات."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<(string? ImageUrl, string? Error)> UploadProductImageAsync(byte[] imageBytes, string fileName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(imageBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(fileContent, "file", fileName ?? "product.jpg");

                var res = await _http.PostAsync("api/inventory/products/upload-image", content);
                if (res.IsSuccessStatusCode)
                {
                    var doc = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    if (doc.TryGetProperty("imageUrl", out var prop))
                    {
                        return (prop.GetString(), null);
                    }
                    return (null, null);
                }

                var errContent = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(errContent, "فشل رفع الصورة."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> UpdateProductAsync(UpdateProductCommandModel model)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/inventory/products/{model.Id}", model);
                if (res.IsSuccessStatusCode) return (true, null);

                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "انتهت صلاحية جلسة تسجيل الدخول (Session Expired). يرجى تسجيل الخروج والدخول مجدداً.");
                }

                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return (false, "عفواً، لا يملك حسابك الحالي الصلاحيات الكافية لتعديل المنتج (مطلوب صلاحية مدير أو مشرف).");
                }

                var errContent = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(errContent, "فشل تحديث بيانات المنتج."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> DeleteProductAsync(Guid id)
        {
            try
            {
                var res = await _http.DeleteAsync($"api/inventory/products/{id}");
                if (res.IsSuccessStatusCode) return (true, null);

                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "انتهت صلاحية جلسة تسجيل الدخول (Session Expired). يرجى تسجيل الخروج والدخول مجدداً.");
                }

                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return (false, "عفواً، لا يملك حسابك الحالي صلاحية حذف المنتج (مطلوب صلاحية مدير).");
                }

                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حذف المنتج من النظام."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ToggleProductStatusAsync(Guid productId, bool activate)
        {
            try
            {
                var endpoint = activate ? $"api/inventory/products/{productId}/activate" : $"api/inventory/products/{productId}/deactivate";
                var res = await _http.PutAsync(endpoint, null);
                if (res.IsSuccessStatusCode) return (true, null);

                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "انتهت صلاحية جلسة تسجيل الدخول (Session Expired). يرجى تسجيل الخروج والدخول مجدداً.");
                }

                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return (false, "عفواً، لا يملك حسابك الحالي صلاحية تغيير حالة المنتج.");
                }

                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تغيير حالة المنتج."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Categories & Units
        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<CategoryDto>>("api/inventory/categories") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<Guid?> CreateCategoryAsync(CreateCategoryRequest request)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/inventory/categories", request);
                return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<Guid>() : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> UpdateCategoryAsync(UpdateCategoryRequest request)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/inventory/categories/{request.Id}", request);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string? Error)> ToggleCategoryStatusAsync(Guid categoryId, bool activate)
        {
            try
            {
                var endpoint = activate ? $"api/inventory/categories/{categoryId}/activate" : $"api/inventory/categories/{categoryId}/deactivate";
                var res = await _http.PutAsync(endpoint, null);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تغيير حالة التصنيف."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> DeleteCategoryAsync(Guid id)
        {
            try
            {
                var res = await _http.DeleteAsync($"api/inventory/categories/{id}");
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حذف التصنيف."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<List<UnitDto>> GetUnitsAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<UnitDto>>("api/inventory/units") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Customers & Suppliers
        public async Task<List<CustomerDto>> GetCustomersAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<CustomerDto>>("api/customers") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(Guid? Id, string? Error)> CreateCustomerAsync(CreateCustomerRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/customers", req);
                if (res.IsSuccessStatusCode)
                {
                    var id = await res.Content.ReadFromJsonAsync<Guid>();
                    return (id, null);
                }
                var err = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(err, "فشل إضافة العميل."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<List<SupplierDto>> GetSuppliersAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<SupplierDto>>("api/suppliers") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Sales & Shifts
        public async Task<(CreateSaleResult? Result, string? Error)> CreateSaleAsync(CreateSaleCommand command)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/sales", command);
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<CreateSaleResult>();
                    return (result, null);
                }

                var errContent = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(errContent, "Failed to create sale. Please check cashier shift or backend validation rules."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ReplenishBatchAsync(Guid batchId, decimal quantity, string? notes = null)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"api/batches/{batchId}/replenish", new ReplenishBatchRequest(quantity, notes));
                if (res.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var errContent = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(errContent, "فشل تجديد كمية الدفعة."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<ShiftDto?> GetCurrentShiftAsync(Guid cashierId)
        {
            try
            {
                var res = await _http.GetAsync($"api/shifts/current/{cashierId}");
                return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ShiftDto>() : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ShiftDto>> GetShiftsAsync(DateTime? fromDate = null, DateTime? toDate = null, Guid? cashierId = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-ddTHH:mm:ss}");
                if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-ddTHH:mm:ss}");
                if (cashierId.HasValue && cashierId.Value != Guid.Empty) queryParams.Add($"cashierId={cashierId.Value}");

                var url = "api/shifts";
                if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

                return await _http.GetFromJsonAsync<List<ShiftDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(Guid? ShiftId, string? Error)> OpenShiftAsync(OpenShiftCommand command)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/shifts/open", command);
                if (res.IsSuccessStatusCode)
                {
                    var id = await res.Content.ReadFromJsonAsync<Guid>();
                    return (id, null);
                }

                var errContent = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(errContent, "Failed to open shift. Cashier may already have an active open shift."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private static string ExtractErrorMessage(string errContent, string fallback)
        {
            if (string.IsNullOrWhiteSpace(errContent)) return fallback;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(errContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
                    return nameProp.GetString()!;

                if (root.TryGetProperty("Name", out var namePascal) && !string.IsNullOrWhiteSpace(namePascal.GetString()))
                    return namePascal.GetString()!;

                if (root.TryGetProperty("message", out var msgProp) && !string.IsNullOrWhiteSpace(msgProp.GetString()))
                    return msgProp.GetString()!;

                if (root.TryGetProperty("Message", out var msgPascal) && !string.IsNullOrWhiteSpace(msgPascal.GetString()))
                    return msgPascal.GetString()!;

                if (root.TryGetProperty("errors", out var errorsProp))
                {
                    var messages = new List<string>();
                    if (errorsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var err in errorsProp.EnumerateArray())
                        {
                            if (err.TryGetProperty("errorMessage", out var em) && !string.IsNullOrWhiteSpace(em.GetString()))
                                messages.Add(em.GetString()!);
                            else if (err.TryGetProperty("ErrorMessage", out var emP) && !string.IsNullOrWhiteSpace(emP.GetString()))
                                messages.Add(emP.GetString()!);
                        }
                    }
                    else if (errorsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var prop in errorsProp.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    var str = item.GetString();
                                    if (!string.IsNullOrWhiteSpace(str)) messages.Add(str);
                                }
                            }
                            else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var str = prop.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(str)) messages.Add(str);
                            }
                        }
                    }

                    if (messages.Any())
                        return string.Join(" | ", messages);
                }

                if (root.TryGetProperty("detail", out var detailProp) && !string.IsNullOrWhiteSpace(detailProp.GetString()))
                    return detailProp.GetString()!;

                if (root.TryGetProperty("Detail", out var detailPascal) && !string.IsNullOrWhiteSpace(detailPascal.GetString()))
                    return detailPascal.GetString()!;

                if (root.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
                    return titleProp.GetString()!;

                if (root.TryGetProperty("code", out var codeProp) && !string.IsNullOrWhiteSpace(codeProp.GetString()))
                    return codeProp.GetString()!;
            }
            catch
            {
            }
            return fallback;
        }

        public async Task<bool> CloseShiftAsync(CloseShiftCommand command)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"api/shifts/{command.ShiftId}/close", command);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Dashboard
        public async Task<DashboardDataDto?> GetDashboardAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-ddTHH:mm:ss}");
                if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-ddTHH:mm:ss}");

                var url = "api/dashboard";
                if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

                return await _http.GetFromJsonAsync<DashboardDataDto>(url);
            }
            catch
            {
                return null;
            }
        }

        // Monthly Sales Calendar & Report
        public async Task<MonthlySalesCalendarDto?> GetMonthlySalesCalendarAsync(int year, int month, string? paymentMethod = null, Guid? cashierId = null)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"year={year}",
                    $"month={month}"
                };

                if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "All")
                {
                    queryParams.Add($"paymentMethod={Uri.EscapeDataString(paymentMethod)}");
                }

                if (cashierId.HasValue && cashierId.Value != Guid.Empty)
                {
                    queryParams.Add($"cashierId={cashierId.Value}");
                }

                var url = "api/dashboard/monthly-calendar?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<MonthlySalesCalendarDto>(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching monthly calendar: {ex.Message}");
                return null;
            }
        }

        public async Task<DaySalesDetailsDto?> GetDaySalesDetailsAsync(DateTime date, string? paymentMethod = null, Guid? cashierId = null)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"date={date:yyyy-MM-dd}"
                };

                if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "All")
                {
                    queryParams.Add($"paymentMethod={Uri.EscapeDataString(paymentMethod)}");
                }

                if (cashierId.HasValue && cashierId.Value != Guid.Empty)
                {
                    queryParams.Add($"cashierId={cashierId.Value}");
                }

                var url = "api/dashboard/day-sales-details?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<DaySalesDetailsDto>(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching day sales details: {ex.Message}");
                return null;
            }
        }


        // Sales History
        public async Task<List<SaleDto>> GetSalesListAsync(DateTime? fromDate = null, DateTime? toDate = null, Guid? cashierId = null)
        {
            try
            {
                var queryParams = new List<string> { "pageSize=1000" };
                if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-ddTHH:mm:ss}");
                if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-ddTHH:mm:ss}");
                if (cashierId.HasValue && cashierId.Value != Guid.Empty) queryParams.Add($"cashierId={cashierId.Value}");

                var url = "api/sales?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<List<SaleDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Expenses
        public async Task<List<ExpenseDto>> GetExpensesListAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var queryParams = new List<string> { "pageSize=1000" };
                if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-ddTHH:mm:ss}");
                if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-ddTHH:mm:ss}");

                var url = "api/expenses?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<List<ExpenseDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool Success, string? Error)> CreateExpenseAsync(CreateExpenseRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/expenses", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل إضافة المصروف."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Purchases & Suppliers
        public async Task<List<PurchaseDto>> GetPurchasesListAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<PurchaseDto>>("api/purchases?pageSize=1000") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<List<ExpiringProductDto>> GetExpiringProductsAsync(int? daysThreshold = null)
        {
            try
            {
                var url = daysThreshold.HasValue ? $"api/purchases/expiring?daysThreshold={daysThreshold.Value}" : "api/purchases/expiring";
                return await _http.GetFromJsonAsync<List<ExpiringProductDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<PurchaseDetailDto?> GetPurchaseDetailsAsync(Guid purchaseId)
        {
            try
            {
                return await _http.GetFromJsonAsync<PurchaseDetailDto>($"api/purchases/{purchaseId}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<(bool Success, string? Error)> CreatePurchaseAsync(CreatePurchaseRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/purchases", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تسجيل فاتورة الشراء."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> UpdatePurchaseAsync(Guid purchaseId, UpdatePurchaseRequest req)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/purchases/{purchaseId}", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تعديل فاتورة الشراء."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> DeletePurchaseAsync(Guid purchaseId)
        {
            try
            {
                var res = await _http.DeleteAsync($"api/purchases/{purchaseId}");
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حذف فاتورة الشراء."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ReceivePurchaseAsync(Guid purchaseId, Guid userId)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"api/purchases/{purchaseId}/receive", userId);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل استلام فاتورة الشراء."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> PayPurchaseInvoiceAsync(Guid purchaseId, decimal amount)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"api/purchases/{purchaseId}/pay", amount);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل سداد المتبقي للفاتورة."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> CreateSupplierAsync(CreateSupplierRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/suppliers", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل إضافة المورد."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> UpdateSupplierAsync(Guid id, UpdateSupplierRequest req)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/suppliers/{id}", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تعديل بيانات المورد."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Store Settings
        public async Task<StoreSettingDto?> GetSettingsAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<StoreSettingDto>("api/settings");
            }
            catch
            {
                return null;
            }
        }

        public async Task<(bool Success, string? Error)> UpdateSettingsAsync(UpdateStoreSettingRequest req)
        {
            try
            {
                var res = await _http.PutAsJsonAsync("api/settings", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حفظ إعدادات المتجر."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(string? LogoUrl, string? Error)> UploadStoreLogoAsync(byte[] fileBytes, string fileName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "application/octet-stream"
                };
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                content.Add(fileContent, "file", fileName);

                var res = await _http.PostAsync("api/settings/logo", content);
                if (res.IsSuccessStatusCode)
                {
                    var doc = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    if (doc.TryGetProperty("logoUrl", out var logoProp))
                    {
                        return (logoProp.GetString(), null);
                    }
                    return (null, "لم يتم العثور على رابط الشعار.");
                }

                var err = await res.Content.ReadAsStringAsync();
                return (null, ExtractErrorMessage(err, "فشل رفع شعار المتجر."));
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // Audit Logs
        public async Task<List<AuditLogDto>> GetAuditLogsAsync(int page = 1, int pageSize = 50, string? entityName = null, string? action = null)
        {
            try
            {
                var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
                if (!string.IsNullOrWhiteSpace(entityName)) queryParams.Add($"entityName={Uri.EscapeDataString(entityName)}");
                if (!string.IsNullOrWhiteSpace(action)) queryParams.Add($"action={Uri.EscapeDataString(action)}");

                var url = "api/audit-logs?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<List<AuditLogDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Users & Roles Management
        public async Task<List<UserManagementDto>> GetUsersAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<UserManagementDto>>("api/users") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<List<RoleItemDto>> GetRolesAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<RoleItemDto>>("api/roles") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool Success, string? Error)> CreateUserAsync(CreateUserRequestModel req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/users", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل إنشاء حساب المستخدم."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> UpdateUserRoleAsync(string userId, string role)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/users/{userId}/role", new UpdateUserRoleRequestModel(role));
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تحديث صلاحية المستخدم."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ToggleUserStatusAsync(string userId, bool activate)
        {
            try
            {
                var endpoint = activate ? $"api/users/{userId}/activate" : $"api/users/{userId}/deactivate";
                var res = await _http.PutAsync(endpoint, null);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تغيير حالة المستخدم."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> DeleteUserAsync(string userId)
        {
            try
            {
                var res = await _http.DeleteAsync($"api/users/{userId}");
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حذف حساب المستخدم."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ResetUserPasswordAsync(string userId, string newPassword)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/users/{userId}/reset-password", new { NewPassword = newPassword });
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تعيين كلمة المرور الجديدة."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Single Invoices
        public async Task<SaleDto?> GetSaleByIdAsync(Guid id)
        {
            try
            {
                return await _http.GetFromJsonAsync<SaleDto>($"api/sales/{id}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<SaleDto?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return null;
            try
            {
                var encoded = Uri.EscapeDataString(invoiceNumber.Trim());
                return await _http.GetFromJsonAsync<SaleDto>($"api/sales/by-invoice/{encoded}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<PurchaseDto?> GetPurchaseByIdAsync(Guid id)
        {
            try
            {
                return await _http.GetFromJsonAsync<PurchaseDto>($"api/purchases/{id}");
            }
            catch
            {
                return null;
            }
        }

        // Sales Returns
        public async Task<List<SalesReturnDto>> GetSalesReturnsAsync(Guid? cashierId = null, Guid? shiftId = null, int page = 1, int pageSize = 100)
        {
            try
            {
                var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
                if (cashierId.HasValue && cashierId.Value != Guid.Empty) queryParams.Add($"cashierId={cashierId.Value}");
                if (shiftId.HasValue && shiftId.Value != Guid.Empty) queryParams.Add($"shiftId={shiftId.Value}");

                var url = "api/returns/sales?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<List<SalesReturnDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<SalesReturnDetailDto?> GetSalesReturnByIdAsync(Guid id)
        {
            try
            {
                return await _http.GetFromJsonAsync<SalesReturnDetailDto>($"api/returns/sales/{id}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<(bool Success, string? Error)> CreateSalesReturnAsync(CreateSalesReturnRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/returns/sales", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تسجيل مرتجع المبيعات."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> UpdateSalesReturnAsync(Guid id, UpdateSalesReturnRequest req)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/returns/sales/{id}", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تعديل مرتجع المبيعات."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> DeleteSalesReturnAsync(Guid id, Guid userId)
        {
            try
            {
                var res = await _http.DeleteAsync($"api/returns/sales/{id}?userId={userId}");
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حذف مرتجع المبيعات."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Purchase Returns
        public async Task<List<PurchaseReturnDto>> GetPurchaseReturnsAsync(Guid? supplierId = null, int page = 1, int pageSize = 100)
        {
            try
            {
                var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
                if (supplierId.HasValue && supplierId.Value != Guid.Empty) queryParams.Add($"supplierId={supplierId.Value}");

                var url = "api/returns/purchases?" + string.Join("&", queryParams);
                return await _http.GetFromJsonAsync<List<PurchaseReturnDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<PurchaseReturnDetailDto?> GetPurchaseReturnByIdAsync(Guid id)
        {
            try
            {
                return await _http.GetFromJsonAsync<PurchaseReturnDetailDto>($"api/returns/purchases/{id}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<(bool Success, string? Error)> CreatePurchaseReturnAsync(CreatePurchaseReturnRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/returns/purchases", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تسجيل مرتجع المشتريات."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> UpdatePurchaseReturnAsync(Guid id, UpdatePurchaseReturnRequest req)
        {
            try
            {
                var res = await _http.PutAsJsonAsync($"api/returns/purchases/{id}", req);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تعديل مرتجع المشتريات."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> DeletePurchaseReturnAsync(Guid id, Guid userId)
        {
            try
            {
                var res = await _http.DeleteAsync($"api/returns/purchases/{id}?userId={userId}");
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل حذف مرتجع المشتريات."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Debts
        public async Task<List<CustomerDebtDto>> GetCustomerDebtsAsync(string? search = null, Guid? customerId = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
                if (customerId.HasValue && customerId.Value != Guid.Empty) queryParams.Add($"customerId={customerId.Value}");

                var url = "api/debts/customers" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
                return await _http.GetFromJsonAsync<List<CustomerDebtDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool Success, string? Error)> PayCustomerDebtAsync(Guid saleId, decimal amount)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"api/sales/{saleId}/pay", amount);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تسجيل تحصيل دفعة من العميل."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<List<SupplierDebtDto>> GetSupplierDebtsAsync(string? search = null, Guid? supplierId = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
                if (supplierId.HasValue && supplierId.Value != Guid.Empty) queryParams.Add($"supplierId={supplierId.Value}");

                var url = "api/debts/suppliers" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
                return await _http.GetFromJsonAsync<List<SupplierDebtDto>>(url) ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool Success, string? Error)> PaySupplierDebtAsync(Guid purchaseId, decimal amount)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"api/purchases/{purchaseId}/pay", amount);
                if (res.IsSuccessStatusCode) return (true, null);
                var err = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(err, "فشل تسجيل سداد دفعة للمورد."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Backup
        public async Task<(byte[]? Data, string? FileName, string? Error)> ExportBackupAsync()
        {
            try
            {
                var res = await _http.GetAsync("api/backup/export");
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    return (null, null, ExtractErrorMessage(err, "فشل تصدير النسخة الاحتياطية."));
                }

                var bytes = await res.Content.ReadAsByteArrayAsync();
                var fileName = res.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"POS_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
                return (bytes, fileName, null);
            }
            catch (Exception ex)
            {
                return (null, null, ex.Message);
            }
        }

        public async Task<(bool Success, string? Message)> RestoreBackupAsync(string jsonContent)
        {
            try
            {
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                var res = await _http.PostAsync("api/backup/restore", content);
                var responseBody = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("message", out var msgProp) || doc.RootElement.TryGetProperty("Message", out msgProp))
                        {
                            return (true, msgProp.GetString() ?? "تم استرجاع النسخة الاحتياطية بنجاح.");
                        }
                    }
                    catch { }
                    return (true, "تم استرجاع النسخة الاحتياطية بنجاح وتحديث كافة البيانات.");
                }

                return (false, ExtractErrorMessage(responseBody, "فشلت عملية استرجاع النسخة الاحتياطية."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Batches
        public async Task<List<ProductBatchDto>> GetProductBatchesAsync(Guid productId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<ProductBatchDto>>($"api/inventory/batches/product/{productId}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Price Update
        public Task<(bool Success, string? Error)> ApplyProductPricesAsync(Guid productId, ApplyProductPricesRequest req)
            => ApplyProductPricesAsync(productId, req.CostPrice, req.SellingPrice, req.WholesalePrice);

        public async Task<(bool Success, string? Error)> ApplyProductPricesAsync(Guid productId, decimal costPrice, decimal sellingPrice, decimal wholesalePrice)
        {
            try
            {
                var req = new ApplyProductPricesRequest(costPrice, sellingPrice, wholesalePrice);
                var res = await _http.PutAsJsonAsync($"api/inventory/products/{productId}/prices", req);
                if (res.IsSuccessStatusCode) return (true, null);

                var body = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(body, "فشل تطبيق الأسعار الجديدة."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Notifications
        public async Task<List<ExpiryNotificationDto>> GetExpiryNotificationsAsync(bool includeSnoozed = false)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<ExpiryNotificationDto>>($"api/inventory/notifications/expiry?includeSnoozed={includeSnoozed}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool Success, string? Error)> ResolveNotificationAsync(Guid notificationId)
        {
            try
            {
                var res = await _http.PostAsync($"api/inventory/notifications/{notificationId}/resolve", null);
                if (res.IsSuccessStatusCode) return (true, null);

                var body = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(body, "فشل تأكيد مراجعة التنبيه."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> SnoozeNotificationAsync(Guid notificationId, int hours = 24)
        {
            try
            {
                var req = new SnoozeNotificationRequest(hours);
                var res = await _http.PostAsJsonAsync($"api/inventory/notifications/{notificationId}/snooze", req);
                if (res.IsSuccessStatusCode) return (true, null);

                var body = await res.Content.ReadAsStringAsync();
                return (false, ExtractErrorMessage(body, "فشل تأجيل الإشعار."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Waste
        public async Task<(bool Success, Guid? Id, string? Error)> RecordWasteAsync(RecordWasteRequest req)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("api/inventory/waste", req);
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var id))
                            return (true, id, null);
                    }
                    catch { }
                    return (true, null, null);
                }

                return (false, null, ExtractErrorMessage(body, "فشل تسجيل الهالك."));
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<WasteReportResponse?> GetWasteReportAsync(Guid? productId = null, string? reason = null, string? source = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (productId.HasValue) queryParams.Add($"productId={productId.Value}");
                if (!string.IsNullOrWhiteSpace(reason)) queryParams.Add($"reason={Uri.EscapeDataString(reason)}");
                if (!string.IsNullOrWhiteSpace(source)) queryParams.Add($"source={Uri.EscapeDataString(source)}");
                if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
                if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

                var url = "api/inventory/waste/report" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
                return await _http.GetFromJsonAsync<WasteReportResponse>(url);
            }
            catch
            {
                return null;
            }
        }
    }

    public record UserManagementDto(string Id, string FullName, string UserName, string Email, string Phone, bool IsActive, DateTime CreatedAt, string Role);
    public record RoleItemDto(string Id, string Name);
    public record CreateUserRequestModel(string FullName, string UserName, string Password, string Role, string? Email = null, string? Phone = null);
    public record UpdateUserRoleRequestModel(string Role);
    public record InitialSetupStatusResponse(bool SetupRequired);
    public record SetupAdminRequest(string FullName, string UserName, string? Email, string? Phone, string Password, string? StoreName = null);
}

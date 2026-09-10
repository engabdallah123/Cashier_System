using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Sales.Domain.Sales;

namespace Sales.Application.Sales.Queries.GetSaleReceipt
{
    internal sealed class GetSaleReceiptQueryHandler : IQueryHandler<GetSaleReceiptQuery, ReceiptResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly IFileService _fileService;

        public GetSaleReceiptQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            IFileService fileService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _fileService = fileService;
        }

        public async Task<Result<ReceiptResponse>> Handle(GetSaleReceiptQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            const string settingsSql = """
                SELECT TOP 1
                    StoreName, Address, Phone, Currency, InvoiceFooterMessage, LogoUrl
                FROM [Settings].[StoreSettings]
                """;

            var setting = await connection.QuerySingleOrDefaultAsync(settingsSql);
            var storeName = setting?.StoreName ?? "Supermarket POS";
            var address = setting?.Address;
            var phone = setting?.Phone;
            var currency = setting?.Currency ?? "EGP";
            var invoiceFooterMessage = setting?.InvoiceFooterMessage ?? "شكراً لزيارتكم!";
            var logoUrl = (string?)setting?.LogoUrl;

            byte[]? logoBytes = null;
            if (!string.IsNullOrWhiteSpace(logoUrl))
            {
                try
                {
                    var logoResult = await _fileService.GetFileAsByteArrayAsync(logoUrl);
                    if (logoResult.IsSuccess && logoResult.Value != null && logoResult.Value.Length > 0)
                    {
                        logoBytes = logoResult.Value;
                    }
                }
                catch
                {
                    // Fallback to text header if file read fails
                }
            }

            const string saleSql = """
                SELECT 
                    s.InvoiceNumber, s.SaleDate,
                    ISNULL(u.FullName, 'Cashier') AS CashierName,
                    c.Name AS CustomerName,
                    s.SubTotal, s.DiscountAmount, s.TaxAmount, s.TotalAmount,
                    s.PaidAmount, s.ChangeAmount, s.PaymentMethod,
                    s.Notes,
                    ISNULL((
                        SELECT COUNT(*) 
                        FROM [Sales].[Sales] s2 
                        WHERE s2.ShiftId = s.ShiftId 
                          AND (s2.SaleDate < s.SaleDate OR (s2.SaleDate = s.SaleDate AND s2.Id <= s.Id))
                    ), 1) AS OrderNumber
                FROM [Sales].[Sales] s
                LEFT JOIN [Identity].[AspNetUsers] u ON s.CashierId = CAST(u.Id AS uniqueidentifier)
                LEFT JOIN [Sales].[Customers] c ON s.CustomerId = c.Id
                WHERE s.Id = @SaleId
                """;

            var saleHeader = await connection.QuerySingleOrDefaultAsync(saleSql, new { request.SaleId });
            if (saleHeader is null)
                return Result<ReceiptResponse>.Failure(SaleErrors.NotFound(request.SaleId));

            const string itemsSql = """
                SELECT 
                    i.Id, i.ProductId, p.NameAr AS ProductName, p.Barcode,
                    i.Quantity, i.UnitPrice, i.Discount, i.Tax, i.Total,
                    p.BaseUnit, p.ParentUnit, ISNULL(p.ConversionFactor, 1) AS ConversionFactor,
                    ISNULL(p.SellingPrice, 0) AS SellingPrice, ISNULL(p.WholesalePrice, 0) AS WholesalePrice
                FROM [Sales].[SaleItems] i
                LEFT JOIN [Inventory].[Products] p ON i.ProductId = p.Id
                WHERE i.SaleId = @SaleId
                """;

            var rawItems = await connection.QueryAsync<dynamic>(itemsSql, new { request.SaleId });
            var items = new List<SaleItemResponse>();
            foreach (var r in rawItems)
            {
                decimal qty = (decimal)r.Quantity;
                decimal unitPrice = (decimal)r.UnitPrice;
                decimal sellingPrice = (decimal)r.SellingPrice;
                decimal wholesalePrice = (decimal)r.WholesalePrice;
                int factor = (int)r.ConversionFactor;
                string baseUnit = !string.IsNullOrWhiteSpace((string?)r.BaseUnit) ? (string)r.BaseUnit : "قطعة";
                string parentUnit = !string.IsNullOrWhiteSpace((string?)r.ParentUnit) ? (string)r.ParentUnit : "كرتونة";

                string priceType = (wholesalePrice > 0 && sellingPrice > wholesalePrice && unitPrice <= wholesalePrice) ? "جملة" : "قطاعي";
                string unitName;
                string packagingInfo;
                decimal displayedQuantity = qty;

                if (factor > 1 && qty >= factor && (qty % factor == 0))
                {
                    int cartons = (int)(qty / factor);
                    unitName = parentUnit;
                    packagingInfo = $"{cartons} {parentUnit} ({qty:G29} {baseUnit})";
                    displayedQuantity = cartons;
                }
                else
                {
                    unitName = baseUnit;
                    packagingInfo = $"{qty:G29} {baseUnit}";
                }

                items.Add(new SaleItemResponse(
                    (Guid)r.Id,
                    (Guid)r.ProductId,
                    (string?)r.ProductName,
                    (string?)r.Barcode,
                    displayedQuantity,
                    unitPrice,
                    (decimal)r.Discount,
                    (decimal)r.Tax,
                    (decimal)r.Total,
                    unitName,
                    priceType,
                    packagingInfo));
            }

            string? notes = (string?)saleHeader.Notes;
            string? orderType = (!string.IsNullOrWhiteSpace(notes) && notes != "POS Desktop Sale") ? notes : null;
            int orderNumber = (int)(saleHeader.OrderNumber ?? 1);
            if (orderNumber <= 0) orderNumber = 1;

            var receipt = new ReceiptResponse(
                storeName,
                address,
                phone,
                saleHeader.InvoiceNumber,
                saleHeader.SaleDate,
                saleHeader.CashierName,
                saleHeader.CustomerName,
                items,
                saleHeader.SubTotal,
                saleHeader.DiscountAmount,
                saleHeader.TaxAmount,
                saleHeader.TotalAmount,
                saleHeader.PaidAmount,
                saleHeader.ChangeAmount,
                saleHeader.PaymentMethod,
                currency,
                invoiceFooterMessage,
                logoUrl,
                logoBytes,
                orderType,
                orderNumber);

            return Result<ReceiptResponse>.Success(receipt);
        }
    }
}

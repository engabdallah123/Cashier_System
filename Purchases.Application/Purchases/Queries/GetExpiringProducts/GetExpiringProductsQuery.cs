using POS.Shared.Application.Messaging;

namespace Purchases.Application.Purchases.Queries.GetExpiringProducts
{
    public sealed record ExpiringProductResponse
    {
        public Guid BatchId { get; init; }
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = default!;
        public string Barcode { get; init; } = default!;
        public DateTime ExpiryDate { get; init; }
        public string? BatchNumber { get; init; }
        public decimal BatchOriginalQuantity { get; init; }
        public decimal BatchRemainingQuantity { get; init; }
        public decimal QuantityInStock { get; init; }
        public decimal TotalProductStock { get; init; }
        public decimal UnitCost { get; init; }
        public DateTime? PurchaseDate { get; init; }
        public string? InvoiceNumber { get; init; }
        public string? SupplierName { get; init; }
        public int DaysRemaining { get; init; }
        public bool IsDepleted { get; init; }
        public decimal WastedQuantity { get; init; }

        public ExpiringProductResponse() { }

        public ExpiringProductResponse(
            Guid batchId,
            Guid productId,
            string productName,
            string barcode,
            DateTime expiryDate,
            string? batchNumber,
            decimal batchOriginalQuantity,
            decimal batchRemainingQuantity,
            decimal quantityInStock,
            decimal totalProductStock,
            decimal unitCost,
            DateTime? purchaseDate,
            string? invoiceNumber,
            string? supplierName,
            int daysRemaining,
            bool isDepleted,
            decimal wastedQuantity)
        {
            BatchId = batchId;
            ProductId = productId;
            ProductName = productName;
            Barcode = barcode;
            ExpiryDate = expiryDate;
            BatchNumber = batchNumber;
            BatchOriginalQuantity = batchOriginalQuantity;
            BatchRemainingQuantity = batchRemainingQuantity;
            QuantityInStock = quantityInStock;
            TotalProductStock = totalProductStock;
            UnitCost = unitCost;
            PurchaseDate = purchaseDate;
            InvoiceNumber = invoiceNumber;
            SupplierName = supplierName;
            DaysRemaining = daysRemaining;
            IsDepleted = isDepleted;
            WastedQuantity = wastedQuantity;
        }
    }

    public sealed record GetExpiringProductsQuery(int? DaysThreshold = null) : IQuery<IReadOnlyList<ExpiringProductResponse>>;
}

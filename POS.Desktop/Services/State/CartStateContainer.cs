namespace POS.Desktop.Services.State
{
    public class CartItemModel
    {
        public Guid ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public bool IsWholesale { get; set; } = false;
        public decimal Discount { get; set; }
        public decimal TaxRate { get; set; }

        public string BaseUnit { get; set; } = "قطعة";
        public string? ParentUnit { get; set; } = "كرتونة";
        public int ConversionFactor { get; set; } = 1;
        public int PiecesPerBox { get; set; } = 1;
        public int BoxesPerCarton { get; set; } = 1;
        public string SelectedUnit { get; set; } = "Piece"; // "Piece", "Box", "Carton"
        public decimal UnitQuantity { get; set; } = 1;
        public bool IsWeighable { get; set; } = false;

        public int CartonPieces => (!IsWeighable && ConversionFactor > 1)
            ? ConversionFactor 
            : (!IsWeighable && BoxesPerCarton > 1 ? BoxesPerCarton : (!IsWeighable && PiecesPerBox * BoxesPerCarton > 1 ? PiecesPerBox * BoxesPerCarton : 1));

        public bool HasCarton => !IsWeighable && CartonPieces > 1;
        public bool HasPackagingUnits => !IsWeighable && (HasCarton || PiecesPerBox > 1);

        public decimal ActiveBaseUnitPrice => (IsWholesale && WholesalePrice > 0) ? WholesalePrice : RetailPrice;
        public decimal CartonUnitPrice => ActiveBaseUnitPrice * CartonPieces;

        public decimal LineSubtotal => Quantity * UnitPrice;
        public decimal LineTax => (LineSubtotal - Discount) * (TaxRate / 100m);
        public decimal LineTotal => LineSubtotal - Discount + LineTax;
    }

    public class CartStateContainer
    {
        public List<CartItemModel> Items { get; } = new();
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; } = "Walk-in Customer";
        public decimal OverallDiscount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";

        public event Action? OnCartChanged;

        public void AddOrIncrementWeighedProduct(
            Guid productId,
            string barcode,
            string name,
            decimal retailPrice,
            decimal wholesalePrice,
            decimal weightInKg,
            string baseUnit = "كجم",
            decimal taxRate = 0,
            bool initialWholesale = false)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing is not null)
            {
                existing.UnitQuantity += weightInKg;
                existing.Quantity = existing.UnitQuantity;
            }
            else
            {
                var activeUnitPrice = initialWholesale && wholesalePrice > 0 ? wholesalePrice : retailPrice;
                var item = new CartItemModel
                {
                    ProductId = productId,
                    Barcode = barcode,
                    ProductName = name,
                    RetailPrice = retailPrice,
                    WholesalePrice = wholesalePrice,
                    UnitPrice = activeUnitPrice,
                    UnitQuantity = weightInKg,
                    Quantity = weightInKg,
                    TaxRate = taxRate,
                    IsWholesale = initialWholesale,
                    IsWeighable = true,
                    PiecesPerBox = 1,
                    BoxesPerCarton = 1,
                    ConversionFactor = 1,
                    BaseUnit = string.IsNullOrWhiteSpace(baseUnit) ? "كجم" : baseUnit,
                    ParentUnit = null,
                    SelectedUnit = "Piece"
                };
                Items.Add(item);
            }
            NotifyStateChanged();
        }

        public void AddOrIncrementProduct(
            Guid productId,
            string barcode,
            string name,
            decimal retailPrice,
            decimal wholesalePrice,
            decimal taxRate = 0,
            int piecesPerBox = 1,
            int boxesPerCarton = 1,
            int conversionFactor = 1,
            string baseUnit = "قطعة",
            string parentUnit = "كرتونة",
            string initialUnit = "Piece",
            bool initialWholesale = false,
            bool isWeighable = false)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing is not null)
            {
                existing.UnitQuantity += 1;
                RecalculateBaseQuantity(existing);
            }
            else
            {
                int effectiveCarton = conversionFactor > 1 ? conversionFactor : (boxesPerCarton > 1 ? boxesPerCarton : 1);
                var activeUnitPrice = initialWholesale && wholesalePrice > 0 ? wholesalePrice : retailPrice;
                var item = new CartItemModel
                {
                    ProductId = productId,
                    Barcode = barcode,
                    ProductName = name,
                    RetailPrice = retailPrice,
                    WholesalePrice = wholesalePrice,
                    UnitPrice = activeUnitPrice,
                    UnitQuantity = 1,
                    Quantity = 1,
                    TaxRate = taxRate,
                    IsWholesale = initialWholesale,
                    IsWeighable = isWeighable,
                    PiecesPerBox = Math.Max(1, piecesPerBox),
                    BoxesPerCarton = Math.Max(1, effectiveCarton),
                    ConversionFactor = Math.Max(1, effectiveCarton),
                    BaseUnit = string.IsNullOrWhiteSpace(baseUnit) ? (isWeighable ? "كجم" : "قطعة") : baseUnit,
                    ParentUnit = string.IsNullOrWhiteSpace(parentUnit) ? (isWeighable ? null : "كرتونة") : parentUnit,
                    SelectedUnit = initialUnit
                };
                RecalculateBaseQuantity(item);
                Items.Add(item);
            }
            NotifyStateChanged();
        }

        public void ChangeItemUnit(Guid productId, string unit)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is not null)
            {
                item.SelectedUnit = unit;
                RecalculateBaseQuantity(item);
                NotifyStateChanged();
            }
        }

        public void SetCustomCartonSize(Guid productId, int cartonSize)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is not null && cartonSize > 0)
            {
                item.ConversionFactor = cartonSize;
                item.BoxesPerCarton = cartonSize;
                RecalculateBaseQuantity(item);
                NotifyStateChanged();
            }
        }

        public void ToggleWholesale(Guid productId, bool isWholesale)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is not null)
            {
                item.IsWholesale = isWholesale;
                if (isWholesale)
                {
                    item.UnitPrice = item.WholesalePrice > 0 ? item.WholesalePrice : item.RetailPrice;
                }
                else
                {
                    item.UnitPrice = item.RetailPrice;
                }
                NotifyStateChanged();
            }
        }

        public void UpdateQuantity(Guid productId, decimal quantity)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is not null)
            {
                if (quantity <= 0)
                {
                    Items.Remove(item);
                }
                else
                {
                    item.UnitQuantity = quantity;
                    RecalculateBaseQuantity(item);
                }
                NotifyStateChanged();
            }
        }

        private static void RecalculateBaseQuantity(CartItemModel item)
        {
            if (item.IsWeighable)
            {
                item.Quantity = item.UnitQuantity;
                return;
            }

            decimal multiplier = item.SelectedUnit switch
            {
                "Box" => item.PiecesPerBox > 1 ? item.PiecesPerBox : 1m,
                "Carton" => item.CartonPieces > 1 ? item.CartonPieces : 1m,
                _ => 1m
            };
            item.Quantity = item.UnitQuantity * multiplier;
        }

        public void RemoveItem(Guid productId)
        {
            Items.RemoveAll(i => i.ProductId == productId);
            NotifyStateChanged();
        }

        public void Clear()
        {
            Items.Clear();
            CustomerId = null;
            CustomerName = "Walk-in Customer";
            OverallDiscount = 0;
            PaymentMethod = "Cash";
            NotifyStateChanged();
        }

        public decimal SubTotal => Items.Sum(i => i.LineSubtotal);
        public decimal ItemDiscounts => Items.Sum(i => i.Discount);
        public decimal TotalDiscount => ItemDiscounts + OverallDiscount;
        public decimal TotalTax => Items.Sum(i => i.LineTax);
        public decimal GrandTotal => Math.Max(0, SubTotal - TotalDiscount + TotalTax);

        private void NotifyStateChanged() => OnCartChanged?.Invoke();
    }
}

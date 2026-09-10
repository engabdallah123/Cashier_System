namespace Inventory.Domain.Stock.StockMovements
{
    public enum StockMovementType
    {
        Sale = 1,
        Purchase = 2,
        Adjustment = 3,
        SaleReturn = 4,
        PurchaseReturn = 5,
        Damage = 6,
        Transfer = 7,
        Waste = 8
    }
}

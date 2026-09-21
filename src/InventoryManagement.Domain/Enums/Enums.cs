namespace InventoryManagement.Domain.Enums;

public enum PurchaseStatus { Draft, Received, Completed, Cancelled }
public enum SaleStatus { Draft, Completed, Cancelled }
public enum StockTransactionType 
{ 
    Purchase = 0, 
    Sale = 1, 
    SalesReturn = 2, 
    PurchaseReturn = 3, 
    LegacyAdjustmentIncrease = 4, 
    LegacyAdjustmentDecrease = 5, 
    OpeningBalance = 6,
    Damage = 7,
    Loss = 8,
    Found = 9,
    ManualAdjustment = 10
}

namespace InventoryManagement.Domain.Enums;

public enum PurchaseStatus { Draft, Received, Completed, Cancelled }
public enum SaleStatus { Draft, Completed, Cancelled }
public enum StockTransactionType { Purchase, Sale, SaleReturn, PurchaseReturn, AdjustmentIncrease, AdjustmentDecrease, OpeningBalance }

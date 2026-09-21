namespace InventoryManagement.Domain.Enums;

public enum Permission
{
    CreateSale,
    CompleteSale,
    CreatePurchase,
    CompletePurchase,
    ProcessSalesReturn,
    ProcessPurchaseReturn,
    CreateStockAdjustment,
    ManageProducts,
    ManageCustomers,
    ManageSuppliers,
    ViewReports,
    ManageUsers,
    CreateBackup,
    RestoreBackup,
    ManageSettings,
    ViewAuditLogs
}

using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IAuthenticationService _authService;

    public AuthorizationService(IAuthenticationService authService)
    {
        _authService = authService;
    }

    public bool IsAdministrator()
    {
        return _authService.CurrentUser?.Role?.RoleName == "Administrator";
    }

    public bool IsManagerOrHigher()
    {
        var role = _authService.CurrentUser?.Role?.RoleName;
        return role == "Administrator" || role == "Manager";
    }

    public bool IsStaffOrHigher()
    {
        var role = _authService.CurrentUser?.Role?.RoleName;
        return role == "Administrator" || role == "Manager" || role == "Staff";
    }

    private static readonly Dictionary<string, HashSet<InventoryManagement.Domain.Enums.Permission>> RolePermissions = new()
    {
        {
            "Administrator", new HashSet<InventoryManagement.Domain.Enums.Permission>
            {
                InventoryManagement.Domain.Enums.Permission.CreateSale,
                InventoryManagement.Domain.Enums.Permission.CompleteSale,
                InventoryManagement.Domain.Enums.Permission.CreatePurchase,
                InventoryManagement.Domain.Enums.Permission.CompletePurchase,
                InventoryManagement.Domain.Enums.Permission.ProcessSalesReturn,
                InventoryManagement.Domain.Enums.Permission.ProcessPurchaseReturn,
                InventoryManagement.Domain.Enums.Permission.CreateStockAdjustment,
                InventoryManagement.Domain.Enums.Permission.ManageProducts,
                InventoryManagement.Domain.Enums.Permission.ManageCustomers,
                InventoryManagement.Domain.Enums.Permission.ManageSuppliers,
                InventoryManagement.Domain.Enums.Permission.ViewReports,
                InventoryManagement.Domain.Enums.Permission.ManageUsers,
                InventoryManagement.Domain.Enums.Permission.CreateBackup,
                InventoryManagement.Domain.Enums.Permission.RestoreBackup,
                InventoryManagement.Domain.Enums.Permission.ManageSettings,
                InventoryManagement.Domain.Enums.Permission.ViewAuditLogs
            }
        },
        {
            "Manager", new HashSet<InventoryManagement.Domain.Enums.Permission>
            {
                InventoryManagement.Domain.Enums.Permission.CreateSale,
                InventoryManagement.Domain.Enums.Permission.CompleteSale,
                InventoryManagement.Domain.Enums.Permission.CreatePurchase,
                InventoryManagement.Domain.Enums.Permission.CompletePurchase,
                InventoryManagement.Domain.Enums.Permission.ProcessSalesReturn,
                InventoryManagement.Domain.Enums.Permission.ProcessPurchaseReturn,
                InventoryManagement.Domain.Enums.Permission.CreateStockAdjustment,
                InventoryManagement.Domain.Enums.Permission.ManageProducts,
                InventoryManagement.Domain.Enums.Permission.ManageCustomers,
                InventoryManagement.Domain.Enums.Permission.ManageSuppliers,
                InventoryManagement.Domain.Enums.Permission.ViewReports
            }
        },
        {
            "Staff", new HashSet<InventoryManagement.Domain.Enums.Permission>
            {
                // Based on existing intended behavior (only Administration/Audit restricted)
                InventoryManagement.Domain.Enums.Permission.CreateSale,
                InventoryManagement.Domain.Enums.Permission.CompleteSale,
                InventoryManagement.Domain.Enums.Permission.CreatePurchase,
                InventoryManagement.Domain.Enums.Permission.CompletePurchase,
                InventoryManagement.Domain.Enums.Permission.ProcessSalesReturn,
                InventoryManagement.Domain.Enums.Permission.ProcessPurchaseReturn,
                InventoryManagement.Domain.Enums.Permission.CreateStockAdjustment,
                InventoryManagement.Domain.Enums.Permission.ManageProducts,
                InventoryManagement.Domain.Enums.Permission.ManageCustomers,
                InventoryManagement.Domain.Enums.Permission.ManageSuppliers,
                InventoryManagement.Domain.Enums.Permission.ViewReports
            }
        }
    };

    public bool HasPermission(InventoryManagement.Domain.Enums.Permission permission)
    {
        var role = _authService.CurrentUser?.Role?.RoleName;
        if (string.IsNullOrEmpty(role) || !RolePermissions.ContainsKey(role)) return false;
        
        return RolePermissions[role].Contains(permission);
    }
}

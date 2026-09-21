namespace InventoryManagement.Application.Interfaces;

public interface IAuthorizationService
{
    bool IsAdministrator();
    bool IsManagerOrHigher();
    bool IsStaffOrHigher();
    bool HasPermission(InventoryManagement.Domain.Enums.Permission permission);
}

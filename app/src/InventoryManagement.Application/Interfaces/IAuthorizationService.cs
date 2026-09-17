namespace InventoryManagement.Application.Interfaces;

public interface IAuthorizationService
{
    bool IsAdministrator();
    bool IsManagerOrHigher();
    bool IsStaffOrHigher();
}

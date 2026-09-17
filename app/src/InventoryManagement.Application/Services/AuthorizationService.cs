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
}

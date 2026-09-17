using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IAuthenticationService
{
    Task<User?> AuthenticateAsync(string username, string password);
    User? CurrentUser { get; }
    void Logout();
}

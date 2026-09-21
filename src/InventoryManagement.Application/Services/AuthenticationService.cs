using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;


namespace InventoryManagement.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IInventoryDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public User? CurrentUser { get; private set; }

    public AuthenticationService(IInventoryDbContext context, IPasswordHasher passwordHasher, IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
        {
            await _auditLogService.LogIndependentActionAsync(0, "LoginFailed", "Users", "", $"Failed login attempt for username: {username}");
            return null;
        }

        if (_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            CurrentUser = user;
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _auditLogService.LogIndependentActionAsync(user.UserId, "Login", "Users", user.UserId.ToString(), "Successful login");
            return user;
        }

        await _auditLogService.LogIndependentActionAsync(user.UserId, "LoginFailed", "Users", user.UserId.ToString(), "Failed login attempt (invalid password)");
        return null;
    }

    public void Logout()
    {
        if (CurrentUser != null)
        {
            // Using fire-and-forget for logout logging since Logout is synchronous
            _ = _auditLogService.LogIndependentActionAsync(CurrentUser.UserId, "Logout", "Users", CurrentUser.UserId.ToString(), "User logged out");
            CurrentUser = null;
        }
    }
}


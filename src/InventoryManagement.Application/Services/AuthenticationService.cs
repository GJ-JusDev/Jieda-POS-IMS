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

    public User? CurrentUser { get; private set; }

    public AuthenticationService(IInventoryDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return null;

        if (_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            CurrentUser = user;
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return user;
        }

        return null;
    }

    public void Logout()
    {
        CurrentUser = null;
    }
}


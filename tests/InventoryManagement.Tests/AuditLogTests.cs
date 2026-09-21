using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Infrastructure.Data;
using InventoryManagement.Infrastructure.Services;
using Moq;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Tests;

public class AuditLogTests
{
    private InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new InventoryDbContext(options);
    }

    [Fact]
    public async Task AuditLogService_FilteringAndPagination_Works()
    {
        var db = CreateDbContext();
        var service = new AuditLogService(db);

        // Seed data
        db.Users.Add(new User { UserId = 1, Username = "test" });
        db.Users.Add(new User { UserId = 2, Username = "test2" });
        
        db.AuditLogs.Add(new AuditLog { UserId = 1, Action = "SaleCompleted", TableName = "Sales", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        db.AuditLogs.Add(new AuditLog { UserId = 1, Action = "Login", TableName = "Users", CreatedAt = DateTime.UtcNow.AddHours(-2) });
        db.AuditLogs.Add(new AuditLog { UserId = 2, Action = "SaleCompleted", TableName = "Sales", CreatedAt = DateTime.UtcNow.AddHours(-3) });
        db.AuditLogs.Add(new AuditLog { UserId = 2, Action = "Logout", TableName = "Users", CreatedAt = DateTime.UtcNow.AddHours(-4) });
        await db.SaveChangesAsync();

        // Test User Filter
        var logs = (await service.GetAuditLogsAsync(userId: 1)).ToList();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.Equal(1, l.UserId));

        // Test Action Filter
        logs = (await service.GetAuditLogsAsync(action: "SaleCompleted")).ToList();
        Assert.Equal(2, logs.Count);

        // Test Pagination
        logs = (await service.GetAuditLogsAsync(skip: 1, take: 2)).ToList();
        Assert.Equal(2, logs.Count);
        // Order is descending by CreatedAt, so it should be the 2nd and 3rd newest
        Assert.Equal("Login", logs[0].Action);
        Assert.Equal(2, logs[1].UserId);
    }

    [Fact]
    public async Task AuthenticationService_LogsLoginLogout()
    {
        var db = CreateDbContext();
        var auditService = new AuditLogService(db);
        var hasher = new PasswordHasher();
        
        var role = new Role { RoleId = 1, RoleName = "Staff" };
        db.Roles.Add(role);
        var user = new User { UserId = 1, Username = "test", PasswordHash = hasher.HashPassword("my_secret_pass"), IsActive = true, RoleId = 1 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var authService = new AuthenticationService(db, hasher, auditService);

        // Act - Failed Login
        await authService.AuthenticateAsync("test", "wrong");
        
        // Assert - Failed Login Audited
        Assert.Contains(db.AuditLogs, a => a.Action == "LoginFailed");

        // Act - Success Login
        await authService.AuthenticateAsync("test", "my_secret_pass");
        
        // Assert - Success Login Audited
        Assert.Contains(db.AuditLogs, a => a.Action == "Login" && a.UserId == 1);

        // Act - Logout
        authService.Logout();
        
        // Assert - Logout Audited
        Assert.Contains(db.AuditLogs, a => a.Action == "Logout" && a.UserId == 1);
        
        // Security check
        Assert.DoesNotContain(db.AuditLogs, a => a.Description != null && a.Description.Contains("my_secret_pass"));
    }

    [Fact]
    public async Task MasterDataService_AuditIsTransactional_AndLogsCorrectly()
    {
        var db = CreateDbContext();
        var auditService = new AuditLogService(db);
        
        var authMock = new Mock<IAuthenticationService>();
        authMock.Setup(a => a.CurrentUser).Returns(new User { UserId = 99 });

        var catService = new CategoryService(db, auditService, authMock.Object);

        // Act
        var cat = await catService.AddAsync(new Category { CategoryName = "TestCat" });

        // Assert
        Assert.Contains(db.AuditLogs, a => a.Action == "CategoryCreated" && a.UserId == 99 && a.RecordId == cat.CategoryId.ToString());

        await catService.UpdateAsync(cat);
        Assert.Contains(db.AuditLogs, a => a.Action == "CategoryUpdated" && a.RecordId == cat.CategoryId.ToString());

        await catService.DeactivateAsync(cat.CategoryId);
        Assert.Contains(db.AuditLogs, a => a.Action == "CategoryDeactivated" && a.RecordId == cat.CategoryId.ToString());
    }
}

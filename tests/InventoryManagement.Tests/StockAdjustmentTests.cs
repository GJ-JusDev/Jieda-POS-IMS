using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Infrastructure.Data;
using Moq;
using InventoryManagement.Application.Interfaces;
using System.Linq;

namespace InventoryManagement.Tests;

public class StockAdjustmentTests
{
    private InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new InventoryDbContext(options);
        
        // Add a test product
        context.Products.Add(new Product { ProductId = 1, ProductName = "Test Product", IsActive = true });
        context.Products.Add(new Product { ProductId = 2, ProductName = "Inactive Product", IsActive = false });
        
        // Add some initial stock
        context.StockTransactions.Add(new StockTransaction 
        { 
            ProductId = 1, 
            TransactionType = StockTransactionType.OpeningBalance, 
            Quantity = 20,
            TransactionDate = DateTime.UtcNow 
        });
        
        context.SaveChanges();
        return context;
    }

    private IAuthorizationService CreateAuthMock(bool hasPermission = true)
    {
        var mock = new Mock<IAuthorizationService>();
        mock.Setup(a => a.HasPermission(Permission.CreateStockAdjustment)).Returns(hasPermission);
        return mock.Object;
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_Damage_DecreasesStock()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await service.AddStockAdjustmentAsync(1, StockTransactionType.Damage, -3, "Damaged", 1);

        var currentStock = await service.GetCurrentStockAsync(1);
        Assert.Equal(17, currentStock);
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_Loss_DecreasesStock()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await service.AddStockAdjustmentAsync(1, StockTransactionType.Loss, -2, "Lost", 1);

        var currentStock = await service.GetCurrentStockAsync(1);
        Assert.Equal(18, currentStock);
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_Found_IncreasesStock()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await service.AddStockAdjustmentAsync(1, StockTransactionType.Found, 5, "Found", 1);

        var currentStock = await service.GetCurrentStockAsync(1);
        Assert.Equal(25, currentStock);
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_ManualAdjustmentPositive_IncreasesStock()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await service.AddStockAdjustmentAsync(1, StockTransactionType.ManualAdjustment, 10, "Manual positive", 1);

        var currentStock = await service.GetCurrentStockAsync(1);
        Assert.Equal(30, currentStock);
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_ManualAdjustmentNegative_DecreasesStock()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await service.AddStockAdjustmentAsync(1, StockTransactionType.ManualAdjustment, -10, "Manual negative", 1);

        var currentStock = await service.GetCurrentStockAsync(1);
        Assert.Equal(10, currentStock);
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_ZeroQuantity_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.AddStockAdjustmentAsync(1, StockTransactionType.ManualAdjustment, 0, "Zero", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_InvalidProduct_ThrowsException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<Exception>(() => 
            service.AddStockAdjustmentAsync(999, StockTransactionType.ManualAdjustment, 1, "Invalid", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_InactiveProduct_ThrowsException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<Exception>(() => 
            service.AddStockAdjustmentAsync(2, StockTransactionType.ManualAdjustment, 1, "Inactive", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_InvalidTransactionType_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.AddStockAdjustmentAsync(1, StockTransactionType.Sale, 1, "Invalid enum", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_InsufficientStock_ThrowsException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<Exception>(() => 
            service.AddStockAdjustmentAsync(1, StockTransactionType.Damage, -25, "Too much damage", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock(false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            service.AddStockAdjustmentAsync(1, StockTransactionType.ManualAdjustment, 1, "Unauthorized", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_DamageWithPositiveQty_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.AddStockAdjustmentAsync(1, StockTransactionType.Damage, 5, "Positive damage", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_FoundWithNegativeQty_ThrowsArgumentException()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.AddStockAdjustmentAsync(1, StockTransactionType.Found, -5, "Negative found", 1));
    }

    [Fact]
    public async Task AddStockAdjustmentAsync_CreatesAuditLogAndTransaction()
    {
        var db = CreateDbContext();
        var service = new InventoryService(db, CreateAuthMock());

        await service.AddStockAdjustmentAsync(1, StockTransactionType.Damage, -3, "Damaged during inspection", 99);

        var transaction = db.StockTransactions.OrderByDescending(t => t.TransactionDate).First();
        Assert.Equal(StockTransactionType.Damage, transaction.TransactionType);
        Assert.Equal(-3, transaction.Quantity);
        Assert.Equal("StockAdjustment", transaction.ReferenceType);
        Assert.Equal("Damaged during inspection", transaction.Notes);
        Assert.Equal(99, transaction.CreatedBy);
        Assert.Equal(transaction.StockTransactionId, transaction.ReferenceId);

        var auditLog = db.AuditLogs.Single(a => a.Action == "StockAdjustmentCreated");
        Assert.Equal(99, auditLog.UserId);
        Assert.Equal("StockTransactions", auditLog.TableName);
        Assert.Contains("Damage: Product Test Product, -3", auditLog.Description);
    }

    [Fact]
    public async Task LedgerCalculation_ComplexSequence_YieldsCorrectStock()
    {
        var db = CreateDbContext(); // Starts with Opening Balance +20
        var service = new InventoryService(db, CreateAuthMock());

        // Sale -5
        db.StockTransactions.Add(new StockTransaction { ProductId = 1, TransactionType = StockTransactionType.Sale, Quantity = -5, TransactionDate = DateTime.UtcNow });
        db.SaveChanges();

        // Damage -2
        await service.AddStockAdjustmentAsync(1, StockTransactionType.Damage, -2, "Damage", 1);
        
        // Found +1
        await service.AddStockAdjustmentAsync(1, StockTransactionType.Found, 1, "Found", 1);

        // ManualAdjustment -3
        await service.AddStockAdjustmentAsync(1, StockTransactionType.ManualAdjustment, -3, "Manual", 1);

        var currentStock = await service.GetCurrentStockAsync(1);
        // 20 - 5 - 2 + 1 - 3 = 11
        Assert.Equal(11, currentStock);
    }
}

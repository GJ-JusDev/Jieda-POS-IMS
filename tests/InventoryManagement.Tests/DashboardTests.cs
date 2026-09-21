using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Infrastructure.Data;
using Moq;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Tests;

public class DashboardTests
{
    private InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new InventoryDbContext(options);
    }

    private IAuthorizationService CreateAuthMock(bool hasPermission = true)
    {
        var mock = new Mock<IAuthorizationService>();
        mock.Setup(a => a.HasPermission(It.IsAny<Permission>())).Returns(hasPermission);
        return mock.Object;
    }

    [Fact]
    public async Task DashboardMetrics_CalculatesCorrectly()
    {
        var db = CreateDbContext();
        var authz = CreateAuthMock();
        var inventoryService = new InventoryService(db, authz);
        var dashboardService = new DashboardService(db, inventoryService, authz);

        db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Cat1" });
        db.Units.Add(new Unit { UnitId = 1, UnitName = "Unit1", Symbol = "U" });
        db.Products.Add(new Product { ProductId = 1, ProductName = "A", CostPrice = 10, ReorderLevel = 5, IsActive = true, CategoryId = 1, UnitId = 1 });
        db.Products.Add(new Product { ProductId = 2, ProductName = "B", CostPrice = 20, ReorderLevel = 5, IsActive = true, CategoryId = 1, UnitId = 1 });
        db.Products.Add(new Product { ProductId = 3, ProductName = "C", CostPrice = 30, ReorderLevel = 5, IsActive = true, CategoryId = 1, UnitId = 1 });
        db.Customers.Add(new Customer { CustomerId = 1, CustomerName = "C1", IsActive = true });
        db.Suppliers.Add(new Supplier { SupplierId = 1, SupplierName = "S1", IsActive = true });
        await db.SaveChangesAsync();

        // Add some stock
        // Prod 1: 10 in stock (Value: 100) -> In Stock
        db.StockTransactions.Add(new StockTransaction { ProductId = 1, Quantity = 10, TransactionType = StockTransactionType.OpeningBalance, TransactionDate = DateTime.UtcNow });
        
        // Prod 2: 3 in stock (Value: 60) -> Low Stock
        db.StockTransactions.Add(new StockTransaction { ProductId = 2, Quantity = 3, TransactionType = StockTransactionType.OpeningBalance, TransactionDate = DateTime.UtcNow });
        
        // Prod 3: 0 in stock (Value: 0) -> Out of Stock
        
        // Sales today
        var todayStartLocal = DateTime.Today;
        var todayStartUtc = todayStartLocal.ToUniversalTime();

        db.Sales.Add(new Sale { SaleId = 1, CustomerId = 1, TotalAmount = 150, Status = SaleStatus.Completed, SaleDate = todayStartUtc.AddHours(2) });
        db.Sales.Add(new Sale { SaleId = 2, CustomerId = 1, TotalAmount = 50, Status = SaleStatus.Draft, SaleDate = todayStartUtc.AddHours(3) }); // not completed
        db.Sales.Add(new Sale { SaleId = 3, CustomerId = 1, TotalAmount = 200, Status = SaleStatus.Completed, SaleDate = todayStartUtc.AddDays(-2) }); // old

        // Purchases today
        db.Purchases.Add(new Purchase { PurchaseId = 1, SupplierId = 1, TotalAmount = 300, Status = PurchaseStatus.Completed, PurchaseDate = todayStartUtc.AddHours(4) });
        db.Purchases.Add(new Purchase { PurchaseId = 2, SupplierId = 1, TotalAmount = 100, Status = PurchaseStatus.Draft, PurchaseDate = todayStartUtc.AddHours(5) }); // draft

        await db.SaveChangesAsync();

        // Act
        var metrics = await dashboardService.GetDashboardMetricsAsync();

        // Assert
        Assert.Equal(160m, metrics.TotalInventoryValue); // 100 + 60 + 0
        Assert.Equal(1, metrics.LowStockCount); // Prod 2
        Assert.Equal(1, metrics.OutOfStockCount); // Prod 3
        Assert.Equal(150m, metrics.TodaySales);
        Assert.Equal(300m, metrics.TodayPurchases);
        Assert.Equal(3, metrics.TotalActiveProducts);
        Assert.Equal(1, metrics.TotalActiveCustomers);
        Assert.Equal(1, metrics.TotalActiveSuppliers);
        
        // Ensure recent movements sorted correctly
        Assert.Equal(2, metrics.RecentMovements.Count()); // Only Opening balances added
        Assert.Contains(metrics.RecentMovements, m => m.ProductName == "A" && m.Quantity == 10);
        Assert.Contains(metrics.RecentMovements, m => m.ProductName == "B" && m.Quantity == 3);
    }
}

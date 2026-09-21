using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using InventoryManagement.Application.Services;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Infrastructure.Data;

namespace InventoryManagement.Tests
{
    public class InventoryCalculationTests
    {
        private InventoryDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new InventoryDbContext(options);
        }

        [Fact]
        public async Task GetCurrentStockAsync_CalculatesStockCorrectly()
        {
            // Arrange
            var dbContext = GetDbContext();
            var authzMock = new Moq.Mock<InventoryManagement.Application.Interfaces.IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Moq.It.IsAny<InventoryManagement.Domain.Enums.Permission>())).Returns(true);
            var service = new InventoryService(dbContext, authzMock.Object);
            int productId = 1;

            dbContext.Products.Add(new Product { ProductId = productId, ProductName = "Test Product", CostPrice = 10 });
            await dbContext.SaveChangesAsync();

            // Opening Balance +10
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.OpeningBalance, Quantity = 10 });
            // Purchase +10
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Purchase, Quantity = 10 });
            // Sale -3
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Sale, Quantity = -3 });
            // Sales Return +1
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.SalesReturn, Quantity = 1 });
            // Purchase Return -1
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.PurchaseReturn, Quantity = -1 });
            // Damage -1
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Damage, Quantity = -1 });
            // Loss -1
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Loss, Quantity = -1 });
            // Found +1
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Found, Quantity = 1 });
            // Manual Adjustment +5
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.ManualAdjustment, Quantity = 5 });
            // Manual Adjustment -5
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.ManualAdjustment, Quantity = -5 });
            
            await dbContext.SaveChangesAsync();

            // Expected: 10 + 10 - 3 + 1 - 1 - 1 - 1 + 1 + 5 - 5 = 16

            // Act
            var currentStock = await service.GetCurrentStockAsync(productId);

            // Assert
            Assert.Equal(16m, currentStock);
        }

        [Fact]
        public async Task CompleteInventorySequence_CalculatesExpectedFinalStock()
        {
            // Arrange
            var dbContext = GetDbContext();
            var authzMock = new Moq.Mock<InventoryManagement.Application.Interfaces.IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Moq.It.IsAny<InventoryManagement.Domain.Enums.Permission>())).Returns(true);
            var service = new InventoryService(dbContext, authzMock.Object);
            int productId = 2;

            dbContext.Products.Add(new Product { ProductId = productId, ProductName = "Test Sequence", CostPrice = 20 });
            await dbContext.SaveChangesAsync();

            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.OpeningBalance, Quantity = 100 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Purchase, Quantity = 50 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Sale, Quantity = -20 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.SalesReturn, Quantity = 5 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.PurchaseReturn, Quantity = -10 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Damage, Quantity = -3 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Loss, Quantity = -2 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.Found, Quantity = 1 });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = productId, TransactionType = StockTransactionType.ManualAdjustment, Quantity = 4 });
            
            await dbContext.SaveChangesAsync();

            // Expected: 100 + 50 - 20 + 5 - 10 - 3 - 2 + 1 + 4 = 125

            // Act
            var currentStock = await service.GetCurrentStockAsync(productId);

            // Assert
            Assert.Equal(125m, currentStock);
        }
        
        [Theory]
        [InlineData(0, 10, "Out of Stock")]
        [InlineData(-5, 10, "Out of Stock")]
        [InlineData(5, 10, "Low Stock")]
        [InlineData(10, 10, "Low Stock")]
        [InlineData(11, 10, "In Stock")]
        [InlineData(100, 10, "In Stock")]
        public void GetStockStatus_ReturnsExpectedStatus(decimal currentStock, decimal reorderLevel, string expectedStatus)
        {
            var dbContext = GetDbContext();
            var authzMock = new Moq.Mock<InventoryManagement.Application.Interfaces.IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Moq.It.IsAny<InventoryManagement.Domain.Enums.Permission>())).Returns(true);
            var service = new InventoryService(dbContext, authzMock.Object);
            var status = service.GetStockStatus(currentStock, reorderLevel);
            Assert.Equal(expectedStatus, status);
        }

        [Fact]
        public async Task GetTotalInventoryValueAsync_CalculatesValueBasedOnCostPrice()
        {
            var dbContext = GetDbContext();
            var authzMock = new Moq.Mock<InventoryManagement.Application.Interfaces.IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Moq.It.IsAny<InventoryManagement.Domain.Enums.Permission>())).Returns(true);
            var service = new InventoryService(dbContext, authzMock.Object);
            // Setup Category and Unit for INNER JOIN
            dbContext.Categories.Add(new Category { CategoryId = 1, CategoryName = "Test Cat" });
            dbContext.Units.Add(new Unit { UnitId = 1, UnitName = "pcs" });

            // Product 1: Stock 10, Cost 5 => 50
            dbContext.Products.Add(new Product { ProductId = 1, SKU = "P1", ProductName = "P1", CategoryId = 1, UnitId = 1, CostPrice = 5, IsActive = true });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = 1, TransactionType = StockTransactionType.OpeningBalance, Quantity = 10 });
            
            // Product 2: Stock 0, Cost 10 => 0
            dbContext.Products.Add(new Product { ProductId = 2, SKU = "P2", ProductName = "P2", CategoryId = 1, UnitId = 1, CostPrice = 10, IsActive = true });
            
            // Product 3: Stock 2, Cost 20 => 40
            dbContext.Products.Add(new Product { ProductId = 3, SKU = "P3", ProductName = "P3", CategoryId = 1, UnitId = 1, CostPrice = 20, IsActive = true });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = 3, TransactionType = StockTransactionType.OpeningBalance, Quantity = 2 });
            
            // Product 4: Stock -5 (Negative), Cost 10 => 0 (Inventory value shouldn't be negative)
            dbContext.Products.Add(new Product { ProductId = 4, SKU = "P4", ProductName = "P4", CategoryId = 1, UnitId = 1, CostPrice = 10, IsActive = true });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = 4, TransactionType = StockTransactionType.Sale, Quantity = -5 });

            await dbContext.SaveChangesAsync();

            var totalValue = await service.GetTotalInventoryValueAsync();
            
            Assert.Equal(90m, totalValue);
        }
    }
}

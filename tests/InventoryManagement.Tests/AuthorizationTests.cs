using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Infrastructure.Data;

namespace InventoryManagement.Tests
{
    public class AuthorizationTests
    {
        private InventoryDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new InventoryDbContext(options);
        }

        [Fact]
        public async Task Staff_Without_CompleteSale_Permission_Throws_UnauthorizedAccessException()
        {
            // Arrange
            var dbContext = GetDbContext();
            
            var authzMock = new Mock<IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Permission.CompleteSale)).Returns(false); // Staff denied

            var saleService = new SaleService(dbContext, authzMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                saleService.CreateSaleAsync(1, 1, new List<SaleItem>(), "Cash", "Notes"));
        }

        [Fact]
        public async Task Admin_With_CompleteSale_Permission_Succeeds()
        {
            // Arrange
            var dbContext = GetDbContext();
            dbContext.Products.Add(new Product { ProductId = 1, CostPrice = 10, SellingPrice = 20, ProductName = "Test", IsActive = true });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = 1, Quantity = 10, TransactionType = StockTransactionType.OpeningBalance });
            await dbContext.SaveChangesAsync();

            var authzMock = new Mock<IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Permission.CompleteSale)).Returns(true);

            var saleService = new SaleService(dbContext, authzMock.Object);
            var items = new List<SaleItem> { new SaleItem { ProductId = 1, Quantity = 1 } };

            // Act
            var sale = await saleService.CreateSaleAsync(1, 1, items, "Cash", "Notes");

            // Assert
            Assert.NotNull(sale);
            Assert.Single(sale.SaleItems);
        }

        [Fact]
        public async Task PurchaseService_CreateDraft_Enforces_Permission()
        {
            var dbContext = GetDbContext();
            var authzMock = new Mock<IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Permission.CreatePurchase)).Returns(false);

            var purchaseService = new PurchaseService(dbContext, authzMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                purchaseService.CreateDraftPurchaseAsync(1, 1, "Notes"));
        }

        [Fact]
        public async Task ReturnService_ProcessSalesReturn_Enforces_Permission()
        {
            var dbContext = GetDbContext();
            var authzMock = new Mock<IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Permission.ProcessSalesReturn)).Returns(false);

            var returnService = new ReturnService(dbContext, authzMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                returnService.ProcessSalesReturnAsync(1, new List<SalesReturnItem>(), "Reason", 1));
        }

        [Fact]
        public async Task InventoryService_AddStockAdjustment_Enforces_Permission()
        {
            var dbContext = GetDbContext();
            var authzMock = new Mock<IAuthorizationService>();
            authzMock.Setup(x => x.HasPermission(Permission.CreateStockAdjustment)).Returns(false);

            var inventoryService = new InventoryService(dbContext, authzMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                inventoryService.AddStockAdjustmentAsync(1, InventoryManagement.Domain.Enums.StockTransactionType.ManualAdjustment, 5, "Found", 1));
        }
        
        [Fact]
        public void AuthorizationService_HasPermission_Admin_CanManageUsers_Staff_Cannot()
        {
            // Test the actual AuthorizationService logic using mock IAuthenticationService
            var authMockAdmin = new Mock<IAuthenticationService>();
            authMockAdmin.Setup(x => x.CurrentUser).Returns(new User { Role = new Role { RoleName = "Administrator" } });
            
            var authMockStaff = new Mock<IAuthenticationService>();
            authMockStaff.Setup(x => x.CurrentUser).Returns(new User { Role = new Role { RoleName = "Staff" } });

            var adminAuthz = new AuthorizationService(authMockAdmin.Object);
            var staffAuthz = new AuthorizationService(authMockStaff.Object);

            Assert.True(adminAuthz.HasPermission(Permission.ManageUsers));
            Assert.False(staffAuthz.HasPermission(Permission.ManageUsers));
            
            Assert.True(adminAuthz.HasPermission(Permission.CompleteSale));
            Assert.True(staffAuthz.HasPermission(Permission.CompleteSale));
        }
    }
}

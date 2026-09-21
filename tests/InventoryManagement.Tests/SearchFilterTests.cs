using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Application.DTOs.Criteria;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Infrastructure.Data;
using Moq;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Tests
{
    public class SearchFilterTests
    {
        private InventoryDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new InventoryDbContext(options);
        }

        private Mock<IAuthorizationService> GetAuthzMock()
        {
            var mock = new Mock<IAuthorizationService>();
            mock.Setup(x => x.HasPermission(It.IsAny<Permission>())).Returns(true);
            return mock;
        }

        [Fact]
        public async Task Product_Search_FiltersCorrectly()
        {
            var db = GetDbContext(Guid.NewGuid().ToString());
            
            db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Cat1" });
            db.Units.Add(new Unit { UnitId = 1, UnitName = "pcs" });
            
            db.Products.Add(new Product { ProductId = 1, ProductName = "Apple", SKU = "SKU-A", Barcode = "111", CategoryId = 1, UnitId = 1, IsActive = true, CostPrice = 1, SellingPrice = 2 });
            db.Products.Add(new Product { ProductId = 2, ProductName = "Banana", SKU = "SKU-B", Barcode = "222", CategoryId = 1, UnitId = 1, IsActive = true, CostPrice = 1, SellingPrice = 2 });
            db.Products.Add(new Product { ProductId = 3, ProductName = "Cherry", SKU = "SKU-C", Barcode = "333", CategoryId = 1, UnitId = 1, IsActive = false, CostPrice = 1, SellingPrice = 2 });
            await db.SaveChangesAsync();

            var service = new ProductService(db, new Moq.Mock<IAuditLogService>().Object, new Moq.Mock<IAuthenticationService>().Object);

            // Search by Name
            var r1 = await service.SearchProductsAsync(new ProductSearchCriteria { SearchText = "App" });
            Assert.Single(r1.Items);
            Assert.Equal("Apple", r1.Items.First().ProductName);

            // Search by SKU
            var r2 = await service.SearchProductsAsync(new ProductSearchCriteria { SearchText = "SKU-B" });
            Assert.Single(r2.Items);
            Assert.Equal("Banana", r2.Items.First().ProductName);

            // Search by Barcode
            var r3 = await service.SearchProductsAsync(new ProductSearchCriteria { SearchText = "333" });
            Assert.Single(r3.Items); // Includes inactive unless explicitly filtered out? Wait, no, the query uses IsActive filter.

            // Wait, in my ProductService SearchProductsAsync, I didn't enforce IsActive = true by default! I allowed `criteria.IsActive.HasValue`. 
            // If it's null, it gets all. Let's verify.
            
            // Search Active Only
            var r4 = await service.SearchProductsAsync(new ProductSearchCriteria { IsActive = true });
            Assert.Equal(2, r4.Items.Count());

            // Category Filter
            var r5 = await service.SearchProductsAsync(new ProductSearchCriteria { CategoryId = 1 });
            Assert.Equal(3, r5.Items.Count()); // all 3 products are in category 1
        }

        [Fact]
        public async Task Inventory_Search_FiltersCorrectly()
        {
            var db = GetDbContext(Guid.NewGuid().ToString());
            
            db.Categories.Add(new Category { CategoryId = 1, CategoryName = "Cat1" });
            db.Units.Add(new Unit { UnitId = 1, UnitName = "pcs" });
            
            // Product 1: Out of Stock
            db.Products.Add(new Product { ProductId = 1, ProductName = "P1", CategoryId = 1, UnitId = 1, IsActive = true, ReorderLevel = 10, CostPrice = 1, SellingPrice = 2, SKU = "1" });
            db.StockTransactions.Add(new StockTransaction { ProductId = 1, Quantity = 0, TransactionType = StockTransactionType.OpeningBalance });
            
            // Product 2: Low Stock (Stock = 5, Reorder = 10)
            db.Products.Add(new Product { ProductId = 2, ProductName = "P2", CategoryId = 1, UnitId = 1, IsActive = true, ReorderLevel = 10, CostPrice = 1, SellingPrice = 2, SKU = "2" });
            db.StockTransactions.Add(new StockTransaction { ProductId = 2, Quantity = 5, TransactionType = StockTransactionType.OpeningBalance });
            
            // Product 3: In Stock (Stock = 20, Reorder = 10)
            db.Products.Add(new Product { ProductId = 3, ProductName = "P3", CategoryId = 1, UnitId = 1, IsActive = true, ReorderLevel = 10, CostPrice = 1, SellingPrice = 2, SKU = "3" });
            db.StockTransactions.Add(new StockTransaction { ProductId = 3, Quantity = 20, TransactionType = StockTransactionType.OpeningBalance });
            
            await db.SaveChangesAsync();

            var service = new InventoryService(db, GetAuthzMock().Object);

            var outOfStock = await service.SearchInventoryAsync(new InventorySearchCriteria { StockStatus = "Out of Stock" });
            Assert.Single(outOfStock.Items);
            Assert.Equal(1, outOfStock.Items.First().ProductId);

            var lowStock = await service.SearchInventoryAsync(new InventorySearchCriteria { StockStatus = "Low Stock" });
            Assert.Single(lowStock.Items);
            Assert.Equal(2, lowStock.Items.First().ProductId);

            var inStock = await service.SearchInventoryAsync(new InventorySearchCriteria { StockStatus = "In Stock" });
            Assert.Single(inStock.Items);
            Assert.Equal(3, inStock.Items.First().ProductId);
            
            var all = await service.SearchInventoryAsync(new InventorySearchCriteria { CategoryId = 1 });
            Assert.Equal(3, all.Items.Count());
        }

        [Fact]
        public async Task Sale_Search_FiltersCorrectly()
        {
            var db = GetDbContext(Guid.NewGuid().ToString());
            
            db.Customers.Add(new Customer { CustomerId = 1, CustomerName = "John" });
            db.Customers.Add(new Customer { CustomerId = 2, CustomerName = "Jane" });
            
            db.Sales.Add(new Sale { SaleId = 1, InvoiceNumber = "INV-1", CustomerId = 1, SaleDate = new DateTime(2026, 9, 1), Status = SaleStatus.Completed });
            db.Sales.Add(new Sale { SaleId = 2, InvoiceNumber = "INV-2", CustomerId = 2, SaleDate = new DateTime(2026, 9, 15), Status = SaleStatus.Cancelled });
            
            await db.SaveChangesAsync();

            var service = new SaleService(db, GetAuthzMock().Object);

            // Date Range
            var r1 = await service.SearchSalesAsync(new SalesSearchCriteria { DateFrom = new DateTime(2026, 9, 1), DateTo = new DateTime(2026, 9, 10) });
            Assert.Single(r1.Items);
            Assert.Equal(1, r1.Items.First().SaleId);

            // Customer
            var r2 = await service.SearchSalesAsync(new SalesSearchCriteria { CustomerId = 2 });
            Assert.Single(r2.Items);
            Assert.Equal(2, r2.Items.First().SaleId);

            // Status
            var r3 = await service.SearchSalesAsync(new SalesSearchCriteria { Status = SaleStatus.Cancelled });
            Assert.Single(r3.Items);

            // Search by Invoice
            var r4 = await service.SearchSalesAsync(new SalesSearchCriteria { SearchText = "INV-1" });
            Assert.Single(r4.Items);
        }

        [Fact]
        public async Task Purchase_Search_FiltersCorrectly()
        {
            var db = GetDbContext(Guid.NewGuid().ToString());
            
            db.Suppliers.Add(new Supplier { SupplierId = 1, SupplierName = "Supplier A" });
            db.Purchases.Add(new Purchase { PurchaseId = 1, PurchaseNumber = "PUR-1", SupplierId = 1, PurchaseDate = new DateTime(2026, 9, 1), Status = PurchaseStatus.Completed });
            db.Purchases.Add(new Purchase { PurchaseId = 2, PurchaseNumber = "PUR-2", SupplierId = 1, PurchaseDate = new DateTime(2026, 9, 20), Status = PurchaseStatus.Draft });
            
            await db.SaveChangesAsync();

            var service = new PurchaseService(db, GetAuthzMock().Object);

            var r1 = await service.SearchPurchasesAsync(new PurchaseSearchCriteria { DateFrom = new DateTime(2026, 9, 15) });
            Assert.Single(r1.Items);
            Assert.Equal(2, r1.Items.First().PurchaseId);

            var r2 = await service.SearchPurchasesAsync(new PurchaseSearchCriteria { Status = PurchaseStatus.Draft });
            Assert.Single(r2.Items);

            var r3 = await service.SearchPurchasesAsync(new PurchaseSearchCriteria { SearchText = "Supplier A" });
            Assert.Equal(2, r3.Items.Count());
        }
    }
}

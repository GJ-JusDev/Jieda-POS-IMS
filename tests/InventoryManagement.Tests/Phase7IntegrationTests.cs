using System;
using System.Collections.Generic;
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

public class Phase7IntegrationTests
{
    private InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new InventoryDbContext(options);

        // Seed basic data
        context.Products.Add(new Product { ProductId = 1, ProductName = "Test Product", SellingPrice = 10, CostPrice = 5, IsActive = true });
        context.Customers.Add(new Customer { CustomerId = 1, CustomerName = "Test Customer" });
        context.Suppliers.Add(new Supplier { SupplierId = 1, SupplierName = "Test Supplier" });
        context.SaveChanges();
        return context;
    }

    private IAuthorizationService CreateAuthMock(bool hasPermission = true)
    {
        var mock = new Mock<IAuthorizationService>();
        mock.Setup(a => a.HasPermission(It.IsAny<Permission>())).Returns(hasPermission);
        return mock.Object;
    }

    [Fact]
    public async Task Sale_WithInsufficientStock_ThrowsExceptionAndRollsBack()
    {
        var db = CreateDbContext();
        var saleService = new SaleService(db, CreateAuthMock());

        var items = new List<SaleItem> { new SaleItem { ProductId = 1, Quantity = 5 } };

        await Assert.ThrowsAsync<Exception>(() => saleService.CreateSaleAsync(1, 1, items, "Cash", null, allowNegativeStock: false));
        
        // Assert rollback
        Assert.Empty(db.Sales);
        Assert.Empty(db.SaleItems);
        Assert.Empty(db.StockTransactions);
        Assert.Empty(db.AuditLogs.Where(a => a.Action == "SaleCompleted"));
    }

    [Fact]
    public async Task Purchase_StateTransitions_Idempotency()
    {
        var db = CreateDbContext();
        var purchaseService = new PurchaseService(db, CreateAuthMock());

        // Create Draft Purchase
        var purchase = await purchaseService.CreateDraftPurchaseAsync(1, 1, "Notes");
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);

        // Add item
        await purchaseService.AddPurchaseItemAsync(purchase.PurchaseId, 1, 10, 5);

        // Complete Purchase
        await purchaseService.CompletePurchaseAsync(purchase.PurchaseId, 1);
        
        // Verify stock added
        var stock = await db.StockTransactions.Where(st => st.ProductId == 1).SumAsync(st => st.Quantity);
        Assert.Equal(10, stock);

        // Attempt duplicate complete
        await Assert.ThrowsAsync<Exception>(() => purchaseService.CompletePurchaseAsync(purchase.PurchaseId, 1));
        
        // Verify stock remains exactly 10
        stock = await db.StockTransactions.Where(st => st.ProductId == 1).SumAsync(st => st.Quantity);
        Assert.Equal(10, stock);
    }

    [Fact]
    public async Task SaleReturn_ExceedingQuantity_ThrowsException()
    {
        var db = CreateDbContext();
        var saleService = new SaleService(db, CreateAuthMock());
        var purchaseService = new PurchaseService(db, CreateAuthMock());
        var returnService = new ReturnService(db, CreateAuthMock());

        // Provide initial stock so sale succeeds
        var p = await purchaseService.CreateDraftPurchaseAsync(1, 1, null);
        await purchaseService.AddPurchaseItemAsync(p.PurchaseId, 1, 10, 5);
        await purchaseService.CompletePurchaseAsync(p.PurchaseId, 1);

        // Create Sale of 5 units
        var items = new List<SaleItem> { new SaleItem { ProductId = 1, Quantity = 5 } };
        var sale = await saleService.CreateSaleAsync(1, 1, items, "Cash", null);

        // Attempt to return 6 units (exceeds 5)
        var returnItems = new List<SalesReturnItem> { new SalesReturnItem { ProductId = 1, Quantity = 6 } };
        await Assert.ThrowsAsync<Exception>(() => returnService.ProcessSalesReturnAsync(sale.SaleId, returnItems, "Too many", 1));

        // Return 3 units (succeeds)
        var validReturnItems = new List<SalesReturnItem> { new SalesReturnItem { ProductId = 1, Quantity = 3 } };
        await returnService.ProcessSalesReturnAsync(sale.SaleId, validReturnItems, "Partial", 1);

        // Attempt to return 3 more (exceeds remaining 2)
        var secondReturnItems = new List<SalesReturnItem> { new SalesReturnItem { ProductId = 1, Quantity = 3 } };
        await Assert.ThrowsAsync<Exception>(() => returnService.ProcessSalesReturnAsync(sale.SaleId, secondReturnItems, "Too many again", 1));
        
        // Return remaining 2 units (succeeds)
        var finalReturnItems = new List<SalesReturnItem> { new SalesReturnItem { ProductId = 1, Quantity = 2 } };
        await returnService.ProcessSalesReturnAsync(sale.SaleId, finalReturnItems, "Final partial", 1);

        // Stock calculation: 10 (Purch) - 5 (Sale) + 3 (Return1) + 2 (Return2) = 10
        var stock = await db.StockTransactions.Where(st => st.ProductId == 1).SumAsync(st => st.Quantity);
        Assert.Equal(10, stock);
    }

    [Fact]
    public async Task PurchaseReturn_ExceedingQuantity_ThrowsException()
    {
        var db = CreateDbContext();
        var purchaseService = new PurchaseService(db, CreateAuthMock());
        var returnService = new ReturnService(db, CreateAuthMock());

        // Purchase 10 units
        var p = await purchaseService.CreateDraftPurchaseAsync(1, 1, null);
        await purchaseService.AddPurchaseItemAsync(p.PurchaseId, 1, 10, 5);
        await purchaseService.CompletePurchaseAsync(p.PurchaseId, 1);

        // Attempt return 11 units
        var returnItems = new List<PurchaseReturnItem> { new PurchaseReturnItem { ProductId = 1, Quantity = 11 } };
        await Assert.ThrowsAsync<Exception>(() => returnService.ProcessPurchaseReturnAsync(p.PurchaseId, returnItems, "Too many", 1));

        // Return 4 units
        var validReturnItems = new List<PurchaseReturnItem> { new PurchaseReturnItem { ProductId = 1, Quantity = 4 } };
        await returnService.ProcessPurchaseReturnAsync(p.PurchaseId, validReturnItems, "Partial", 1);

        // Attempt 7 units
        var secondReturnItems = new List<PurchaseReturnItem> { new PurchaseReturnItem { ProductId = 1, Quantity = 7 } };
        await Assert.ThrowsAsync<Exception>(() => returnService.ProcessPurchaseReturnAsync(p.PurchaseId, secondReturnItems, "Too many again", 1));

        // Return 6 units
        var finalReturnItems = new List<PurchaseReturnItem> { new PurchaseReturnItem { ProductId = 1, Quantity = 6 } };
        await returnService.ProcessPurchaseReturnAsync(p.PurchaseId, finalReturnItems, "Final partial", 1);

        // Stock: 10 - 4 - 6 = 0
        var stock = await db.StockTransactions.Where(st => st.ProductId == 1).SumAsync(st => st.Quantity);
        Assert.Equal(0, stock);
    }
    
    [Fact]
    public async Task LedgerConsistency_ComplexSequence_MaintainsExactStock()
    {
        var db = CreateDbContext();
        var purchaseService = new PurchaseService(db, CreateAuthMock());
        var saleService = new SaleService(db, CreateAuthMock());
        var returnService = new ReturnService(db, CreateAuthMock());
        var inventoryService = new InventoryService(db, CreateAuthMock());

        // 1. Opening Balance (+20)
        await inventoryService.AddStockAdjustmentAsync(1, StockTransactionType.OpeningBalance, 20, "Init", 1);
        
        // 2. Purchase (+10)
        var p = await purchaseService.CreateDraftPurchaseAsync(1, 1, null);
        await purchaseService.AddPurchaseItemAsync(p.PurchaseId, 1, 10, 5);
        await purchaseService.CompletePurchaseAsync(p.PurchaseId, 1);

        // 3. Sale (-5)
        var sItems = new List<SaleItem> { new SaleItem { ProductId = 1, Quantity = 5 } };
        var s = await saleService.CreateSaleAsync(1, 1, sItems, "Cash", null);

        // 4. Damage (-2)
        await inventoryService.AddStockAdjustmentAsync(1, StockTransactionType.Damage, -2, "Damage", 1);

        // 5. Sales Return (+1)
        var srItems = new List<SalesReturnItem> { new SalesReturnItem { ProductId = 1, Quantity = 1 } };
        await returnService.ProcessSalesReturnAsync(s.SaleId, srItems, "Return", 1);

        // 6. Purchase Return (-3)
        var prItems = new List<PurchaseReturnItem> { new PurchaseReturnItem { ProductId = 1, Quantity = 3 } };
        await returnService.ProcessPurchaseReturnAsync(p.PurchaseId, prItems, "Return to supplier", 1);

        // 7. Found (+2)
        await inventoryService.AddStockAdjustmentAsync(1, StockTransactionType.Found, 2, "Found", 1);

        // 8. Manual Adjustment (-1)
        await inventoryService.AddStockAdjustmentAsync(1, StockTransactionType.ManualAdjustment, -1, "Manual", 1);

        // Expected stock: 20 + 10 - 5 - 2 + 1 - 3 + 2 - 1 = 22
        var finalStock = await inventoryService.GetCurrentStockAsync(1);
        Assert.Equal(22m, finalStock);
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Infrastructure.Data;

namespace InventoryManagement.Tests
{
    class Program
    {
        static async Task Main()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var dbContext = new InventoryDbContext(options);
            var service = new InventoryService(dbContext);
            
            dbContext.Products.Add(new Product { ProductId = 1, SKU = "P1", ProductName = "P1", CategoryId = 1, UnitId = 1, CostPrice = 5, IsActive = true });
            dbContext.StockTransactions.Add(new StockTransaction { ProductId = 1, TransactionType = InventoryManagement.Domain.Enums.StockTransactionType.OpeningBalance, Quantity = 10 });
            await dbContext.SaveChangesAsync();
            
            var overview = await service.GetStockOverviewAsync();
            Console.WriteLine($"Products count: {dbContext.Products.Count()}");
            Console.WriteLine($"Overview count: {overview.Count()}");
            foreach (var item in overview) {
                Console.WriteLine($"{item.ProductName}: {item.CurrentStock}");
            }
            var val = await service.GetTotalInventoryValueAsync();
            Console.WriteLine($"Val: {val}");
        }
    }
}

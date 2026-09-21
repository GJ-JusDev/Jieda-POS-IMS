using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Application.DTOs.Dashboard;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IInventoryDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IAuthorizationService _authzService;

    public DashboardService(IInventoryDbContext context, IInventoryService inventoryService, IAuthorizationService authzService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _authzService = authzService;
    }

    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync()
    {
        // Authorization - Dashboard should be accessible by all authenticated roles according to the instruction:
        // "Dashboard is read-only and should not bypass any existing authorization architecture. 
        // If the existing application allows Administrator, Manager, and Staff to view operational information, preserve that behavior."
        // We assume basic read access is granted to anyone who can reach the dashboard.

        var metrics = new DashboardMetricsDto();

        // 1. Inventory Valuation & Low Stock
        var stockOverview = await _inventoryService.GetStockOverviewAsync();
        
        metrics.TotalInventoryValue = stockOverview.Sum(i => i.CurrentStock > 0 ? i.CurrentStock * i.CostPrice : 0);
        metrics.LowStockCount = stockOverview.Count(i => i.StockStatus == "Low Stock");
        metrics.OutOfStockCount = stockOverview.Count(i => i.StockStatus == "Out of Stock");

        // "Today" boundaries. The DB stores dates in UtcNow (as seen in SaleService.cs: SaleDate = DateTime.UtcNow).
        // To accurately get "Today" for the local user, we find today's local midnight, and convert to UTC.
        var todayStartLocal = DateTime.Today;
        var todayStartUtc = todayStartLocal.ToUniversalTime();
        var tomorrowStartUtc = todayStartLocal.AddDays(1).ToUniversalTime();

        // 2. Today's Sales
        var salesAmounts = await _context.Sales
            .Where(s => s.Status == SaleStatus.Completed && 
                        s.SaleDate >= todayStartUtc && 
                        s.SaleDate < tomorrowStartUtc)
            .Select(s => s.TotalAmount)
            .ToListAsync();
        metrics.TodaySales = salesAmounts.Sum();

        // 3. Today's Purchases
        var purchaseAmounts = await _context.Purchases
            .Where(p => p.Status == PurchaseStatus.Completed && 
                        p.PurchaseDate >= todayStartUtc && 
                        p.PurchaseDate < tomorrowStartUtc)
            .Select(p => p.TotalAmount)
            .ToListAsync();
        metrics.TodayPurchases = purchaseAmounts.Sum();

        // 4. Counts
        metrics.TotalActiveProducts = await _context.Products.CountAsync(p => p.IsActive);
        metrics.TotalActiveCustomers = await _context.Customers.CountAsync(c => c.IsActive);
        metrics.TotalActiveSuppliers = await _context.Suppliers.CountAsync(s => s.IsActive);

        // 5. Recent Stock Movements
        var recentTxs = await _context.StockTransactions
            .Include(st => st.Product)
            .OrderByDescending(st => st.TransactionDate)
            .Take(20)
            .ToListAsync();

        metrics.RecentMovements = recentTxs.Select(tx => new RecentStockMovementDto
        {
            Date = tx.TransactionDate.ToLocalTime(), // Display in local time
            ProductName = tx.Product?.ProductName ?? "Unknown",
            TransactionType = FormatTransactionType(tx.TransactionType),
            Quantity = tx.Quantity,
            Reference = $"{tx.ReferenceType} {tx.ReferenceId}",
            Notes = tx.Notes ?? string.Empty
        }).ToList();

        return metrics;
    }

    private string FormatTransactionType(StockTransactionType type)
    {
        return type switch
        {
            StockTransactionType.Purchase => "Purchase",
            StockTransactionType.Sale => "Sale",
            StockTransactionType.SalesReturn => "Sales Return",
            StockTransactionType.PurchaseReturn => "Purchase Return",
            (StockTransactionType)4 => "Legacy Adjustment Increase",
            (StockTransactionType)5 => "Legacy Adjustment Decrease",
            StockTransactionType.OpeningBalance => "Opening Balance",
            StockTransactionType.Damage => "Damage",
            StockTransactionType.Loss => "Loss",
            StockTransactionType.Found => "Found",
            StockTransactionType.ManualAdjustment => "Manual Adjustment",
            _ => type.ToString()
        };
    }
}

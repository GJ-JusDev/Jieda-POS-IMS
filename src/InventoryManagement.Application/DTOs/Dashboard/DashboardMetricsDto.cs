using System;
using System.Collections.Generic;

namespace InventoryManagement.Application.DTOs.Dashboard;

public class DashboardMetricsDto
{
    public decimal TotalInventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal TodaySales { get; set; }
    public decimal TodayPurchases { get; set; }
    public int TotalActiveProducts { get; set; }
    public int TotalActiveCustomers { get; set; }
    public int TotalActiveSuppliers { get; set; }
    
    public IEnumerable<RecentStockMovementDto> RecentMovements { get; set; } = new List<RecentStockMovementDto>();
}

public class RecentStockMovementDto
{
    public DateTime Date { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

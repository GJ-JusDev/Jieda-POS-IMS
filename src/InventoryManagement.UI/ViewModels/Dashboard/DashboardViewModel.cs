using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.UI.ViewModels.Base;
using InventoryManagement.Application.DTOs;

namespace InventoryManagement.UI.ViewModels.Dashboard;

public class CategoryAnalyticDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}

public class DashboardViewModel : ViewModelBase
{
    private readonly IReportService _reportService;
    private readonly IInventoryService _inventoryService;

    private decimal _monthlySales;
    public decimal MonthlySales { get => _monthlySales; set => SetProperty(ref _monthlySales, value); }

    private decimal _monthlyPurchases;
    public decimal MonthlyPurchases { get => _monthlyPurchases; set => SetProperty(ref _monthlyPurchases, value); }

    private int _lowStockCount;
    public int LowStockCount { get => _lowStockCount; set => SetProperty(ref _lowStockCount, value); }

        public ObservableCollection<StockOverviewDto> LowStockItems { get; set; } = new();

    public ObservableCollection<CategoryAnalyticDto> CategoryAnalytics { get; set; } = new();

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(IReportService reportService, IInventoryService inventoryService)
    {
        _reportService = reportService;
        _inventoryService = inventoryService;
        
        RefreshCommand = new RelayCommand(async _ => await LoadDashboardDataAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = DateTime.Now;

            // Load Sales
            var sales = await _reportService.GetSalesReportAsync(startDate, endDate);
            MonthlySales = sales.Sum(s => s.TotalAmount);

            // Load Purchases
            var purchases = await _reportService.GetPurchaseReportAsync(startDate, endDate);
            MonthlyPurchases = purchases.Sum(p => p.TotalAmount);

            // Load Low Stock
                        var inventory = await _inventoryService.GetStockOverviewAsync();
            
            // Analytics
            var analytics = inventory
                .GroupBy(i => i.CategoryName)
                .Select(g => new CategoryAnalyticDto { CategoryName = string.IsNullOrEmpty(g.Key) ? "Uncategorized" : g.Key, ProductCount = g.Count() })
                .OrderByDescending(a => a.ProductCount)
                .ToList();
            
            CategoryAnalytics.Clear();
            foreach (var a in analytics) CategoryAnalytics.Add(a);
            var lowStock = inventory.Where(i => i.StockStatus == "Low Stock" || i.StockStatus == "Out of Stock").ToList();
            
            LowStockCount = lowStock.Count;
            
            LowStockItems.Clear();
            foreach(var item in lowStock.Take(10)) // show top 10
            {
                LowStockItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading dashboard: {ex.Message}");
        }
    }
}



using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Application.DTOs;
using InventoryManagement.Domain.Entities;
using InventoryManagement.UI.ViewModels.Base;

namespace InventoryManagement.UI.ViewModels.Inventory;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthenticationService _authService;

    public ObservableCollection<StockOverviewDto> StockItems { get; set; } = new();
    public ObservableCollection<Category> Categories { get; set; } = new();
    public ObservableCollection<string> StatusFilters { get; set; } = new() { "All", "In Stock", "Low Stock", "Out of Stock" };

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { SetProperty(ref _searchText, value); }
    }

    private Category? _selectedFilterCategory;
    public Category? SelectedFilterCategory
    {
        get => _selectedFilterCategory;
        set { SetProperty(ref _selectedFilterCategory, value); }
    }

    private string _selectedStatusFilter = "All";
    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set { SetProperty(ref _selectedStatusFilter, value); }
    }

    private StockOverviewDto? _selectedStockItem;
    public StockOverviewDto? SelectedStockItem
    {
        get => _selectedStockItem;
        set { SetProperty(ref _selectedStockItem, value); }
    }

    public ICommand SearchCommand { get; }
    public ICommand AdjustStockCommand { get; }
    public ICommand ViewHistoryCommand { get; }

    public InventoryViewModel(IInventoryService inventoryService, ICategoryService categoryService, IAuthenticationService authService)
    {
        _inventoryService = inventoryService;
        _categoryService = categoryService;
        _authService = authService;

        SearchCommand = new RelayCommand(async _ => await LoadInventoryAsync());
        AdjustStockCommand = new RelayCommand(async _ => await AdjustStockAsync(), _ => SelectedStockItem != null);
        ViewHistoryCommand = new RelayCommand(_ => ViewHistory(), _ => SelectedStockItem != null);
    }

    public async Task InitializeAsync()
    {
        var cats = await _categoryService.GetAllAsync();
        foreach (var c in cats) Categories.Add(c);
        await LoadInventoryAsync();
    }

    private async Task LoadInventoryAsync()
    {
        var result = await _inventoryService.GetStockOverviewAsync(SearchText, SelectedFilterCategory?.CategoryId, SelectedStatusFilter);
        StockItems.Clear();
        foreach (var item in result)
        {
            StockItems.Add(item);
        }
    }

    private async Task AdjustStockAsync()
    {
        if (SelectedStockItem == null || _authService.CurrentUser == null) return;
        
        // normally we would show a dialog with quantity and reason, simulating it for Phase 4:
        var quantityToAdjust = 5m; // simulating adding 5 items
        var reason = "Physical count correction";
        
        var result = MessageBox.Show($"Adjust stock for {SelectedStockItem.ProductName} by +{quantityToAdjust}?\nReason: {reason}", "Stock Adjustment", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _inventoryService.AddStockAdjustmentAsync(SelectedStockItem.ProductId, quantityToAdjust, reason, _authService.CurrentUser.UserId);
                await LoadInventoryAsync();
                MessageBox.Show("Stock adjusted successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adjusting stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ViewHistory()
    {
        if (SelectedStockItem == null) return;
        MessageBox.Show($"View History Dialog Placeholder for {SelectedStockItem.ProductName}");
    }
}

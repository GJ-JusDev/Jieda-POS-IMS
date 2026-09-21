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
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.UI.ViewModels.Inventory;

public class InventoryViewModel : ViewModelBase, InventoryManagement.Application.Interfaces.IBarcodeScannerTarget
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

    private string _emptyMessage = string.Empty;
    public string EmptyMessage
    {
        get => _emptyMessage;
        set { SetProperty(ref _emptyMessage, value); }
    }

    private async Task LoadInventoryAsync()
    {
        var criteria = new InventoryManagement.Application.DTOs.Criteria.InventorySearchCriteria
        {
            SearchText = SearchText,
            CategoryId = SelectedFilterCategory?.CategoryId,
            StockStatus = SelectedStatusFilter,
            Page = 1,
            PageSize = 1000
        };

        var result = await _inventoryService.SearchInventoryAsync(criteria);
        StockItems.Clear();
        foreach (var item in result.Items)
        {
            StockItems.Add(item);
        }

        if (!StockItems.Any())
        {
            EmptyMessage = "No inventory records found.";
        }
        else
        {
            EmptyMessage = string.Empty;
        }
    }

    private async Task AdjustStockAsync()
    {
        if (SelectedStockItem == null || _authService.CurrentUser == null) return;
        
        var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Inventory.StockAdjustmentWindow>();
        var vm = (InventoryManagement.UI.ViewModels.Inventory.StockAdjustmentViewModel)window.DataContext;
        await vm.LoadProductAsync(SelectedStockItem.ProductId);
        
        var result = window.ShowDialog();
        
        if (result == true)
        {
            await LoadInventoryAsync();
        }
    }

    private async void ViewHistory()
    {
        if (SelectedStockItem == null) return;
        
        var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Inventory.StockHistoryWindow>();
        var vm = (InventoryManagement.UI.ViewModels.Inventory.StockHistoryViewModel)window.DataContext;
        await vm.LoadHistoryAsync(SelectedStockItem.ProductId);
        
        window.ShowDialog();
    }

    public async void OnBarcodeScanned(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        
        SearchText = barcode;
        SelectedFilterCategory = null;
        SelectedStatusFilter = "All";
        await LoadInventoryAsync();

        // Optional: Pre-select if exactly one match
        if (StockItems.Count == 1)
        {
            SelectedStockItem = StockItems[0];
        }
    }
}

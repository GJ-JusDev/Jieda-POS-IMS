using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.UI.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.UI.ViewModels.Inventory;

public class StockAdjustmentViewModel : ViewModelBase, IBarcodeScannerTarget
{
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
    private readonly IAuthenticationService _authService;

    public Action? CloseAction { get; set; }

    public ObservableCollection<StockTransactionType> AvailableTypes { get; } = new()
    {
        StockTransactionType.Damage,
        StockTransactionType.Loss,
        StockTransactionType.Found,
        StockTransactionType.ManualAdjustment
    };

    private StockTransactionType _selectedType = StockTransactionType.ManualAdjustment;
    public StockTransactionType SelectedType
    {
        get => _selectedType;
        set
        {
            SetProperty(ref _selectedType, value);
            OnPropertyChanged(nameof(ResultingStock));
            OnPropertyChanged(nameof(IsDirectionalHintVisible));
        }
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            SetProperty(ref _selectedProduct, value);
            LoadCurrentStock();
        }
    }

    private decimal _currentStock;
    public decimal CurrentStock
    {
        get => _currentStock;
        private set
        {
            SetProperty(ref _currentStock, value);
            OnPropertyChanged(nameof(ResultingStock));
        }
    }

    private decimal _quantity;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            SetProperty(ref _quantity, value);
            OnPropertyChanged(nameof(ResultingStock));
        }
    }

    private string _notes = string.Empty;
    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public decimal ResultingStock
    {
        get
        {
            if (SelectedProduct == null) return 0;
            
            decimal adjustment = Quantity;
            
            // Apply direction if it's not a manual adjustment containing its own sign
            if (SelectedType == StockTransactionType.Damage || SelectedType == StockTransactionType.Loss)
            {
                // Force negative for display logic
                adjustment = -Math.Abs(Quantity);
            }
            else if (SelectedType == StockTransactionType.Found)
            {
                adjustment = Math.Abs(Quantity);
            }

            return CurrentStock + adjustment;
        }
    }

    public bool IsDirectionalHintVisible => SelectedType == StockTransactionType.ManualAdjustment;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenCameraScannerCommand { get; }

    public StockAdjustmentViewModel(IInventoryService inventoryService, IProductService productService, IAuthenticationService authService)
    {
        _inventoryService = inventoryService;
        _productService = productService;
        _authService = authService;

        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => SelectedProduct != null && Quantity != 0);
        CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());
        OpenCameraScannerCommand = new RelayCommand(_ => OpenCameraScanner());
    }

    public async Task LoadProductAsync(int productId)
    {
        var product = await _productService.GetByIdAsync(productId);
        if (product != null)
        {
            SelectedProduct = product;
        }
    }

    private async void LoadCurrentStock()
    {
        if (SelectedProduct != null)
        {
            CurrentStock = await _inventoryService.GetCurrentStockAsync(SelectedProduct.ProductId);
        }
        else
        {
            CurrentStock = 0;
        }
    }

    private async Task SaveAsync()
    {
        if (SelectedProduct == null) return;

        if (Quantity == 0)
        {
            MessageBox.Show("Adjustment quantity cannot be zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal adjustmentQty = Quantity;
        if (SelectedType == StockTransactionType.Damage || SelectedType == StockTransactionType.Loss)
        {
            adjustmentQty = -Math.Abs(Quantity);
        }
        else if (SelectedType == StockTransactionType.Found)
        {
            adjustmentQty = Math.Abs(Quantity);
        }

        if (CurrentStock + adjustmentQty < 0)
        {
            MessageBox.Show("This adjustment would result in negative stock, which is not permitted.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var userId = _authService.CurrentUser?.UserId ?? 1;
            await _inventoryService.AddStockAdjustmentAsync(SelectedProduct.ProductId, SelectedType, adjustmentQty, Notes, userId);
            
            MessageBox.Show("Stock adjusted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenCameraScanner()
    {
        var scannerWindow = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Scanner.CameraScannerWindow>();
        var vm = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.ViewModels.Base.CameraScannerViewModel>();
        scannerWindow.DataContext = vm;
        vm.OnBarcodeDetected = barcode => 
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => OnBarcodeScanned(barcode));
        };
        scannerWindow.ShowDialog();
    }

    public async void OnBarcodeScanned(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        try
        {
            var product = await _productService.GetByBarcodeAsync(barcode);
            if (product != null)
            {
                if (!product.IsActive)
                {
                    MessageBox.Show("Cannot adjust stock for an inactive product.", "Inactive", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SelectedProduct = product;
            }
            else
            {
                MessageBox.Show("Product not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error processing barcode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

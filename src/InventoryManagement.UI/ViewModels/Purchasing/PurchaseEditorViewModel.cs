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

namespace InventoryManagement.UI.ViewModels.Purchasing;

public class PurchaseEditorViewModel : ViewModelBase, InventoryManagement.Application.Interfaces.IBarcodeScannerTarget
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;
    private readonly IAuthenticationService _authService;

    private string _supplierText = "";
    public string SupplierText
    {
        get => _supplierText;
        set => SetProperty(ref _supplierText, value);
    }

    public Action? CloseAction { get; set; }

    public ObservableCollection<Supplier> Suppliers { get; set; } = new();
    public ObservableCollection<Product> Products { get; set; } = new();
    public ObservableCollection<PurchaseItem> PurchaseItems { get; set; } = new();

    private Purchase _purchase = new();
    public Purchase Purchase
    {
        get => _purchase;
        set => SetProperty(ref _purchase, value);
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            SetProperty(ref _selectedProduct, value);
            if (value != null) UnitCost = value.CostPrice;
        }
    }

    private PurchaseItem? _selectedItem;
    public PurchaseItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private decimal _quantity = 1;
    public decimal Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }

    private decimal _unitCost;
    public decimal UnitCost { get => _unitCost; set => SetProperty(ref _unitCost, value); }

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public PurchaseEditorViewModel(IPurchaseService purchaseService, ISupplierService supplierService, IProductService productService, IAuthenticationService authService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _productService = productService;
        _authService = authService;

        AddItemCommand = new RelayCommand(async _ => await AddItemAsync());
        RemoveItemCommand = new RelayCommand(async _ => await RemoveItemAsync(), _ => SelectedItem != null);
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());
    }

    public async Task LoadDataAsync(Purchase? existingPurchase = null)
    {
        var suppliers = await _supplierService.GetAllAsync();
        Suppliers.Clear();
        foreach (var s in suppliers) Suppliers.Add(s);

        var products = await _productService.GetAllAsync();
        Products.Clear();
        foreach (var p in products.Where(p => p.IsActive)) Products.Add(p);

        if (existingPurchase != null)
        {
            var p = await _purchaseService.GetPurchaseAsync(existingPurchase.PurchaseId);
            if (p != null)
            {
                Purchase = p;
                SupplierText = p.Supplier?.SupplierName ?? "";
                RefreshItems();
            }
        }
        else
        {
            Purchase = new Purchase 
            { 
                PurchaseDate = DateTime.Now,
                SupplierId = Suppliers.FirstOrDefault()?.SupplierId ?? 0
            };
            SupplierText = Suppliers.FirstOrDefault()?.SupplierName ?? "";
        }
    }

    private async Task EnsureSupplierAsync()
    {
        if (Purchase.SupplierId == 0 && !string.IsNullOrWhiteSpace(SupplierText))
        {
            var existing = Suppliers.FirstOrDefault(s => s.SupplierName.Equals(SupplierText, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Purchase.SupplierId = existing.SupplierId;
            }
            else
            {
                var newSupplier = await _supplierService.AddAsync(new Supplier 
                { 
                    SupplierCode = "SUP" + DateTime.Now.ToString("yyMMddHHmm"), 
                    SupplierName = SupplierText 
                });
                Suppliers.Add(newSupplier);
                Purchase.SupplierId = newSupplier.SupplierId;
            }
        }
    }

    private void RefreshItems()
    {
        PurchaseItems.Clear();
        if (Purchase.PurchaseItems != null)
        {
            foreach(var item in Purchase.PurchaseItems) PurchaseItems.Add(item);
        }
    }

    private async Task AddItemAsync()
    {
        if (SelectedProduct == null || Quantity <= 0 || UnitCost < 0)
        {
            MessageBox.Show("Please select a valid product and enter positive quantity/cost.");
            return;
        }

        try
        {
            if (Purchase.PurchaseId == 0)
            {
                await EnsureSupplierAsync();
                var userId = _authService.CurrentUser?.UserId ?? 1;
                Purchase = await _purchaseService.CreateDraftPurchaseAsync(Purchase.SupplierId, userId, Purchase.Notes);
            }

            await _purchaseService.AddPurchaseItemAsync(Purchase.PurchaseId, SelectedProduct.ProductId, Quantity, UnitCost);
            
            // Reload purchase to get updated items and total
            var updated = await _purchaseService.GetPurchaseAsync(Purchase.PurchaseId);
            if (updated != null)
            {
                Purchase = updated;
                RefreshItems();
                OnPropertyChanged(nameof(Purchase));
            }
            
            Quantity = 1;
            SelectedProduct = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding item: {ex.Message}");
        }
    }

    private async Task RemoveItemAsync()
    {
        if (SelectedItem == null) return;
        try
        {
            await _purchaseService.RemovePurchaseItemAsync(SelectedItem.PurchaseItemId);
            
            var updated = await _purchaseService.GetPurchaseAsync(Purchase.PurchaseId);
            if (updated != null)
            {
                Purchase = updated;
                RefreshItems();
                OnPropertyChanged(nameof(Purchase));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error removing item: {ex.Message}");
        }
    }

    private async Task SaveAsync()
    {
        // Just closing since items are saved as they are added
        if (Purchase.PurchaseId == 0)
        {
            await EnsureSupplierAsync();
            var userId = _authService.CurrentUser?.UserId ?? 1;
            await _purchaseService.CreateDraftPurchaseAsync(Purchase.SupplierId, userId, Purchase.Notes);
        }
        
        CloseAction?.Invoke();
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
                    MessageBox.Show("Product is inactive", "Inactive", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedProduct = product;
                UnitCost = product.CostPrice;
                Quantity = 1;

                var existingItem = PurchaseItems.FirstOrDefault(i => i.ProductId == product.ProductId);
                if (existingItem != null)
                {
                    // Increase quantity of existing line
                    Quantity = existingItem.Quantity + 1;
                    await _purchaseService.RemovePurchaseItemAsync(existingItem.PurchaseItemId);
                }

                await AddItemAsync();
            }
            else
            {
                MessageBox.Show("Product not found", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error processing barcode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

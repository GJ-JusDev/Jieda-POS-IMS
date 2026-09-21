using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.UI.ViewModels.Base;
using InventoryManagement.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.UI.ViewModels.Sales;

public class SaleItemModel : ViewModelBase
{
    private int _productId;
    public int ProductId { get => _productId; set => SetProperty(ref _productId, value); }
    
    private string _productName = string.Empty;
    public string ProductName { get => _productName; set => SetProperty(ref _productName, value); }
    
    private decimal _quantity = 1;
    public decimal Quantity 
    { 
        get => _quantity; 
        set 
        { 
            if(SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(TotalPrice));
            }
        } 
    }
    
    private decimal _unitPrice;
    public decimal UnitPrice 
    { 
        get => _unitPrice; 
        set 
        {
            if(SetProperty(ref _unitPrice, value))
            {
                OnPropertyChanged(nameof(TotalPrice));
            }
        } 
    }
    
    private decimal _discount;
    public decimal Discount 
    { 
        get => _discount; 
        set 
        {
            if(SetProperty(ref _discount, value))
            {
                OnPropertyChanged(nameof(TotalPrice));
            }
        } 
    }
    
    public decimal TotalPrice => (Quantity * UnitPrice) - Discount;
}

public class SalesViewModel : ViewModelBase, InventoryManagement.Application.Interfaces.IBarcodeScannerTarget
{
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private readonly IAuthenticationService _authService;
    private readonly IReceiptPdfService _receiptPdfService;

    public ObservableCollection<SaleItemModel> CartItems { get; set; } = new();
    public ObservableCollection<Product> AvailableProducts { get; set; } = new();

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    private string _barcodeInput = string.Empty;
    public string BarcodeInput
    {
        get => _barcodeInput;
        set => SetProperty(ref _barcodeInput, value);
    }

    private decimal _totalAmount;
    public decimal TotalAmount
    {
        get => _totalAmount;
        set => SetProperty(ref _totalAmount, value);
    }
    
    public ICommand ScanBarcodeCommand { get; }
    public ICommand AddSelectedProductCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand OpenCameraScannerCommand { get; }

    public SalesViewModel(ISaleService saleService, IProductService productService, ICustomerService customerService, IAuthenticationService authService, IReceiptPdfService receiptPdfService)
    {
        _saleService = saleService;
        _productService = productService;
        _customerService = customerService;
        _authService = authService;
        _receiptPdfService = receiptPdfService;

        ScanBarcodeCommand = new RelayCommand(async _ => await ProcessBarcodeAsync());
        AddSelectedProductCommand = new RelayCommand(async _ => await AddSelectedProductAsync(), _ => SelectedProduct != null);
        RemoveItemCommand = new RelayCommand<SaleItemModel>(RemoveItem);
        CheckoutCommand = new RelayCommand(async _ => await CheckoutAsync(), _ => CartItems.Any());
        OpenCameraScannerCommand = new RelayCommand(_ => OpenCameraScanner());
        
        CartItems.CollectionChanged += (s, e) => UpdateTotal();
        
        _ = LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            var products = await _productService.GetAllAsync();
            foreach (var p in products.Where(x => x.IsActive))
            {
                AvailableProducts.Add(p);
            }
        }
        catch
        {
            // Ignore UI-level loading errors for dropdown; fallback to barcode scanner
        }
    }

    public async void OnBarcodeScanned(string barcode)
    {
        BarcodeInput = barcode;
        await ProcessBarcodeAsync();
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

    private async Task ProcessBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

        try
        {
            var product = await _productService.GetByBarcodeAsync(BarcodeInput);
            if (product != null)
            {
                await AddProductToCartAsync(product);
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
        finally
        {
            BarcodeInput = string.Empty; // Clear for next scan
        }
    }

    private async Task AddSelectedProductAsync()
    {
        if (SelectedProduct == null) return;
        try
        {
            await AddProductToCartAsync(SelectedProduct);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SelectedProduct = null;
        }
    }

    private async Task AddProductToCartAsync(Product product)
    {
        if (!product.IsActive)
        {
            MessageBox.Show("Product is inactive", "Inactive", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existingItem = CartItems.FirstOrDefault(i => i.ProductId == product.ProductId);
        
        // Optional UI-level stock check (Business logic still enforces this in Checkout)
        var currentStock = await ((App)System.Windows.Application.Current).Services.GetRequiredService<IInventoryService>().GetCurrentStockAsync(product.ProductId);
        var desiredQty = (existingItem?.Quantity ?? 0) + 1;
        
        if (currentStock < desiredQty)
        {
            MessageBox.Show($"Insufficient stock for '{product.ProductName}'. Available: {currentStock}", "Stock Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (existingItem != null)
        {
            existingItem.Quantity++;
            UpdateTotal();
        }
        else
        {
            var newItem = new SaleItemModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Quantity = 1,
                UnitPrice = product.SellingPrice,
                Discount = 0
            };
            
            // Attach property changed handler to update total when qty changes
            newItem.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SaleItemModel.TotalPrice)) UpdateTotal(); };
            
            CartItems.Add(newItem);
        }
    }

    private void RemoveItem(SaleItemModel item)
    {
        if (item != null && CartItems.Contains(item))
        {
            CartItems.Remove(item);
            UpdateTotal();
        }
    }

    private void UpdateTotal()
    {
        TotalAmount = CartItems.Sum(i => i.TotalPrice);
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            System.Windows.Input.CommandManager.InvalidateRequerySuggested());
    }

    private async Task CheckoutAsync()
    {
        if (!CartItems.Any()) return;

        try
        {
            var customers = await _customerService.GetAllAsync();
            var customer = customers.FirstOrDefault(c => c.CustomerName.Contains("Walk-in")) 
                           ?? customers.FirstOrDefault();
                           
            if (customer == null)
            {
                customer = await _customerService.AddAsync(new Customer { CustomerCode = "C01", CustomerName = "Walk-in Customer" });
            }

            var saleItems = CartItems.Select(ci => new SaleItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                UnitPrice = ci.UnitPrice,
                Discount = ci.Discount,
                TotalPrice = ci.TotalPrice
            }).ToList();

            var userId = _authService.CurrentUser?.UserId ?? 1;
            
            var sale = await _saleService.CreateSaleAsync(
                customerId: customer.CustomerId,
                createdBy: userId,
                items: saleItems,
                paymentMethod: "Cash",
                notes: null,
                allowNegativeStock: false
            );

            // Fetch the fully populated sale so it includes Product references for the receipt
            var fullSale = await _saleService.GetSaleAsync(sale.SaleId);
            
            var result = MessageBox.Show(
                $"Sale completed successfully!\nInvoice: {sale.InvoiceNumber}\nTotal: {sale.TotalAmount:C}\n\nWould you like to save the receipt as a PDF?",
                "Sale Success",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes && fullSale != null)
            {
                var safeInvoiceNumber = string.Join("_", sale.InvoiceNumber.Split(System.IO.Path.GetInvalidFileNameChars()));
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Receipt as PDF",
                    Filter = "PDF Documents (*.pdf)|*.pdf",
                    FileName = $"Invoice_{safeInvoiceNumber}.pdf",
                    DefaultExt = ".pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        await _receiptPdfService.GenerateReceiptAsync(fullSale, dialog.FileName);
                    }
                    catch (Exception pdfEx)
                    {
                        // Sale is already saved, don't rollback. Just inform the user.
                        // In a real app, we would log `pdfEx` here.
                        MessageBox.Show($"Sale completed, but the receipt could not be generated.\n\nDetails: {pdfEx.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            
            CartItems.Clear();
            UpdateTotal();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Checkout failed: {ex.Message}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

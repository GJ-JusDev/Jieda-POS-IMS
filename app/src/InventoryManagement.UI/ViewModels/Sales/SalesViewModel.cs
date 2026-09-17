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

public class SalesViewModel : ViewModelBase
{
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private readonly IAuthenticationService _authService;

    public ObservableCollection<SaleItemModel> CartItems { get; set; } = new();

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
    public ICommand RemoveItemCommand { get; }
    public ICommand CheckoutCommand { get; }

    public SalesViewModel(ISaleService saleService, IProductService productService, ICustomerService customerService, IAuthenticationService authService)
    {
        _saleService = saleService;
        _productService = productService;
        _customerService = customerService;
        _authService = authService;

        ScanBarcodeCommand = new RelayCommand(async _ => await ProcessBarcodeAsync());
        RemoveItemCommand = new RelayCommand<SaleItemModel>(RemoveItem);
        CheckoutCommand = new RelayCommand(async _ => await CheckoutAsync(), _ => CartItems.Any());
        
        CartItems.CollectionChanged += (s, e) => UpdateTotal();
    }

    private async Task ProcessBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

        try
        {
            // First try barcode, then fallback to SKU
            var product = await _productService.GetByBarcodeAsync(BarcodeInput);
            if (product == null)
            {
                product = await _productService.GetBySkuAsync(BarcodeInput);
            }

            if (product != null)
            {
                var existingItem = CartItems.FirstOrDefault(i => i.ProductId == product.ProductId);
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
            else
            {
                MessageBox.Show("Product not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            MessageBox.Show($"Sale completed successfully!\nInvoice: {sale.InvoiceNumber}\nTotal: {sale.TotalAmount:C}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
            CartItems.Clear();
            UpdateTotal();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Checkout failed: {ex.Message}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

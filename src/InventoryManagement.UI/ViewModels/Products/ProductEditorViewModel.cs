using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.UI.ViewModels.Base;
using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Windows.Compatibility;

namespace InventoryManagement.UI.ViewModels.Products;

public class ProductEditorViewModel : ViewModelBase
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IUnitService _unitService;
    private readonly IInventoryService _inventoryService;
    private readonly IAuthenticationService _authService;
    
    public ObservableCollection<Category> Categories { get; set; } = new();
    public ObservableCollection<Unit> Units { get; set; } = new();
    public ObservableCollection<Product> AvailableRawMaterials { get; set; } = new();
    public ObservableCollection<ProductComponent> Components { get; set; } = new();

    private Product _product;
    public Product Product
    {
        get => _product;
        set => SetProperty(ref _product, value);
    }

    private decimal _initialStock;
    public decimal InitialStock
    {
        get => _initialStock;
        set => SetProperty(ref _initialStock, value);
    }

    private bool _isNewProduct;
    public bool IsNewProduct
    {
        get => _isNewProduct;
        set => SetProperty(ref _isNewProduct, value);
    }

        public string BarcodeText
    {
        get => Product?.Barcode;
        set
        {
            if (Product != null)
            {
                Product.Barcode = value;
                OnPropertyChanged();
                GenerateBarcodeImage();
            }
        }
    }

    private BitmapImage _barcodeImage;
    public BitmapImage BarcodeImage
    {
        get => _barcodeImage;
        set => SetProperty(ref _barcodeImage, value);
    }

    private void GenerateBarcodeImage()
    {
        if (string.IsNullOrWhiteSpace(Product?.Barcode))
        {
            BarcodeImage = null;
            return;
        }
        
        try
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new ZXing.Common.EncodingOptions
                {
                    Width = 200,
                    Height = 60,
                    Margin = 0
                }
            };
            using var bitmap = writer.Write(Product.Barcode);
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze(); 
            
            BarcodeImage = image;
        }
        catch 
        {
            BarcodeImage = null;
        }
    }

        public string TotalInitialValueText
    {
        get
        {
            if (Product == null) return "";
            return (InitialStock * Product.CostPrice).ToString("C");
        }
    }

    public string InitialStockText
    {
        get => InitialStock == 0 ? "" : InitialStock.ToString("0.##");
        set
        {
                        if (string.IsNullOrWhiteSpace(value)) InitialStock = 0;
            else if (decimal.TryParse(value, out decimal parsed)) InitialStock = parsed;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalInitialValueText));
        }
    }

    public string CostPriceText
    {
        get => Product?.CostPrice == 0 ? "" : Product?.CostPrice.ToString("0.##");
        set
        {
            if (Product != null)
            {
                                if (string.IsNullOrWhiteSpace(value)) Product.CostPrice = 0;
                else if (decimal.TryParse(value, out decimal parsed)) Product.CostPrice = parsed;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalInitialValueText));
                CalculateSellingPrice();
            }
        }
    }

    public string SellingPriceText
    {
        get => Product?.SellingPrice == 0 ? "" : Product?.SellingPrice.ToString("0.##");
        set
        {
            if (Product != null)
            {
                if (string.IsNullOrWhiteSpace(value)) Product.SellingPrice = 0;
                else if (decimal.TryParse(value, out decimal parsed)) Product.SellingPrice = parsed;
                OnPropertyChanged();
            }
        }
    }

    public string MarkupPercentageText
    {
        get => Product?.MarkupPercentage == 0 ? "" : Product?.MarkupPercentage.ToString("0.##");
        set
        {
            if (Product != null)
            {
                if (string.IsNullOrWhiteSpace(value)) Product.MarkupPercentage = 0;
                else if (decimal.TryParse(value, out decimal parsed) && parsed >= 0) Product.MarkupPercentage = parsed;
                OnPropertyChanged();
                CalculateSellingPrice();
            }
        }
    }

    private Product _selectedRawMaterial;
    public Product SelectedRawMaterial
    {
        get => _selectedRawMaterial;
        set => SetProperty(ref _selectedRawMaterial, value);
    }

    private string _componentQuantityText;
    public string ComponentQuantityText
    {
        get => _componentQuantityText;
        set => SetProperty(ref _componentQuantityText, value);
    }

    public Action CloseAction { get; set; }
    public ICommand SaveCommand { get; }
    public ICommand AddComponentCommand { get; }
    public ICommand RemoveComponentCommand { get; }
    public ICommand CalculateCostCommand { get; }

    public ProductEditorViewModel(IProductService productService, ICategoryService categoryService, IUnitService unitService, IInventoryService inventoryService, IAuthenticationService authService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _unitService = unitService;
        _inventoryService = inventoryService;
        _authService = authService;
        
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        AddComponentCommand = new RelayCommand(_ => AddComponent(), _ => SelectedRawMaterial != null && decimal.TryParse(ComponentQuantityText, out decimal dummy));
        RemoveComponentCommand = new RelayCommand(comp => RemoveComponent(comp as ProductComponent));
        CalculateCostCommand = new RelayCommand(_ => CalculateCost());
    }

    public async Task LoadDataAsync(int? productId = null)
    {
        var cats = await _categoryService.GetAllAsync();
        foreach (var c in cats) Categories.Add(c);
        
        var units = await _unitService.GetAllAsync();
        foreach (var u in units) Units.Add(u);

        var allProducts = await _productService.GetAllAsync();
        foreach (var p in allProducts)
        {
            if (!p.IsManufactured)
            {
                AvailableRawMaterials.Add(p);
            }
        }

        if (productId.HasValue && productId.Value > 0)
        {
            var p = await _productService.GetByIdAsync(productId.Value);
            Product = p ?? new Product();
            IsNewProduct = false;
            if (Product.Components != null)
            {
                foreach(var comp in Product.Components)
                {
                    // Ensure RawMaterial is loaded
                    comp.RawMaterial = AvailableRawMaterials.FirstOrDefault(r => r.ProductId == comp.RawMaterialId);
                    Components.Add(comp);
                }
            }
            OnPropertyChanged(nameof(InitialStockText));
            OnPropertyChanged(nameof(CostPriceText));
            OnPropertyChanged(nameof(SellingPriceText));
                        OnPropertyChanged(nameof(SellingPriceText));
            OnPropertyChanged(nameof(MarkupPercentageText));
            OnPropertyChanged(nameof(BarcodeText));
            GenerateBarcodeImage();
        }
        else
        {
            var generatedSku = $"PRD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            Product = new Product 
            { 
                SKU = generatedSku,
                Barcode = $"{DateTime.UtcNow:yyyyMMdd}{new Random().Next(10000, 99999)}",
                CategoryId = Categories.FirstOrDefault()?.CategoryId ?? 0,
                UnitId = Units.FirstOrDefault()?.UnitId ?? 0,
                IsActive = true
            };
            InitialStock = 0;
            IsNewProduct = true;
            OnPropertyChanged(nameof(InitialStockText));
            OnPropertyChanged(nameof(CostPriceText));
            OnPropertyChanged(nameof(SellingPriceText));
                        OnPropertyChanged(nameof(SellingPriceText));
            OnPropertyChanged(nameof(MarkupPercentageText));
            OnPropertyChanged(nameof(BarcodeText));
            GenerateBarcodeImage();
        }
    }

    private void AddComponent()
    {
        if (SelectedRawMaterial != null && decimal.TryParse(ComponentQuantityText, out decimal qty))
        {
            Components.Add(new ProductComponent
            {
                RawMaterialId = SelectedRawMaterial.ProductId,
                RawMaterial = SelectedRawMaterial,
                QuantityUsed = qty
            });
            ComponentQuantityText = "";
            SelectedRawMaterial = null;
        }
    }

    private void RemoveComponent(ProductComponent comp)
    {
        if (comp != null && Components.Contains(comp))
        {
            Components.Remove(comp);
        }
    }

    private void CalculateCost()
    {
        if (Product == null) return;
        decimal total = 0;
        foreach (var comp in Components)
        {
            total += (comp.RawMaterial?.CostPrice ?? 0) * comp.QuantityUsed;
        }
        Product.CostPrice = total;
        OnPropertyChanged(nameof(CostPriceText));
        OnPropertyChanged(nameof(TotalInitialValueText));
        CalculateSellingPrice();
    }

    private void CalculateSellingPrice()
    {
        if (Product == null) return;
        if (Product.MarkupPercentage > 0)
        {
            Product.SellingPrice = Product.CostPrice + (Product.CostPrice * (Product.MarkupPercentage / 100m));
            OnPropertyChanged(nameof(SellingPriceText));
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Product.ProductName))
            {
                MessageBox.Show("Product Name is required.");
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to {(Product.ProductId == 0 ? "add" : "update")} this product?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            
            if (string.IsNullOrWhiteSpace(Product.SKU))
            {
                Product.SKU = $"PRD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            }
            if (string.IsNullOrWhiteSpace(Product.Barcode))
            {
                Product.Barcode = $"{DateTime.UtcNow:yyyyMMdd}{new Random().Next(10000, 99999)}";
            }

            // Bind components
            Product.Components = Components.ToList();

            if (Product.ProductId == 0)
            {
                var addedProduct = await _productService.AddAsync(Product);
                
                if (InitialStock > 0)
                {
                    var userId = _authService.CurrentUser?.UserId ?? 1;
                    await _inventoryService.AddStockAdjustmentAsync(addedProduct.ProductId, InventoryManagement.Domain.Enums.StockTransactionType.OpeningBalance, InitialStock, "Initial Stock entry", userId);
                }
            }
            else
            {
                await _productService.UpdateAsync(Product);
            }
            
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving product: {ex.Message}");
        }
    }
}




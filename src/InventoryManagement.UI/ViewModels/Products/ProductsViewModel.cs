using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.UI.ViewModels.Base;
using Microsoft.Win32;
using ZXing;
using ZXing.Windows.Compatibility;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
namespace InventoryManagement.UI.ViewModels.Products;

public class ProductsViewModel : ViewModelBase
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IUnitService _unitService;

    public ObservableCollection<Product> Products { get; set; } = new();
    public ObservableCollection<Category> Categories { get; set; } = new();
    public ObservableCollection<InventoryManagement.Domain.Entities.Unit> Units { get; set; } = new();

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

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set { SetProperty(ref _selectedProduct, value); }
    }

    public ICommand SearchCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
        public ICommand DeactivateCommand { get; }
    public ICommand ExportBarcodesCommand { get; }

    public ProductsViewModel(IProductService productService, ICategoryService categoryService, IUnitService unitService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _unitService = unitService;

        SearchCommand = new RelayCommand(async _ => await LoadProductsAsync());
        AddCommand = new RelayCommand(async _ => await AddProductAsync());
        EditCommand = new RelayCommand(async _ => await EditProductAsync(), _ => SelectedProduct != null);
                DeactivateCommand = new RelayCommand(async _ => await DeactivateProductAsync(), _ => SelectedProduct != null);
        ExportBarcodesCommand = new RelayCommand(async _ => await ExportBarcodesAsync());
    }

    public async Task InitializeAsync()
    {
        var cats = await _categoryService.GetAllAsync();
        foreach (var c in cats) Categories.Add(c);
        
        var uns = await _unitService.GetAllAsync();
        foreach (var u in uns) Units.Add(u);

        await LoadProductsAsync();
    }

    private string _emptyMessage = string.Empty;
    public string EmptyMessage
    {
        get => _emptyMessage;
        set { SetProperty(ref _emptyMessage, value); }
    }

    private async Task LoadProductsAsync()
    {
        var criteria = new InventoryManagement.Application.DTOs.Criteria.ProductSearchCriteria
        {
            SearchText = SearchText,
            CategoryId = SelectedFilterCategory?.CategoryId,
            IsActive = true,
            Page = 1,
            PageSize = 1000 // Large page size to preserve existing UI without pagination
        };

        var result = await _productService.SearchProductsAsync(criteria);
        Products.Clear();
        foreach (var p in result.Items)
        {
            Products.Add(p);
        }

        if (!Products.Any())
        {
            EmptyMessage = "No products found.";
        }
        else
        {
            EmptyMessage = string.Empty;
        }
    }

    private async Task AddProductAsync()
    {
        // For phase 3 we are demonstrating the view model capability, a dialog would normally be launched here.
                var editor = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Products.ProductEditorWindow>();
        await ((InventoryManagement.UI.ViewModels.Products.ProductEditorViewModel)editor.DataContext).LoadDataAsync();
        if (editor.ShowDialog() == true)
        {
            await LoadProductsAsync();
        }
    }

        private async Task EditProductAsync()
    {
        if (SelectedProduct == null) return;
        var editor = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Products.ProductEditorWindow>();
        await ((InventoryManagement.UI.ViewModels.Products.ProductEditorViewModel)editor.DataContext).LoadDataAsync(SelectedProduct?.ProductId);
        if (editor.ShowDialog() == true)
        {
            await LoadProductsAsync();
        }
    }

    private async Task DeactivateProductAsync()
    {
        if (SelectedProduct == null) return;
        
        var result = MessageBox.Show($"Are you sure you want to deactivate {SelectedProduct.ProductName}?", "Confirm Deactivate", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await _productService.DeactivateAsync(SelectedProduct.ProductId);
            await LoadProductsAsync();
        }
    }
    private async Task ExportBarcodesAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Barcodes to PDF",
            Filter = "PDF Documents (*.pdf)|*.pdf",
            FileName = "ProductBarcodes_{DateTime.Now:yyyyMMdd}.pdf"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new ZXing.Common.EncodingOptions
                    {
                        Width = 300,
                        Height = 100,
                        Margin = 10
                    }
                };

                QuestPDF.Settings.License = LicenseType.Community;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                        page.PageColor(Colors.White);

                        page.Header().Text("Jeida Crafts & Prints - Product Barcodes").FontSize(20).SemiBold().AlignCenter();

                        page.Content().PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre).Column(col => 
                        {
                            col.Item().Grid(grid =>
                            {
                                grid.Columns(3);
                                grid.Spacing(20);
                                
                                foreach (var product in Products.Where(p => !string.IsNullOrEmpty(p.Barcode)))
                                {
                                    grid.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(itemCol => 
                                    {
                                        using var bitmap = writer.Write(product.Barcode);
                                        using var ms = new MemoryStream();
                                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                        
                                        itemCol.Item().Height(50).Image(ms.ToArray()).FitArea();
                                        itemCol.Item().PaddingTop(5).Text(product.ProductName).AlignCenter().FontSize(10).SemiBold();
                                        itemCol.Item().Text(product.Barcode).AlignCenter().FontSize(9);
                                        itemCol.Item().Text("Price: " + product.SellingPrice.ToString("C")).AlignCenter().FontSize(9).FontColor(Colors.Green.Darken2);
                                    });
                                }
                            });
                        });
                    });
                })
                .GeneratePdf(dialog.FileName);

                MessageBox.Show("Barcodes successfully exported to:\n{dialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting barcodes: {ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}


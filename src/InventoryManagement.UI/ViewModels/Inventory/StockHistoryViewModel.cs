using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.UI.ViewModels.Base;

namespace InventoryManagement.UI.ViewModels.Inventory;

public class StockHistoryItemDto
{
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class StockHistoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;

    public ObservableCollection<StockHistoryItemDto> HistoryItems { get; } = new();

    private Product? _product;
    public Product? Product
    {
        get => _product;
        set => SetProperty(ref _product, value);
    }

    public StockHistoryViewModel(IInventoryService inventoryService, IProductService productService)
    {
        _inventoryService = inventoryService;
        _productService = productService;
    }

    public async Task LoadHistoryAsync(int productId)
    {
        Product = await _productService.GetByIdAsync(productId);
        var history = await _inventoryService.GetStockHistoryAsync(productId);
        
        HistoryItems.Clear();
        foreach (var item in history)
        {
            HistoryItems.Add(new StockHistoryItemDto
            {
                Date = item.TransactionDate,
                TransactionType = FormatTransactionType(item.TransactionType),
                Quantity = item.Quantity,
                Notes = item.Notes ?? string.Empty,
                Reference = item.ReferenceType + (item.ReferenceId.HasValue ? $" #{item.ReferenceId}" : "")
            });
        }
    }

    private string FormatTransactionType(StockTransactionType type)
    {
        return type switch
        {
            StockTransactionType.LegacyAdjustmentIncrease => "Legacy Adjustment Increase",
            StockTransactionType.LegacyAdjustmentDecrease => "Legacy Adjustment Decrease",
            StockTransactionType.ManualAdjustment => "Manual Adjustment",
            StockTransactionType.SalesReturn => "Sales Return",
            StockTransactionType.PurchaseReturn => "Purchase Return",
            StockTransactionType.OpeningBalance => "Opening Balance",
            _ => type.ToString()
        };
    }
}

using System.Windows;
using InventoryManagement.UI.ViewModels.Inventory;

namespace InventoryManagement.UI.Views.Inventory;

public partial class StockHistoryWindow : Window
{
    public StockHistoryWindow(StockHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

using System.Windows.Controls;
using InventoryManagement.UI.ViewModels.Inventory;

namespace InventoryManagement.UI.Views.Inventory;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
        Loaded += async (s, e) => 
        {
            if (DataContext is InventoryViewModel vm)
                await vm.InitializeAsync();
        };
    }
}

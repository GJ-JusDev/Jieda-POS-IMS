using System.Windows.Controls;
using InventoryManagement.UI.ViewModels.Purchasing;

namespace InventoryManagement.UI.Views.Purchasing;

public partial class PurchasesView : UserControl
{
    public PurchasesView()
    {
        InitializeComponent();
        Loaded += async (s, e) => 
        {
            if (DataContext is PurchasesViewModel vm)
                await vm.InitializeAsync();
        };
    }
}

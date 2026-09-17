using System.Windows.Controls;
using InventoryManagement.UI.ViewModels.Products;

namespace InventoryManagement.UI.Views.Products;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        Loaded += async (s, e) => 
        {
            if (DataContext is ProductsViewModel vm)
                await vm.InitializeAsync();
        };
    }
}

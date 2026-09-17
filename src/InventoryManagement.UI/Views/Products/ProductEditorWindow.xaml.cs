using System.Windows;
using InventoryManagement.UI.ViewModels.Products;

namespace InventoryManagement.UI.Views.Products;

public partial class ProductEditorWindow : Window
{
    public ProductEditorWindow(ProductEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseAction = () =>
        {
            DialogResult = true;
            Close();
        };
    }
}

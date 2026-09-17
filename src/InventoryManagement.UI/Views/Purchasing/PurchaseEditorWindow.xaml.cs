using System.Windows;
using InventoryManagement.UI.ViewModels.Purchasing;

namespace InventoryManagement.UI.Views.Purchasing;

public partial class PurchaseEditorWindow : Window
{
    public PurchaseEditorWindow(PurchaseEditorViewModel viewModel)
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

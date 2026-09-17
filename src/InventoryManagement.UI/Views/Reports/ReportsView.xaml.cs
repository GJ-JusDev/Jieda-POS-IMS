using System.Windows.Controls;
using InventoryManagement.UI.ViewModels.Reports;

namespace InventoryManagement.UI.Views.Reports;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        Loaded += async (s, e) => 
        {
            if (DataContext is ReportsViewModel vm)
                await vm.InitializeAsync();
        };
    }
}

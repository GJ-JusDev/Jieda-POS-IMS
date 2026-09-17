using System.Windows.Controls;
using InventoryManagement.UI.ViewModels.Dashboard;

namespace InventoryManagement.UI.Views.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += async (s, e) => 
        {
            if (DataContext is DashboardViewModel vm)
                await vm.InitializeAsync();
        };
    }
}

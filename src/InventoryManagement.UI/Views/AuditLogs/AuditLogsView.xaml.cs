using System.Windows.Controls;
using InventoryManagement.UI.ViewModels.AuditLogs;

namespace InventoryManagement.UI.Views.AuditLogs;

public partial class AuditLogsView : UserControl
{
    public AuditLogsView()
    {
        InitializeComponent();
        Loaded += async (s, e) => 
        {
            if (DataContext is AuditLogsViewModel vm)
                await vm.InitializeAsync();
        };
    }
}

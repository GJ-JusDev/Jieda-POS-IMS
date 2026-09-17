using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.UI.Views.Login;
using InventoryManagement.UI.Views.Products;
using InventoryManagement.UI.ViewModels.Products;
using InventoryManagement.UI.Views.Inventory;
using InventoryManagement.UI.ViewModels.Inventory;
using InventoryManagement.UI.Views.Purchasing;
using InventoryManagement.UI.ViewModels.Purchasing;
using InventoryManagement.UI.Views.Sales;
using InventoryManagement.UI.ViewModels.Sales;
using InventoryManagement.UI.Views.Reports;
using InventoryManagement.UI.ViewModels.Reports;
using InventoryManagement.UI.Views.Settings;
using InventoryManagement.UI.ViewModels.Settings;
using InventoryManagement.UI.Views.AuditLogs;
using InventoryManagement.UI.ViewModels.AuditLogs;
using InventoryManagement.UI.Views.Dashboard;
using InventoryManagement.UI.ViewModels.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.UI;

public partial class MainWindow : Window
{
    private readonly IAuthenticationService _authService;
    private readonly IAuthorizationService _authorizationService;

    public MainWindow(IAuthenticationService authService, IAuthorizationService authorizationService)
    {
        InitializeComponent();
        Loaded += (s, e) => LoadDashboard();
        _authService = authService;
        _authorizationService = authorizationService;
        
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var user = _authService.CurrentUser;
        if (user != null)
        {
            txtUserInfo.Text = $"Welcome, {user.FullName} ({user.Role?.RoleName})";
            
            // Apply basic UI permission checks
            if (!_authorizationService.IsAdministrator())
            {
                btnAdministration.Visibility = Visibility.Collapsed;
                btnAuditLogs.Visibility = Visibility.Collapsed;
            }
        }
    }

            private void BtnDashboard_Click(object sender, RoutedEventArgs e)
    {
        LoadDashboard();
    }

    private void LoadDashboard()
    {
        var dashboardView = new DashboardView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<DashboardViewModel>()
        };
        MainContent.Content = dashboardView;
    }

    private void BtnProducts_Click(object sender, RoutedEventArgs e)
    {
        var productsView = new ProductsView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<ProductsViewModel>()
        };
        MainContent.Content = productsView;
    }

        private void BtnInventory_Click(object sender, RoutedEventArgs e)
    {
        var inventoryView = new InventoryView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryViewModel>()
        };
        MainContent.Content = inventoryView;
    }

        private void BtnPurchasing_Click(object sender, RoutedEventArgs e)
    {
        var purchasingView = new PurchasesView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<PurchasesViewModel>()
        };
        MainContent.Content = purchasingView;
    }

        private void BtnSales_Click(object sender, RoutedEventArgs e)
    {
        var salesView = new SalesView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<SalesViewModel>()
        };
        MainContent.Content = salesView;
    }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
    {
        var reportsView = new ReportsView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<ReportsViewModel>()
        };
        MainContent.Content = reportsView;
    }

        private void BtnAdministration_Click(object sender, RoutedEventArgs e)
    {
        var settingsView = new SettingsView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<SettingsViewModel>()
        };
        MainContent.Content = settingsView;
    }

        private void BtnAuditLogs_Click(object sender, RoutedEventArgs e)
    {
        var auditView = new AuditLogsView
        {
            DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<AuditLogsViewModel>()
        };
        MainContent.Content = auditView;
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _authService.Logout();
        
        // Normally we'd restart the application or show the login window again.
        // For simplicity, we just close the main window which shuts down the app,
        // or we can resolve a new LoginWindow.
        MessageBox.Show("Logged out successfully. The application will now close.", "Logout", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}










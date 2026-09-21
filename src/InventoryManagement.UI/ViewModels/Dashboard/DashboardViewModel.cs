using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.UI.ViewModels.Base;
using InventoryManagement.Application.DTOs.Dashboard;

namespace InventoryManagement.UI.ViewModels.Dashboard;

public class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboardService;

    private DashboardMetricsDto _metrics = new DashboardMetricsDto();
    public DashboardMetricsDto Metrics
    {
        get => _metrics;
        set => SetProperty(ref _metrics, value);
    }

    public ObservableCollection<RecentStockMovementDto> RecentMovements { get; set; } = new();

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        
        RefreshCommand = new RelayCommand(async _ => await LoadDashboardDataAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            var data = await _dashboardService.GetDashboardMetricsAsync();
            Metrics = data;
            
            RecentMovements.Clear();
            foreach(var item in data.RecentMovements)
            {
                RecentMovements.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading dashboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}



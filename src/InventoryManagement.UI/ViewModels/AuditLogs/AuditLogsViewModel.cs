using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.UI.ViewModels.Base;

namespace InventoryManagement.UI.ViewModels.AuditLogs;

public class AuditLogsViewModel : ViewModelBase
{
    private readonly IAuditLogService _auditLogService;

    public ObservableCollection<AuditLog> Logs { get; set; } = new();

    private DateTime? _startDate;
    public DateTime? StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

    private DateTime? _endDate;
    public DateTime? EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

    private string _actionFilter = string.Empty;
    public string ActionFilter { get => _actionFilter; set => SetProperty(ref _actionFilter, value); }

    private string _moduleFilter = string.Empty;
    public string ModuleFilter { get => _moduleFilter; set => SetProperty(ref _moduleFilter, value); }

    private int _currentPage = 1;
    public int CurrentPage 
    { 
        get => _currentPage; 
        set 
        {
            if (SetProperty(ref _currentPage, value) && value > 0)
            {
                _ = LoadLogsAsync();
            }
        }
    }
    
    private const int PageSize = 50;

    public ICommand RefreshCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }

    public AuditLogsViewModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
        RefreshCommand = new RelayCommand(async _ => 
        {
            CurrentPage = 1;
            await LoadLogsAsync();
        });

        NextPageCommand = new RelayCommand(_ => CurrentPage++);
        PrevPageCommand = new RelayCommand(_ => { if (CurrentPage > 1) CurrentPage--; });
    }

    public async Task InitializeAsync()
    {
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            int skip = (CurrentPage - 1) * PageSize;
            var result = await _auditLogService.GetAuditLogsAsync(
                StartDate, EndDate, null, ActionFilter, ModuleFilter, "", skip, PageSize);
            
            Logs.Clear();
            foreach (var log in result)
            {
                Logs.Add(log);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading audit logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

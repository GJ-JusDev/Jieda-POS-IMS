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

    public ICommand RefreshCommand { get; }

    public AuditLogsViewModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
        RefreshCommand = new RelayCommand(async _ => await LoadLogsAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var result = await _auditLogService.GetAuditLogsAsync(100); // load top 100 for performance
            Logs.Clear();
            foreach (var log in result)
            {
                Logs.Add(log);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading audit logs: {ex.Message}");
        }
    }
}

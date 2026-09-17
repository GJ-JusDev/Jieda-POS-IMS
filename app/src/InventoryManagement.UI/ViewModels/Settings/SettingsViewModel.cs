using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Microsoft.Win32;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.UI.ViewModels.Base;

namespace InventoryManagement.UI.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IAuthenticationService _authService;
    private readonly IAuthorizationService _authzService;

    public ICommand ManualBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }

    public SettingsViewModel(IBackupService backupService, IAuthenticationService authService, IAuthorizationService authzService)
    {
        _backupService = backupService;
        _authService = authService;
        _authzService = authzService;

        ManualBackupCommand = new RelayCommand(async _ => await ManualBackupAsync(), _ => _authzService.IsAdministrator());
        RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync(), _ => _authzService.IsAdministrator());
    }

    private async Task ManualBackupAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Backup",
            Filter = "SQLite Database (*.db)|*.db|Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
            FileName = $"Inventory_ManualBackup_{DateTime.Now:yyyyMMdd}.db"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _backupService.CreateManualBackupAsync(dialog.FileName);
                MessageBox.Show($"Backup successfully saved to:\n{dialog.FileName}", "Backup Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating backup: {ex.Message}", "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task RestoreBackupAsync()
    {
        var msgResult = MessageBox.Show(
            "WARNING: Restoring a backup will completely overwrite your current database. A pre-restore safety backup will be created automatically.\n\nAre you sure you want to proceed?", 
            "Confirm Restore", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (msgResult != MessageBoxResult.Yes) return;

        var dialog = new OpenFileDialog
        {
            Title = "Select Backup File",
            Filter = "SQLite Database (*.db)|*.db|Backup Files (*.bak)|*.bak|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _backupService.RestoreBackupAsync(dialog.FileName);
                MessageBox.Show("Database successfully restored.\nThe application will now close. Please restart the application to reload data.", "Restore Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Typically you force restart here. Closing the app is the safest way.
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring backup: {ex.Message}", "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}


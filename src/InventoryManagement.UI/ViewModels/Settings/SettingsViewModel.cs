using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Extensions.Configuration;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Application.Services;
using InventoryManagement.Infrastructure.Services;
using InventoryManagement.UI.ViewModels.Base;

namespace InventoryManagement.UI.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IAuthenticationService _authService;
    private readonly IAuthorizationService _authzService;
    private readonly ISystemDiagnosticsService _diagnosticsService;
    private readonly IUserConfigurationService _configService;
    private readonly IConfiguration _configuration;
    private readonly BarcodeScannerOptions _barcodeOptions;

    public ICommand ManualBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand RunDiagnosticsCommand { get; }
    public ICommand SaveBarcodeSettingsCommand { get; }
    public ICommand ResetBarcodeSettingsCommand { get; }

    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = new();

    private bool _isDiagnosticsRunning;
    public bool IsDiagnosticsRunning
    {
        get => _isDiagnosticsRunning;
        set 
        {
            if (SetProperty(ref _isDiagnosticsRunning, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private int _minBarcodeLength;
    public int MinBarcodeLength
    {
        get => _minBarcodeLength;
        set => SetProperty(ref _minBarcodeLength, value);
    }

    private int _maxTimeBetweenKeystrokesMs;
    public int MaxTimeBetweenKeystrokesMs
    {
        get => _maxTimeBetweenKeystrokesMs;
        set => SetProperty(ref _maxTimeBetweenKeystrokesMs, value);
    }

    private bool _requireTerminatingEnter;
    public bool RequireTerminatingEnter
    {
        get => _requireTerminatingEnter;
        set => SetProperty(ref _requireTerminatingEnter, value);
    }

    public SettingsViewModel(IBackupService backupService, IAuthenticationService authService, 
        IAuthorizationService authzService, ISystemDiagnosticsService diagnosticsService, 
        IUserConfigurationService configService, IConfiguration configuration, BarcodeScannerOptions barcodeOptions)
    {
        _backupService = backupService;
        _authService = authService;
        _authzService = authzService;
        _diagnosticsService = diagnosticsService;
        _configService = configService;
        _configuration = configuration;
        _barcodeOptions = barcodeOptions;

        MinBarcodeLength = _barcodeOptions.MinBarcodeLength;
        MaxTimeBetweenKeystrokesMs = _barcodeOptions.MaxTimeBetweenKeystrokesMs;
        RequireTerminatingEnter = _barcodeOptions.RequireTerminatingEnter;

        ManualBackupCommand = new RelayCommand(async _ => await ManualBackupAsync(), _ => _authzService.IsAdministrator());
        RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync(), _ => _authzService.IsAdministrator());
        RunDiagnosticsCommand = new RelayCommand(async _ => await RunDiagnosticsAsync(), _ => !IsDiagnosticsRunning);
        SaveBarcodeSettingsCommand = new RelayCommand(_ => SaveBarcodeSettings(), _ => _authzService.IsAdministrator());
        ResetBarcodeSettingsCommand = new RelayCommand(_ => ResetBarcodeSettings(), _ => _authzService.IsAdministrator());
    }

    private async Task RunDiagnosticsAsync()
    {
        IsDiagnosticsRunning = true;
        Diagnostics.Clear();
        try
        {
            var results = await _diagnosticsService.RunDiagnosticsAsync();
            foreach (var item in results)
            {
                Diagnostics.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Diagnostics failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDiagnosticsRunning = false;
        }
    }

    private void ResetBarcodeSettings()
    {
        var msgResult = MessageBox.Show(
            "Are you sure you want to reset barcode settings to system defaults?", 
            "Confirm Reset", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Question);

        if (msgResult != MessageBoxResult.Yes) return;

        var defaults = new BarcodeScannerOptions();
        _configuration.GetSection("BarcodeScanner").Bind(defaults);

        MinBarcodeLength = defaults.MinBarcodeLength;
        MaxTimeBetweenKeystrokesMs = defaults.MaxTimeBetweenKeystrokesMs;
        RequireTerminatingEnter = defaults.RequireTerminatingEnter;

        SaveBarcodeSettings();
    }

    private void SaveBarcodeSettings()
    {
        if (MinBarcodeLength <= 0)
        {
            MessageBox.Show("Minimum barcode length must be greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MaxTimeBetweenKeystrokesMs <= 0 || MaxTimeBetweenKeystrokesMs > 5000)
        {
            MessageBox.Show("Max time between keystrokes must be between 1 and 5000 ms.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _barcodeOptions.MinBarcodeLength = MinBarcodeLength;
        _barcodeOptions.MaxTimeBetweenKeystrokesMs = MaxTimeBetweenKeystrokesMs;
        _barcodeOptions.RequireTerminatingEnter = RequireTerminatingEnter;

        try
        {
            _configService.SaveBarcodeScannerOptions(_barcodeOptions);
            MessageBox.Show("Barcode settings saved successfully. They will apply immediately.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save user configuration: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring backup: {ex.Message}", "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

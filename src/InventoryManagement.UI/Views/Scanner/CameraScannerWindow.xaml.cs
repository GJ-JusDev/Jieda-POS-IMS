using System;
using System.Windows;
using InventoryManagement.UI.ViewModels.Base;

namespace InventoryManagement.UI.Views.Scanner;

public partial class CameraScannerWindow : Window
{
    private CameraScannerViewModel? _viewModel;

    public CameraScannerWindow()
    {
        InitializeComponent();
        Loaded += CameraScannerWindow_Loaded;
        Closing += CameraScannerWindow_Closing;
    }

    private async void CameraScannerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as CameraScannerViewModel;
        if (_viewModel != null)
        {
            _viewModel.CloseAction = Close;
            await _viewModel.InitializeAsync();
        }
    }

    private async void CameraScannerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.DisposeAsync();
        }
    }
}

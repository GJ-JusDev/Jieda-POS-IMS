using System.Windows;
using InventoryManagement.UI.ViewModels.Inventory;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.UI.Views.Inventory;

public partial class StockAdjustmentWindow : Window
{
    private readonly IBarcodeScannerService _scannerService;

    public StockAdjustmentWindow(StockAdjustmentViewModel viewModel, IBarcodeScannerService scannerService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _scannerService = scannerService;
        
        viewModel.CloseAction = () =>
        {
            DialogResult = true;
            Close();
        };

        PreviewTextInput += StockAdjustmentWindow_PreviewTextInput;
        PreviewKeyDown += StockAdjustmentWindow_PreviewKeyDown;
        _scannerService.BarcodeScanned += ScannerService_BarcodeScanned;
    }

    private void StockAdjustmentWindow_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        _scannerService.ProcessInput(e.Text);
    }

    private void StockAdjustmentWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
        {
            if (_scannerService.ProcessEnter(out string barcode))
            {
                e.Handled = true;
                
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox txt)
                {
                    if (txt.Text.EndsWith(barcode))
                    {
                        txt.Text = txt.Text.Substring(0, txt.Text.Length - barcode.Length);
                        txt.CaretIndex = txt.Text.Length;
                    }
                }
            }
        }
    }

    private void ScannerService_BarcodeScanned(object? sender, string barcode)
    {
        if (DataContext is IBarcodeScannerTarget target)
        {
            target.OnBarcodeScanned(barcode);
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _scannerService.BarcodeScanned -= ScannerService_BarcodeScanned;
        base.OnClosed(e);
    }
}

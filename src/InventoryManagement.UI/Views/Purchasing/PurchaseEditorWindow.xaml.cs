using System.Windows;
using InventoryManagement.UI.ViewModels.Purchasing;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.UI.Views.Purchasing;

public partial class PurchaseEditorWindow : Window
{
    private readonly IBarcodeScannerService _scannerService;

    public PurchaseEditorWindow(PurchaseEditorViewModel viewModel, IBarcodeScannerService scannerService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _scannerService = scannerService;
        
        viewModel.CloseAction = () =>
        {
            DialogResult = true;
            Close();
        };

        PreviewTextInput += PurchaseEditorWindow_PreviewTextInput;
        PreviewKeyDown += PurchaseEditorWindow_PreviewKeyDown;
        _scannerService.BarcodeScanned += ScannerService_BarcodeScanned;
    }

    private void PurchaseEditorWindow_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        _scannerService.ProcessInput(e.Text);
    }

    private void PurchaseEditorWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
        {
            if (_scannerService.ProcessEnter(out string barcode))
            {
                e.Handled = true; // Prevent Enter on focused control
                
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
        // Route to the active view model if it supports barcode scanning
        if (DataContext is IBarcodeScannerTarget target)
        {
            // Detach to avoid double firing if MainWindow is also listening (though MainWindow is disabled if this is modal)
            target.OnBarcodeScanned(barcode);
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _scannerService.BarcodeScanned -= ScannerService_BarcodeScanned;
        base.OnClosed(e);
    }
}

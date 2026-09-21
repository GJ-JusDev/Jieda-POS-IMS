namespace InventoryManagement.Application.Interfaces;

public interface IBarcodeScannerTarget
{
    void OnBarcodeScanned(string barcode);
}

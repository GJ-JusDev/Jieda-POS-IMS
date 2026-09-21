using System;

namespace InventoryManagement.Application.Interfaces;

public interface IBarcodeScannerService
{
    event EventHandler<string> BarcodeScanned;
    void ProcessInput(string input);
    bool ProcessEnter(out string barcode);
}

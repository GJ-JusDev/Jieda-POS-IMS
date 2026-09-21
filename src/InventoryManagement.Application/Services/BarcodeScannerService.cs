using System;
using System.Text;

namespace InventoryManagement.Application.Services;

public class BarcodeScannerOptions
{
    public int MaxTimeBetweenKeystrokesMs { get; set; } = 100;
    public int MinBarcodeLength { get; set; } = 4;
    public bool RequireTerminatingEnter { get; set; } = true;
}

public class BarcodeScannerService : InventoryManagement.Application.Interfaces.IBarcodeScannerService
{
    private readonly BarcodeScannerOptions _options;
    private StringBuilder _buffer = new StringBuilder();
    private DateTime _lastKeystroke = DateTime.MinValue;

    public event EventHandler<string>? BarcodeScanned;

    public BarcodeScannerService(BarcodeScannerOptions options)
    {
        _options = options;
    }

    public void ProcessInput(string input)
    {
        var now = DateTime.Now;
        if ((now - _lastKeystroke).TotalMilliseconds > _options.MaxTimeBetweenKeystrokesMs)
        {
            _buffer.Clear();
        }
        
        _buffer.Append(input);
        _lastKeystroke = now;
    }

    public bool ProcessEnter(out string barcode)
    {
        barcode = string.Empty;
        var now = DateTime.Now;
        // Verify that the Enter key came immediately after the rapid text
        if ((now - _lastKeystroke).TotalMilliseconds <= _options.MaxTimeBetweenKeystrokesMs)
        {
            if (_buffer.Length >= _options.MinBarcodeLength)
            {
                barcode = _buffer.ToString();
                BarcodeScanned?.Invoke(this, barcode);
                _buffer.Clear();
                return true;
            }
        }
        _buffer.Clear();
        return false;
    }
}

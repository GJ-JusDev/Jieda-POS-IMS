using System;
using System.Threading;
using Xunit;
using InventoryManagement.Application.Services;

namespace InventoryManagement.Tests;

public class BarcodeScannerTests
{
    [Fact]
    public void ProcessEnter_WithRapidInput_TriggersBarcodeScannedAndReturnsTrue()
    {
        // Arrange
        var options = new BarcodeScannerOptions { MaxTimeBetweenKeystrokesMs = 100, MinBarcodeLength = 3 };
        var scanner = new BarcodeScannerService(options);
        
        string scannedBarcode = null;
        scanner.BarcodeScanned += (sender, barcode) => { scannedBarcode = barcode; };

        // Act
        scanner.ProcessInput("1");
        scanner.ProcessInput("2");
        scanner.ProcessInput("3");
        bool isBarcode = scanner.ProcessEnter(out string returnedBarcode);

        // Assert
        Assert.True(isBarcode);
        Assert.Equal("123", scannedBarcode);
        Assert.Equal("123", returnedBarcode);
    }

    [Fact]
    public void ProcessEnter_WithSlowInput_DoesNotTriggerBarcodeScannedAndReturnsFalse()
    {
        // Arrange
        var options = new BarcodeScannerOptions { MaxTimeBetweenKeystrokesMs = 50, MinBarcodeLength = 3 };
        var scanner = new BarcodeScannerService(options);
        
        string scannedBarcode = null;
        scanner.BarcodeScanned += (sender, barcode) => { scannedBarcode = barcode; };

        // Act
        scanner.ProcessInput("A");
        Thread.Sleep(60); // Simulate slow typing
        scanner.ProcessInput("B");
        scanner.ProcessInput("C");
        bool isBarcode = scanner.ProcessEnter(out string returnedBarcode);

        // Assert
        Assert.False(isBarcode);
        Assert.Null(scannedBarcode);
        Assert.Equal(string.Empty, returnedBarcode);
    }

    [Fact]
    public void ProcessEnter_WithShortInput_DoesNotTriggerBarcodeScanned()
    {
        // Arrange
        var options = new BarcodeScannerOptions { MaxTimeBetweenKeystrokesMs = 50, MinBarcodeLength = 5 };
        var scanner = new BarcodeScannerService(options);
        
        string scannedBarcode = null;
        scanner.BarcodeScanned += (sender, barcode) => { scannedBarcode = barcode; };

        // Act
        scanner.ProcessInput("1");
        scanner.ProcessInput("2");
        scanner.ProcessInput("3");
        bool isBarcode = scanner.ProcessEnter(out string returnedBarcode);

        // Assert
        Assert.False(isBarcode);
        Assert.Null(scannedBarcode);
    }

    [Fact]
    public void ProcessInput_PreservesLeadingZerosAndAlphabeticCharacters()
    {
        // Arrange
        var options = new BarcodeScannerOptions { MaxTimeBetweenKeystrokesMs = 100, MinBarcodeLength = 3 };
        var scanner = new BarcodeScannerService(options);
        
        string scannedBarcode = null;
        scanner.BarcodeScanned += (sender, barcode) => { scannedBarcode = barcode; };

        // Act
        scanner.ProcessInput("0");
        scanner.ProcessInput("0");
        scanner.ProcessInput("A");
        scanner.ProcessInput("B");
        scanner.ProcessInput("C");
        bool isBarcode = scanner.ProcessEnter(out string returnedBarcode);

        // Assert
        Assert.True(isBarcode);
        Assert.Equal("00ABC", scannedBarcode);
    }

    [Fact]
    public void ProcessEnter_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var options = new BarcodeScannerOptions { MaxTimeBetweenKeystrokesMs = 100, MinBarcodeLength = 3 };
        var scanner = new BarcodeScannerService(options);

        // Act
        bool isBarcode = scanner.ProcessEnter(out string returnedBarcode);

        // Assert
        Assert.False(isBarcode);
        Assert.Equal(string.Empty, returnedBarcode);
    }
}

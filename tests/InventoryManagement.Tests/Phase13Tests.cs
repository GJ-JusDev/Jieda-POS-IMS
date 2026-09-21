using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Infrastructure.Data;
using InventoryManagement.Infrastructure.Services;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Tests;

public class Phase13Tests
{
    private InventoryDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={dbName}")
            .Options;

        var context = new InventoryDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void BarcodeScanner_RespectsMinimumLength()
    {
        var options = new BarcodeScannerOptions { MinBarcodeLength = 4, MaxTimeBetweenKeystrokesMs = 100 };
        var service = new BarcodeScannerService(options);
        
        // Input 3 chars rapidly
        service.ProcessInput("1");
        service.ProcessInput("2");
        service.ProcessInput("3");
        
        var result = service.ProcessEnter(out var barcode);
        Assert.False(result); // Should reject because length < 4
        Assert.Empty(barcode);
    }

    [Fact]
    public void BarcodeScanner_AcceptsValidLength()
    {
        var options = new BarcodeScannerOptions { MinBarcodeLength = 4, MaxTimeBetweenKeystrokesMs = 100 };
        var service = new BarcodeScannerService(options);
        
        service.ProcessInput("1");
        service.ProcessInput("2");
        service.ProcessInput("3");
        service.ProcessInput("4");
        
        var result = service.ProcessEnter(out var barcode);
        Assert.True(result);
        Assert.Equal("1234", barcode);
    }

    [Fact]
    public void BarcodeScanner_RejectsSlowTyping()
    {
        var options = new BarcodeScannerOptions { MinBarcodeLength = 4, MaxTimeBetweenKeystrokesMs = 10 }; // very tight
        var service = new BarcodeScannerService(options);
        
        service.ProcessInput("1");
        System.Threading.Thread.Sleep(20); // wait longer than max time
        service.ProcessInput("2");
        service.ProcessInput("3");
        service.ProcessInput("4");
        
        var result = service.ProcessEnter(out var barcode);
        // The first character should be cleared due to timeout, so length is only 3
        Assert.False(result); 
    }
    
    [Fact]
    public async Task Diagnostics_DetectsValidDatabase()
    {
        var dbFile = "TestDiagnostics.db";
        var db = CreateDbContext(dbFile);
        
        var diagnostics = new SystemDiagnosticsService(db);
        var result = await diagnostics.RunDiagnosticsAsync();
        
        var exists = result.First(r => r.Name == "Database Exists");
        Assert.Equal("PASS", exists.Status);
        
        var integrity = result.First(r => r.Name == "SQLite Integrity Status");
        Assert.Equal("PASS", integrity.Status);
        
        db.Dispose();
        db.Dispose();
    }
}

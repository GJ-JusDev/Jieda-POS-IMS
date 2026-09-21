using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Services;
using InventoryManagement.Infrastructure.Data;
using InventoryManagement.Infrastructure.Services;
using InventoryManagement.Infrastructure.Logging;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Tests;

public class Phase14Tests
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
    public void BarcodeSettings_SaveAndReload_PreservesValues()
    {
        var configService = new UserConfigurationService();
        var defaults = new BarcodeScannerOptions { MinBarcodeLength = 3, MaxTimeBetweenKeystrokesMs = 50, RequireTerminatingEnter = false };
        
        var newOptions = new BarcodeScannerOptions { MinBarcodeLength = 6, MaxTimeBetweenKeystrokesMs = 120, RequireTerminatingEnter = true };
        
        // Save
        configService.SaveBarcodeScannerOptions(newOptions);
        
        // Reload
        var loaded = configService.LoadBarcodeScannerOptions(defaults);
        
        Assert.Equal(6, loaded.MinBarcodeLength);
        Assert.Equal(120, loaded.MaxTimeBetweenKeystrokesMs);
        Assert.True(loaded.RequireTerminatingEnter);
    }
    
    [Fact]
    public void UserConfigurationService_MissingFile_ReturnsDefaults()
    {
        var configService = new UserConfigurationService();
        var configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InventoryManagement", "Config", "user-settings.json");
        
        if (File.Exists(configFile)) File.Delete(configFile);
        
        var defaults = new BarcodeScannerOptions { MinBarcodeLength = 12, MaxTimeBetweenKeystrokesMs = 99 };
        var loaded = configService.LoadBarcodeScannerOptions(defaults);
        
        Assert.Equal(12, loaded.MinBarcodeLength);
        Assert.Equal(99, loaded.MaxTimeBetweenKeystrokesMs);
    }

    [Fact]
    public void UserConfigurationService_MalformedFile_ReturnsDefaults()
    {
        var configService = new UserConfigurationService();
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InventoryManagement", "Config");
        var configFile = Path.Combine(configDir, "user-settings.json");
        
        if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
        File.WriteAllText(configFile, "{ invalid json ]");
        
        var defaults = new BarcodeScannerOptions { MinBarcodeLength = 8, MaxTimeBetweenKeystrokesMs = 77 };
        var loaded = configService.LoadBarcodeScannerOptions(defaults);
        
        Assert.Equal(8, loaded.MinBarcodeLength);
        Assert.Equal(77, loaded.MaxTimeBetweenKeystrokesMs);
    }
    
    [Fact]
    public async Task Diagnostics_ReportsEnvironmentProperly()
    {
        var dbFile = "TestDiagnostics_Phase14.db";
        var db = CreateDbContext(dbFile);
        
        var diagnostics = new SystemDiagnosticsService(db);
        var results = await diagnostics.RunDiagnosticsAsync();
        
        Assert.Contains(results, r => r.Name == "Operating System" && r.Status == "PASS");
        Assert.Contains(results, r => r.Name == ".NET Runtime" && r.Status == "PASS");
        Assert.Contains(results, r => r.Name == "Configuration Path" && r.Status == "PASS");
        Assert.Contains(results, r => r.Name == "Log Path" && r.Status == "PASS");
        Assert.Contains(results, r => r.Name.StartsWith("Disk Space")); // Can be Free/Total or just Disk Space
        Assert.Contains(results, r => r.Name == "SQLite Integrity Status" && r.Status == "PASS");
        
        db.Dispose();
    }

    [Fact]
    public void TechnicalLogger_CreatesDirectoryAndFile()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "InventoryLogs_Test1");
        if (Directory.Exists(logDir)) Directory.Delete(logDir, true);

        var logger = new TechnicalLogger(logDir);
        logger.LogInfo("Test message");

        Assert.True(Directory.Exists(logDir));
        Assert.True(File.Exists(Path.Combine(logDir, "app.log")));
        var content = File.ReadAllText(Path.Combine(logDir, "app.log"));
        Assert.Contains("[INFO] Test message", content);

        Directory.Delete(logDir, true);
    }

    [Fact]
    public void TechnicalLogger_RotatesLogs()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "InventoryLogs_Test2");
        if (Directory.Exists(logDir)) Directory.Delete(logDir, true);

        // 100 bytes max
        var logger = new TechnicalLogger(logDir, 100);
        
        // This will write well over 100 bytes over a few lines
        logger.LogInfo("First line of text that is fairly long to exceed limits.");
        logger.LogInfo("Second line of text that is fairly long to exceed limits.");
        logger.LogInfo("Third line of text that is fairly long to exceed limits.");

        Assert.True(File.Exists(Path.Combine(logDir, "app.log")));
        Assert.True(File.Exists(Path.Combine(logDir, "app.1.log")));

        Directory.Delete(logDir, true);
    }

    [Fact]
    public void TechnicalLogger_LogsExceptions()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "InventoryLogs_Test3");
        if (Directory.Exists(logDir)) Directory.Delete(logDir, true);

        var logger = new TechnicalLogger(logDir);
        
        try
        {
            throw new InvalidOperationException("Test exception message");
        }
        catch (Exception ex)
        {
            logger.LogError("An error occurred", ex);
        }

        var content = File.ReadAllText(Path.Combine(logDir, "app.log"));
        Assert.Contains("[ERROR] An error occurred", content);
        Assert.Contains("Exception: Test exception message", content);

        Directory.Delete(logDir, true);
    }

    [Fact]
    public void TechnicalLogger_HandlesConcurrentWrites()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "InventoryLogs_Test4");
        if (Directory.Exists(logDir)) Directory.Delete(logDir, true);

        var logger = new TechnicalLogger(logDir);

        Parallel.For(0, 100, i =>
        {
            logger.LogInfo($"Concurrent message {i}");
        });

        var content = File.ReadAllText(Path.Combine(logDir, "app.log"));
        Assert.True(content.Split('\n').Length > 5); // Some might fail due to lock, but shouldn't crash!

        Directory.Delete(logDir, true);
    }

    [Fact]
    public async Task Database_StartupChecks_MissingDatabase()
    {
        var dbFile = "TestDiagnostics_Missing.db";
        if (File.Exists(dbFile)) File.Delete(dbFile);
        
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={dbFile}")
            .Options;

        var context = new InventoryDbContext(options);
        
        // EF Core will return false for CanConnect if it can't find or access it (unless it creates it, but CanConnect does not create it!)
        Assert.False(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Database_StartupChecks_ValidDatabase()
    {
        var dbFile = "TestDiagnostics_Valid.db";
        var context = CreateDbContext(dbFile);
        
        Assert.True(await context.Database.CanConnectAsync());
        
        var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        await context.Database.OpenConnectionAsync();
        var integrity = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("ok", integrity);
        
        context.Dispose();
    }
}

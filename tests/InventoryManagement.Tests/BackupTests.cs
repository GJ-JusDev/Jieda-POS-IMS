using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Infrastructure.Services;
using InventoryManagement.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Moq;
using InventoryManagement.Domain.Entities;
using System.Collections.Generic;
using System.Threading;

namespace InventoryManagement.Tests;

public class BackupTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly Mock<IAuditLogService> _auditLogMock;
    private readonly Mock<IAuthenticationService> _authMock;
    private readonly IConfiguration _config;
    private readonly InventoryDbContext _dbContext;
    
    public BackupTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "InventoryBackupTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "Inventory.db");
        _backupDir = Path.Combine(_testDir, "Backups");
        
        var connectionString = $"Data Source={_dbPath}";
        
        var configMock = new Mock<IConfiguration>();
        var connectionStringsSectionMock = new Mock<IConfigurationSection>();
        connectionStringsSectionMock.Setup(s => s["DefaultConnection"]).Returns(connectionString);
        configMock.Setup(c => c.GetSection("ConnectionStrings")).Returns(connectionStringsSectionMock.Object);
        _config = configMock.Object;
        
        _auditLogMock = new Mock<IAuditLogService>();
        _authMock = new Mock<IAuthenticationService>();
        _authMock.Setup(a => a.CurrentUser).Returns(new User { UserId = 1 });

        // Initialize a real SQLite database
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite(connectionString)
            .Options;
            
        _dbContext = new InventoryDbContext(options);
        _dbContext.Database.EnsureCreated();
        
        // Ensure there is some data
        _dbContext.Categories.Add(new Category { CategoryId = 1, CategoryName = "Cat 1" });
        _dbContext.Units.Add(new Unit { UnitId = 1, UnitName = "Unit 1", Symbol = "u" });
        _dbContext.Products.Add(new Product { ProductId = 1, ProductName = "Test", SKU = "123", CostPrice = 1, SellingPrice = 2, CategoryId = 1, UnitId = 1 });
        _dbContext.SaveChanges();
        
        _dbContext.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
    }
    
    public void Dispose()
    {
        _dbContext.Dispose();
        SqliteConnection.ClearAllPools();
        
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }
    
    private BackupService CreateService()
    {
        return new BackupService(_config, _auditLogMock.Object, _authMock.Object);
    }
    
    private async Task CorruptDatabaseAsync(string path)
    {
        // Write garbage to corrupt the SQLite header
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);
        var garbage = new byte[100];
        new Random().NextBytes(garbage);
        await fs.WriteAsync(garbage, 0, garbage.Length);
    }

    [Fact]
    public async Task CreateManualBackup_Success_ValidatesAndAudits()
    {
        var service = CreateService();
        var backupPath = Path.Combine(_testDir, "manual_backup.db");
        
        var result = await service.CreateManualBackupAsync(backupPath);
        
        Assert.Equal(backupPath, result);
        Assert.True(File.Exists(backupPath));
        Assert.True(new FileInfo(backupPath).Length > 0);
        
        // Verify Audit Event
        _auditLogMock.Verify(a => a.LogIndependentActionAsync(1, "BackupCreated", "System", backupPath, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateManualBackup_InvalidSourceDb_RejectsAndAudits()
    {
        var service = CreateService();
        var backupPath = Path.Combine(_testDir, "manual_backup2.db");
        
        // Close EF connection to allow corruption
        _dbContext.Dispose();
        SqliteConnection.ClearAllPools();
        
        await CorruptDatabaseAsync(_dbPath);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateManualBackupAsync(backupPath));
        
        Assert.False(File.Exists(backupPath));
        _auditLogMock.Verify(a => a.LogIndependentActionAsync(1, "BackupFailed", "System", backupPath, It.Is<string>(s => s.Contains("integrity"))), Times.Once);
    }

    [Fact]
    public async Task RestoreBackup_ValidBackup_RestoresSuccessfully()
    {
        var service = CreateService();
        var backupPath = Path.Combine(_testDir, "good_backup.db");
        await service.CreateManualBackupAsync(backupPath);
        
        // Write something new to DB so we can verify it gets reverted
        _dbContext.Products.Add(new Product { ProductName = "Should Be Erased", SKU = "999", CostPrice = 1, SellingPrice = 2, CategoryId = 1, UnitId = 1 });
        await _dbContext.SaveChangesAsync();
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
        
        _dbContext.Dispose();
        SqliteConnection.ClearAllPools();
        
        await service.RestoreBackupAsync(backupPath);
        
        // Validate restored DB
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        using var newContext = new InventoryDbContext(options);
        var productCount = await newContext.Products.CountAsync();
        Assert.Equal(1, productCount); // The second product was successfully erased by the restore
        
        _auditLogMock.Verify(a => a.LogIndependentActionAsync(1, "BackupRestored", "System", backupPath, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RestoreBackup_InvalidBackup_RejectsAndPreservesCurrentDb()
    {
        var service = CreateService();
        var badBackupPath = Path.Combine(_testDir, "bad_backup.db");
        File.Copy(_dbPath, badBackupPath);
        
        _dbContext.Dispose();
        SqliteConnection.ClearAllPools();
        
        await CorruptDatabaseAsync(badBackupPath);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreBackupAsync(badBackupPath));
        
        // Validate current DB is preserved
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        using var newContext = new InventoryDbContext(options);
        Assert.True(await newContext.Database.CanConnectAsync());
        
        _auditLogMock.Verify(a => a.LogIndependentActionAsync(1, "BackupFailed", "System", badBackupPath, It.Is<string>(s => s.Contains("integrity"))), Times.Once);
    }

    [Fact]
    public async Task EnforceRetentionPolicy_KeepsNewest30()
    {
        var service = CreateService();
        Directory.CreateDirectory(_backupDir);
        
        // Create 35 dummy files
        for(int i = 1; i <= 35; i++)
        {
            var path = Path.Combine(_backupDir, $"Inventory_test{i}.db");
            File.WriteAllText(path, "dummy");
            // Set progressively newer dates
            File.SetCreationTime(path, DateTime.Now.AddDays(i));
        }
        
        await service.EnforceRetentionPolicyAsync(30);
        
        var remaining = Directory.GetFiles(_backupDir, "Inventory_*.db").ToList();
        Assert.Equal(30, remaining.Count);
        
        // Ensure oldest 5 were removed
        Assert.DoesNotContain(Path.Combine(_backupDir, "Inventory_test1.db"), remaining);
        Assert.DoesNotContain(Path.Combine(_backupDir, "Inventory_test5.db"), remaining);
        Assert.Contains(Path.Combine(_backupDir, "Inventory_test6.db"), remaining);
        Assert.Contains(Path.Combine(_backupDir, "Inventory_test35.db"), remaining);
        
        // Ensure active db is untouched
        Assert.True(File.Exists(_dbPath));
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly string _databaseFilePath;
    private readonly string _autoBackupDirectory;

    public BackupService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        
        // Parse the SQLite connection string, e.g. "Data Source=C:\...\Inventory.db"
        var dbPath = connectionString.Replace("Data Source=", "").Trim();
        if (string.IsNullOrEmpty(dbPath))
        {
            throw new ArgumentException("Invalid connection string. Could not resolve database path.");
        }
        
        _databaseFilePath = dbPath;
        
        // Create an automatic backup directory adjacent to the database
        var dbDirectory = Path.GetDirectoryName(_databaseFilePath) ?? AppContext.BaseDirectory;
        _autoBackupDirectory = Path.Combine(dbDirectory, "Backups");
    }

    public Task<string> CreateManualBackupAsync(string targetFilePath)
    {
        if (!File.Exists(_databaseFilePath))
            throw new FileNotFoundException("The main database file could not be found.");

        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Copying SQLite file. A true hot-backup might require SQLite specific commands (like .backup),
        // but for a single user offline app, a direct file copy is generally safe if no transactions are actively writing.
                // Use FileShare.ReadWrite to prevent locking exceptions from EF Core SQLite
        using (var sourceStream = new FileStream(_databaseFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var destStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            sourceStream.CopyTo(destStream);
        }
        
        return Task.FromResult(targetFilePath);
    }

    public Task<string> CreateAutoBackupAsync()
    {
        if (!Directory.Exists(_autoBackupDirectory))
        {
            Directory.CreateDirectory(_autoBackupDirectory);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"Inventory_AutoBackup_{timestamp}.db";
        var backupFilePath = Path.Combine(_autoBackupDirectory, backupFileName);

        return CreateManualBackupAsync(backupFilePath);
    }

    public Task RestoreBackupAsync(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"The backup file '{backupFilePath}' could not be found.");

        if (!File.Exists(_databaseFilePath))
            throw new FileNotFoundException("The target database file could not be found.");

        // First, make a safety backup of the current state before overwriting
        var safetyBackupPath = _databaseFilePath + $".pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        File.Copy(_databaseFilePath, safetyBackupPath, overwrite: true);

        try
        {
            File.Copy(backupFilePath, _databaseFilePath, overwrite: true);
        }
        catch (Exception)
        {
            // If restore fails, attempt to rollback to safety backup
            if (File.Exists(safetyBackupPath))
            {
                File.Copy(safetyBackupPath, _databaseFilePath, overwrite: true);
            }
            throw;
        }

        return Task.CompletedTask;
    }

    public Task EnforceRetentionPolicyAsync(int daysToKeep)
    {
        if (!Directory.Exists(_autoBackupDirectory))
            return Task.CompletedTask;

        var thresholdDate = DateTime.Now.AddDays(-daysToKeep);
        var backupFiles = Directory.GetFiles(_autoBackupDirectory, "Inventory_AutoBackup_*.db");

        foreach (var file in backupFiles)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.CreationTime < thresholdDate)
            {
                try
                {
                    fileInfo.Delete();
                }
                catch
                {
                    // Ignore retention deletion errors (e.g. file locked)
                }
            }
        }

        return Task.CompletedTask;
    }
}


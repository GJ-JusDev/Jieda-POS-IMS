using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly string _databaseFilePath;
    private readonly string _autoBackupDirectory;
    private readonly IAuditLogService _auditLogService;
    private readonly IAuthenticationService _authService;

    public BackupService(IConfiguration configuration, IAuditLogService auditLogService, IAuthenticationService authService)
    {
        _auditLogService = auditLogService;
        _authService = authService;
        
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        
        var dbPath = connectionString.Replace("Data Source=", "").Trim();
        if (string.IsNullOrEmpty(dbPath))
        {
            throw new ArgumentException("Invalid connection string. Could not resolve database path.");
        }
        
        _databaseFilePath = dbPath;
        
        var dbDirectory = Path.GetDirectoryName(_databaseFilePath) ?? AppContext.BaseDirectory;
        _autoBackupDirectory = Path.Combine(dbDirectory, "Backups");
    }

    private async Task<bool> ValidateDatabaseIntegrityAsync(string dbPath)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = (string?)await command.ExecuteScalarAsync();

            return result?.Equals("ok", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CreateManualBackupAsync(string targetFilePath)
    {
        int userId = _authService.CurrentUser?.UserId ?? 0;

        if (!File.Exists(_databaseFilePath))
            throw new FileNotFoundException("The main database file could not be found.");

        if (File.Exists(targetFilePath))
        {
            throw new InvalidOperationException("Backup file already exists. Will not overwrite.");
        }

        if (!await ValidateDatabaseIntegrityAsync(_databaseFilePath))
        {
            _ = _auditLogService.LogIndependentActionAsync(userId, "BackupFailed", "System", targetFilePath, "Source database failed integrity check.");
            throw new InvalidOperationException("Source database failed integrity check. Backup aborted.");
        }

        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            using (var sourceStream = new FileStream(_databaseFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var destStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            if (!await ValidateDatabaseIntegrityAsync(targetFilePath))
            {
                File.Delete(targetFilePath);
                _ = _auditLogService.LogIndependentActionAsync(userId, "BackupFailed", "System", targetFilePath, "Created backup failed integrity check.");
                throw new InvalidOperationException("Created backup failed integrity check and was deleted.");
            }

            _ = _auditLogService.LogIndependentActionAsync(userId, "BackupCreated", "System", targetFilePath, "Manual backup created successfully.");
            return targetFilePath;
        }
        catch (Exception ex)
        {
            _ = _auditLogService.LogIndependentActionAsync(userId, "BackupFailed", "System", targetFilePath, $"Failed to create backup: {ex.Message}");
            throw;
        }
    }

    public async Task<string> CreateAutoBackupAsync()
    {
        if (!Directory.Exists(_autoBackupDirectory))
        {
            Directory.CreateDirectory(_autoBackupDirectory);
        }
        
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var existingBackup = Directory.GetFiles(_autoBackupDirectory, $"Inventory_{today}*.db").FirstOrDefault();
        if (existingBackup != null)
        {
            return existingBackup;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var backupFileName = $"Inventory_{timestamp}.db";
        var backupFilePath = Path.Combine(_autoBackupDirectory, backupFileName);

        var result = await CreateManualBackupAsync(backupFilePath);
        
        await EnforceRetentionPolicyAsync(30);
        return result;
    }

    public async Task RestoreBackupAsync(string backupFilePath)
    {
        int userId = _authService.CurrentUser?.UserId ?? 0;

        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"The backup file '{backupFilePath}' could not be found.");

        if (!File.Exists(_databaseFilePath))
            throw new FileNotFoundException("The target database file could not be found.");

        if (!await ValidateDatabaseIntegrityAsync(backupFilePath))
        {
            _ = _auditLogService.LogIndependentActionAsync(userId, "BackupFailed", "System", backupFilePath, "Backup file failed integrity check.");
            throw new InvalidOperationException("Backup file failed integrity check. Restore aborted.");
        }

        var safetyBackupPath = _databaseFilePath + $".pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        File.Copy(_databaseFilePath, safetyBackupPath, overwrite: true);

        try
        {
            File.Copy(backupFilePath, _databaseFilePath, overwrite: true);

            var walPath = _databaseFilePath + "-wal";
            var shmPath = _databaseFilePath + "-shm";
            if (File.Exists(walPath)) File.Delete(walPath);
            if (File.Exists(shmPath)) File.Delete(shmPath);

            if (!await ValidateDatabaseIntegrityAsync(_databaseFilePath))
            {
                File.Copy(safetyBackupPath, _databaseFilePath, overwrite: true);
                _ = _auditLogService.LogIndependentActionAsync(userId, "BackupFailed", "System", backupFilePath, "Restored database failed integrity check. Rolled back.");
                throw new InvalidOperationException("Restored database failed integrity check. The original database has been preserved.");
            }

            _ = _auditLogService.LogIndependentActionAsync(userId, "BackupRestored", "System", backupFilePath, "Database restored from backup successfully.");
        }
        catch (Exception ex)
        {
            _ = _auditLogService.LogIndependentActionAsync(userId, "BackupFailed", "System", backupFilePath, $"Failed to restore backup: {ex.Message}");
            if (File.Exists(safetyBackupPath))
            {
                File.Copy(safetyBackupPath, _databaseFilePath, overwrite: true);
            }
            throw;
        }
    }

    public Task EnforceRetentionPolicyAsync(int maxBackups = 30)
    {
        if (!Directory.Exists(_autoBackupDirectory))
            return Task.CompletedTask;

        var backupFiles = Directory.GetFiles(_autoBackupDirectory, "Inventory_*.db")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        if (backupFiles.Count > maxBackups)
        {
            var filesToDelete = backupFiles.Skip(maxBackups);
            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Ignore
                }
            }
        }

        return Task.CompletedTask;
    }
}

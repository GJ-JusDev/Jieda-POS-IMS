using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Infrastructure.Data;

namespace InventoryManagement.Infrastructure.Services;

public class SystemDiagnosticsService : ISystemDiagnosticsService
{
    private readonly InventoryDbContext _dbContext;

    public SystemDiagnosticsService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DiagnosticItem>> RunDiagnosticsAsync()
    {
        var results = new List<DiagnosticItem>();

        // System Info
        results.Add(new DiagnosticItem { Name = "Application Version", Value = "1.0.0", Status = "PASS" });
        results.Add(new DiagnosticItem { Name = "Operating System", Value = RuntimeInformation.OSDescription, Status = "PASS" });
        results.Add(new DiagnosticItem { Name = ".NET Runtime", Value = RuntimeInformation.FrameworkDescription, Status = "PASS" });
        results.Add(new DiagnosticItem { Name = "Process Architecture", Value = RuntimeInformation.ProcessArchitecture.ToString(), Status = "PASS" });
        results.Add(new DiagnosticItem { Name = "Processor Count", Value = Environment.ProcessorCount.ToString(), Status = "PASS" });
        
        try 
        {
            var gcInfo = GC.GetGCMemoryInfo();
            double availMemGb = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
            results.Add(new DiagnosticItem { Name = "Total System Memory", Value = $"{availMemGb:F1} GB", Status = "PASS" });
        }
        catch { }

        // Paths
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InventoryManagement");
        results.Add(new DiagnosticItem { Name = "Configuration Path", Value = Path.Combine(appData, "Config", "user-settings.json"), Status = "PASS" });
        results.Add(new DiagnosticItem { Name = "Log Path", Value = Path.Combine(appData, "Logs", "app.log"), Status = "PASS" });

        // Database Path & Exists
        string dbPath = "";
        try
        {
            var conn = _dbContext.Database.GetDbConnection();
            dbPath = conn.DataSource;
            results.Add(new DiagnosticItem { Name = "Database Path", Value = dbPath, Status = "PASS" });

            if (File.Exists(dbPath))
            {
                results.Add(new DiagnosticItem { Name = "Database Exists", Value = "Yes", Status = "PASS" });
            }
            else
            {
                results.Add(new DiagnosticItem { Name = "Database Exists", Value = "No", Status = "ERROR" });
            }
        }
        catch (Exception ex)
        {
            results.Add(new DiagnosticItem { Name = "Database Exists", Value = ex.Message, Status = "ERROR" });
        }

        // Database Connectivity
        try
        {
            if (await _dbContext.Database.CanConnectAsync())
            {
                results.Add(new DiagnosticItem { Name = "Database Accessibility", Value = "Connected", Status = "PASS" });
            }
            else
            {
                results.Add(new DiagnosticItem { Name = "Database Accessibility", Value = "Cannot Connect", Status = "ERROR" });
            }
        }
        catch
        {
            results.Add(new DiagnosticItem { Name = "Database Accessibility", Value = "Failed", Status = "ERROR" });
        }

        // Database Integrity
        try
        {
            var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            await _dbContext.Database.OpenConnectionAsync();
            var integrity = (string?)await cmd.ExecuteScalarAsync();
            if (integrity == "ok")
            {
                results.Add(new DiagnosticItem { Name = "SQLite Integrity Status", Value = "OK", Status = "PASS" });
            }
            else
            {
                results.Add(new DiagnosticItem { Name = "SQLite Integrity Status", Value = integrity ?? "Failed", Status = "ERROR" });
            }
        }
        catch (Exception ex)
        {
            results.Add(new DiagnosticItem { Name = "SQLite Integrity Status", Value = ex.Message, Status = "ERROR" });
        }

        // Disk Space
        try
        {
            if (!string.IsNullOrEmpty(dbPath))
            {
                var drive = new DriveInfo(Path.GetPathRoot(dbPath)!);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                
                string status = freeGb < 1.0 ? "WARNING" : "PASS";
                results.Add(new DiagnosticItem { Name = "Disk Space (Free / Total)", Value = $"{freeGb:F1} GB / {totalGb:F1} GB", Status = status });
            }
        }
        catch
        {
            results.Add(new DiagnosticItem { Name = "Disk Space", Value = "Unknown", Status = "WARNING" });
        }

        return results;
    }
}

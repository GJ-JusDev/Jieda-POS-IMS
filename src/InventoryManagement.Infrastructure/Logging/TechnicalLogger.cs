using System;
using System.IO;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Infrastructure.Logging;

public class TechnicalLogger : ITechnicalLogger
{
    private readonly string _logFilePath;
    private readonly int _maxFileSize;
    
    public TechnicalLogger(string logDirectory, int maxFileSizeBytes = 5 * 1024 * 1024)
    {
        _maxFileSize = maxFileSizeBytes;
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }
        catch { } // Swallow so we don't crash before logging can even start if permissions are fully denied.
        
        _logFilePath = Path.Combine(logDirectory, "app.log");
    }

    public void LogInfo(string message) => WriteLog("INFO", message);
    public void LogWarning(string message) => WriteLog("WARNING", message);
    public void LogError(string message, Exception? ex = null)
    {
        var log = ex != null ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}" : message;
        WriteLog("ERROR", log);
    }

    private void WriteLog(string level, string message)
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Length > _maxFileSize)
                {
                    RotateLogs();
                }
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"{timestamp} [{level}] {message}\n";
            File.AppendAllText(_logFilePath, logEntry);
        }
        catch
        {
            // Fallback: swallow exception so logger doesn't crash app
        }
    }

    private void RotateLogs()
    {
        int maxRetained = 3;
        var dir = Path.GetDirectoryName(_logFilePath);
        if (dir == null) return;
        
        // Remove the oldest if it exists
        var oldest = Path.Combine(dir, $"app.{maxRetained}.log");
        if (File.Exists(oldest)) File.Delete(oldest);
        
        // Shift existing rotated files
        for (int i = maxRetained - 1; i >= 1; i--)
        {
            var current = Path.Combine(dir, $"app.{i}.log");
            var next = Path.Combine(dir, $"app.{i + 1}.log");
            if (File.Exists(current))
            {
                File.Move(current, next);
            }
        }
        
        // Rename current app.log to app.1.log
        File.Move(_logFilePath, Path.Combine(dir, "app.1.log"));
    }
}

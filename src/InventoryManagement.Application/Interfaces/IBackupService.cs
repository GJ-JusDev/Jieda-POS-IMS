using System.Threading.Tasks;

namespace InventoryManagement.Application.Interfaces;

public interface IBackupService
{
    Task<string> CreateManualBackupAsync(string targetFilePath);
    Task<string> CreateAutoBackupAsync();
    Task RestoreBackupAsync(string backupFilePath);
    Task EnforceRetentionPolicyAsync(int maxBackups = 30);
}

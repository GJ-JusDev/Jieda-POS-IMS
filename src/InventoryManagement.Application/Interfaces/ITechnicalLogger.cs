using System;

namespace InventoryManagement.Application.Interfaces;

public interface ITechnicalLogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}

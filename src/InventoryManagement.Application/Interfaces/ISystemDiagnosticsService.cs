using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagement.Application.Interfaces;

public class DiagnosticItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Status { get; set; } = "PASS"; // PASS, WARNING, ERROR
}

public interface ISystemDiagnosticsService
{
    Task<List<DiagnosticItem>> RunDiagnosticsAsync();
}

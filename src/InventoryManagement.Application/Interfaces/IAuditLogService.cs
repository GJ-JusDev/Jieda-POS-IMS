using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IAuditLogService
{
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null,
        int? userId = null,
        string action = "",
        string tableName = "",
        string recordId = "",
        int skip = 0, 
        int take = 100);

    Task LogIndependentActionAsync(int userId, string action, string tableName, string recordId, string description);
}

using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IAuditLogService
{
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int take = 1000);
}

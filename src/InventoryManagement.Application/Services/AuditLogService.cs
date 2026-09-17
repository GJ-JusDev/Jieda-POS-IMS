using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IInventoryDbContext _context;

    public AuditLogService(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int take = 1000)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync();
    }
}


using System;
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

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null,
        int? userId = null,
        string action = "",
        string tableName = "",
        string recordId = "",
        int skip = 0, 
        int take = 100)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = endDate.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedAt <= endUtc);
        }

        if (userId.HasValue && userId.Value > 0)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = query.Where(a => a.TableName.Contains(tableName));
        }

        if (!string.IsNullOrWhiteSpace(recordId))
        {
            query = query.Where(a => a.RecordId == recordId);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task LogIndependentActionAsync(int userId, string action, string tableName, string recordId, string description)
    {
        var audit = new AuditLog
        {
            UserId = userId,
            Action = action,
            TableName = tableName,
            RecordId = recordId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
        await _context.SaveChangesAsync();
    }
}


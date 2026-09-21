using System;
namespace InventoryManagement.Domain.Entities;
public class AuditLog {
    public int AuditLogId { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public User? User { get; set; }
}

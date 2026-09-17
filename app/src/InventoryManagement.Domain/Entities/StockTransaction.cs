using System;
using InventoryManagement.Domain.Enums;
namespace InventoryManagement.Domain.Entities;
public class StockTransaction {
    public int StockTransactionId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public StockTransactionType TransactionType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public int CreatedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

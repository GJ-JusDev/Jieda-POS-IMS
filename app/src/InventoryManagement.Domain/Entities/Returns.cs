using System;
using System.Collections.Generic;
namespace InventoryManagement.Domain.Entities;

public class SalesReturn
{
    public int SalesReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public string? Reason { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<SalesReturnItem> ReturnItems { get; set; } = new List<SalesReturnItem>();
}

public class SalesReturnItem
{
    public int SalesReturnItemId { get; set; }
    public int SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal RefundAmount { get; set; }
}

public class PurchaseReturn
{
    public int PurchaseReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public string? Reason { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PurchaseReturnItem> ReturnItems { get; set; } = new List<PurchaseReturnItem>();
}

public class PurchaseReturnItem
{
    public int PurchaseReturnItemId { get; set; }
    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal RefundAmount { get; set; }
}

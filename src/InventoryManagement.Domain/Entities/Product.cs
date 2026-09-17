using System;
namespace InventoryManagement.Domain.Entities;
public class Product {
    public int ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

    public bool IsManufactured { get; set; }
    public decimal MarkupPercentage { get; set; }
    
    public System.Collections.Generic.ICollection<ProductComponent> Components { get; set; } = new System.Collections.Generic.List<ProductComponent>();
    
    public System.Collections.Generic.ICollection<ProductInput> Inputs { get; set; } = new System.Collections.Generic.List<ProductInput>();
}

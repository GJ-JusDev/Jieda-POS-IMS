using System;
using System.Collections.Generic;
namespace InventoryManagement.Domain.Entities;
public class Category {
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

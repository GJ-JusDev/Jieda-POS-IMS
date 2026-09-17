using System.Collections.Generic;
namespace InventoryManagement.Domain.Entities;
public class Unit {
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

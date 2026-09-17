namespace InventoryManagement.Domain.Entities;

public class ProductComponent
{
    public int ProductComponentId { get; set; }
    
    public int FinishedProductId { get; set; }
    public Product? FinishedProduct { get; set; }
    
    public int RawMaterialId { get; set; }
    public Product? RawMaterial { get; set; }
    
    public decimal QuantityUsed { get; set; }
}

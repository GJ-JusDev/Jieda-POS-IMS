namespace InventoryManagement.Domain.Entities;

public class ProductInputOption
{
    public int ProductInputOptionId { get; set; }
    
    public int ProductInputId { get; set; }
    public ProductInput? ProductInput { get; set; }
    
    public string OptionName { get; set; } = string.Empty;
    public string? Value { get; set; }
    
    public decimal AdditionalPrice { get; set; }
    
    public int SortOrder { get; set; }
    
    public bool IsActive { get; set; } = true;
}

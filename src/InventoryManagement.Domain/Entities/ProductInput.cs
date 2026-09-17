using System.Collections.Generic;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Domain.Entities;

public class ProductInput
{
    public int ProductInputId { get; set; }
    
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    
    public string InputName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public InputType InputType { get; set; }
    
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    
    public string? Unit { get; set; }
    public string? DefaultValue { get; set; }
    public string? HelpText { get; set; }
    
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public ICollection<ProductInputOption> Options { get; set; } = new List<ProductInputOption>();
}

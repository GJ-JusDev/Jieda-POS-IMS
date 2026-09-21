using System;

namespace InventoryManagement.Application.DTOs.Criteria;

public class ProductSearchCriteria : SearchCriteria
{
    public int? CategoryId { get; set; }
    public int? UnitId { get; set; }
    public bool? IsActive { get; set; } // null means all
}

public class InventorySearchCriteria : SearchCriteria
{
    public int? CategoryId { get; set; }
    public string? StockStatus { get; set; } // "All", "In Stock", "Low Stock", "Out of Stock"
}

public class SalesSearchCriteria : SearchCriteria
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    public InventoryManagement.Domain.Enums.SaleStatus? Status { get; set; }
}

public class PurchaseSearchCriteria : SearchCriteria
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int? SupplierId { get; set; }
    public int? UserId { get; set; }
    public InventoryManagement.Domain.Enums.PurchaseStatus? Status { get; set; }
}

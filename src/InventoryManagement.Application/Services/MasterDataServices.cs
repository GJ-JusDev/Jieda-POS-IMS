using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Services;

public abstract class BaseMasterDataService<T> : IMasterDataService<T> where T : class
{
    protected readonly IInventoryDbContext _context;
    protected readonly IAuditLogService _auditLogService;
    protected readonly IAuthenticationService _authService;

    protected BaseMasterDataService(IInventoryDbContext context, IAuditLogService auditLogService, IAuthenticationService authService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _authService = authService;
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _context.Set<T>().ToListAsync();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _context.Set<T>().FindAsync(id);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        var createdAtProp = typeof(T).GetProperty("CreatedAt");
        if (createdAtProp != null && createdAtProp.CanWrite)
            createdAtProp.SetValue(entity, DateTime.UtcNow);

        _context.Set<T>().Add(entity);
        
        // Save first to generate ID
        await _context.SaveChangesAsync();

        var idProp = typeof(T).GetProperty(typeof(T).Name + "Id");
        var id = idProp?.GetValue(entity)?.ToString() ?? "";
        
        int userId = _authService.CurrentUser?.UserId ?? 0;
        
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = $"{typeof(T).Name}Created",
            TableName = typeof(T).Name,
            RecordId = id,
            Description = $"Created {typeof(T).Name}"
        });
        
        await _context.SaveChangesAsync();

        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        var updatedAtProp = typeof(T).GetProperty("UpdatedAt");
        if (updatedAtProp != null && updatedAtProp.CanWrite)
            updatedAtProp.SetValue(entity, DateTime.UtcNow);

        _context.Set<T>().Update(entity);
        
        var idProp = typeof(T).GetProperty(typeof(T).Name + "Id");
        var id = idProp?.GetValue(entity)?.ToString() ?? "";
        
        int userId = _authService.CurrentUser?.UserId ?? 0;
        
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = $"{typeof(T).Name}Updated",
            TableName = typeof(T).Name,
            RecordId = id,
            Description = $"Updated {typeof(T).Name}"
        });
        
        await _context.SaveChangesAsync();
    }

    public virtual async Task DeactivateAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            var isActiveProp = typeof(T).GetProperty("IsActive");
            if (isActiveProp != null && isActiveProp.CanWrite)
            {
                isActiveProp.SetValue(entity, false);
                
                var updatedAtProp = typeof(T).GetProperty("UpdatedAt");
                if (updatedAtProp != null && updatedAtProp.CanWrite)
                    updatedAtProp.SetValue(entity, DateTime.UtcNow);

                _context.Set<T>().Update(entity);
                
                int userId = _authService.CurrentUser?.UserId ?? 0;
                
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = $"{typeof(T).Name}Deactivated",
                    TableName = typeof(T).Name,
                    RecordId = id.ToString(),
                    Description = $"Deactivated {typeof(T).Name}"
                });
                
                await _context.SaveChangesAsync();
            }
        }
    }
}

public class CategoryService : BaseMasterDataService<Category>, ICategoryService
{
    public CategoryService(IInventoryDbContext context, IAuditLogService auditLogService, IAuthenticationService authService) : base(context, auditLogService, authService) { }
    
    public override async Task<IEnumerable<Category>> GetAllAsync()
    {
        return await _context.Categories.Where(c => c.IsActive).ToListAsync();
    }
}

public class UnitService : BaseMasterDataService<Unit>, IUnitService
{
    public UnitService(IInventoryDbContext context, IAuditLogService auditLogService, IAuthenticationService authService) : base(context, auditLogService, authService) { }
    
    public override async Task<IEnumerable<Unit>> GetAllAsync()
    {
        return await _context.Units.Where(u => u.IsActive).OrderBy(u => u.UnitName).ToListAsync();
    }
}

public class SupplierService : BaseMasterDataService<Supplier>, ISupplierService
{
    public SupplierService(IInventoryDbContext context, IAuditLogService auditLogService, IAuthenticationService authService) : base(context, auditLogService, authService) { }
    
    public override async Task<IEnumerable<Supplier>> GetAllAsync()
    {
        return await _context.Suppliers.Where(s => s.IsActive).ToListAsync();
    }
}

public class CustomerService : BaseMasterDataService<Customer>, ICustomerService
{
    public CustomerService(IInventoryDbContext context, IAuditLogService auditLogService, IAuthenticationService authService) : base(context, auditLogService, authService) { }
    
    public override async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await _context.Customers.Where(c => c.IsActive).ToListAsync();
    }
}

public class ProductService : BaseMasterDataService<Product>, IProductService
{
    public ProductService(IInventoryDbContext context, IAuditLogService auditLogService, IAuthenticationService authService) : base(context, auditLogService, authService) { }

    public override async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .FirstOrDefaultAsync(p => p.SKU == sku && p.IsActive);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .FirstOrDefaultAsync(p => p.Barcode == barcode && p.IsActive);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string searchTerm, int? categoryId, int? unitId)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(p => p.ProductName.ToLower().Contains(term) || 
                                     p.SKU.ToLower().Contains(term) || 
                                     (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (unitId.HasValue && unitId.Value > 0)
        {
            query = query.Where(p => p.UnitId == unitId.Value);
        }

        return await query.ToListAsync();
    }
    public async Task<InventoryManagement.Application.DTOs.Criteria.PagedResult<Product>> SearchProductsAsync(InventoryManagement.Application.DTOs.Criteria.ProductSearchCriteria criteria)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .AsQueryable();

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == criteria.IsActive.Value);
        }

        if (criteria.CategoryId.HasValue && criteria.CategoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == criteria.CategoryId.Value);
        }

        if (criteria.UnitId.HasValue && criteria.UnitId.Value > 0)
        {
            query = query.Where(p => p.UnitId == criteria.UnitId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var text = criteria.SearchText.Trim(); // Let SQLite handle case insensitivity via LIKE
            query = query.Where(p => p.ProductName.Contains(text) || 
                                     p.SKU.Contains(text) || 
                                     (p.Barcode != null && p.Barcode.Contains(text)));
        }

        var totalCount = await query.CountAsync();

        if (string.IsNullOrEmpty(criteria.SortColumn))
        {
            query = query.OrderBy(p => p.ProductName);
        }
        else
        {
            query = criteria.SortColumn switch
            {
                "ProductName" => criteria.SortDescending ? query.OrderByDescending(p => p.ProductName) : query.OrderBy(p => p.ProductName),
                "SKU" => criteria.SortDescending ? query.OrderByDescending(p => p.SKU) : query.OrderBy(p => p.SKU),
                "Category" => criteria.SortDescending ? query.OrderByDescending(p => p.Category != null ? p.Category.CategoryName : "") : query.OrderBy(p => p.Category != null ? p.Category.CategoryName : ""),
                "Price" => criteria.SortDescending ? query.OrderByDescending(p => p.SellingPrice) : query.OrderBy(p => p.SellingPrice),
                _ => query.OrderBy(p => p.ProductName)
            };
        }

        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();

        return new InventoryManagement.Application.DTOs.Criteria.PagedResult<Product>
        {
            Items = items,
            TotalCount = totalCount,
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };
    }
    
    public override async Task<Product> AddAsync(Product entity)
    {
        // Validation for uniqueness
        if (await _context.Products.AnyAsync(p => p.SKU == entity.SKU))
            throw new Exception($"Product with SKU '{entity.SKU}' already exists.");
            
        if (!string.IsNullOrWhiteSpace(entity.Barcode) && await _context.Products.AnyAsync(p => p.Barcode == entity.Barcode))
            throw new Exception($"Product with Barcode '{entity.Barcode}' already exists.");
            
        return await base.AddAsync(entity);
    }
    
    public override async Task UpdateAsync(Product entity)
    {
        // Validation for uniqueness
        if (await _context.Products.AnyAsync(p => p.SKU == entity.SKU && p.ProductId != entity.ProductId))
            throw new Exception($"Product with SKU '{entity.SKU}' already exists.");
            
        if (!string.IsNullOrWhiteSpace(entity.Barcode) && await _context.Products.AnyAsync(p => p.Barcode == entity.Barcode && p.ProductId != entity.ProductId))
            throw new Exception($"Product with Barcode '{entity.Barcode}' already exists.");
            
        await base.UpdateAsync(entity);
    }
}


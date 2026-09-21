using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IMasterDataService<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeactivateAsync(int id);
}

public interface ICategoryService : IMasterDataService<Category> { }
public interface IUnitService : IMasterDataService<Unit> { }
public interface ISupplierService : IMasterDataService<Supplier> { }
public interface ICustomerService : IMasterDataService<Customer> { }
public interface IProductService : IMasterDataService<Product> 
{ 
    Task<Product?> GetBySkuAsync(string sku);
    Task<Product?> GetByBarcodeAsync(string barcode);
    Task<IEnumerable<Product>> SearchAsync(string searchTerm, int? categoryId, int? unitId);
    Task<InventoryManagement.Application.DTOs.Criteria.PagedResult<Product>> SearchProductsAsync(InventoryManagement.Application.DTOs.Criteria.ProductSearchCriteria criteria);
}

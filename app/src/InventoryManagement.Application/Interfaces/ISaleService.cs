using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Interfaces;

public interface ISaleService
{
    Task<Sale> CreateSaleAsync(int customerId, int createdBy, IEnumerable<SaleItem> items, string paymentMethod, string? notes, bool allowNegativeStock = false);
    Task<Sale?> GetSaleAsync(int saleId);
    Task<IEnumerable<Sale>> GetAllSalesAsync();
}

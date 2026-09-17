using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Application.DTOs;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IInventoryService
{
    Task<decimal> GetCurrentStockAsync(int productId);
    Task<IEnumerable<StockTransaction>> GetStockHistoryAsync(int productId);
    Task<IEnumerable<StockOverviewDto>> GetStockOverviewAsync(string searchTerm = "", int? categoryId = null, string statusFilter = "");
    Task AddStockAdjustmentAsync(int productId, decimal quantity, string reason, int userId);
}

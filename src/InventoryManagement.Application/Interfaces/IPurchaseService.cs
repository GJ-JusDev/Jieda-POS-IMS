using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Interfaces;

public interface IPurchaseService
{
    Task<Purchase> CreateDraftPurchaseAsync(int supplierId, int createdBy, string? notes);
    Task AddPurchaseItemAsync(int purchaseId, int productId, decimal quantity, decimal unitCost);
    Task<Purchase?> GetPurchaseAsync(int purchaseId);
    Task<IEnumerable<Purchase>> GetAllPurchasesAsync();
    Task<InventoryManagement.Application.DTOs.Criteria.PagedResult<Purchase>> SearchPurchasesAsync(InventoryManagement.Application.DTOs.Criteria.PurchaseSearchCriteria criteria);
    Task CompletePurchaseAsync(int purchaseId, int userId);
    Task DeletePurchaseAsync(int purchaseId);
    Task RemovePurchaseItemAsync(int purchaseItemId);
}


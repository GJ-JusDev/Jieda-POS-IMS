using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IReturnService
{
    Task<SalesReturn> ProcessSalesReturnAsync(int saleId, IEnumerable<SalesReturnItem> items, string? reason, int createdBy);
    Task<PurchaseReturn> ProcessPurchaseReturnAsync(int purchaseId, IEnumerable<PurchaseReturnItem> items, string? reason, int createdBy);
}

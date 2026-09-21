using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Services;

public class PurchaseService : IPurchaseService
{
    private readonly IInventoryDbContext _context;
    private readonly IAuthorizationService _authzService;

    public PurchaseService(IInventoryDbContext context, IAuthorizationService authzService)
    {
        _context = context;
        _authzService = authzService;
    }

    public async Task<Purchase> CreateDraftPurchaseAsync(int supplierId, int createdBy, string? notes)
    {
        if (!_authzService.HasPermission(Permission.CreatePurchase))
            throw new UnauthorizedAccessException("You do not have permission to create purchases.");
        var purchase = new Purchase
        {
            SupplierId = supplierId,
            PurchaseNumber = GeneratePurchaseNumber(),
            PurchaseDate = DateTime.UtcNow,
            Status = PurchaseStatus.Draft,
            CreatedBy = createdBy,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync();
        return purchase;
    }

    public async Task AddPurchaseItemAsync(int purchaseId, int productId, decimal quantity, decimal unitCost)
    {
        var purchase = await _context.Purchases.Include(p => p.PurchaseItems).FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);
        if (purchase == null) throw new Exception("Purchase not found.");
        
        if (purchase.Status != PurchaseStatus.Draft)
            throw new Exception("Items can only be added to a draft purchase.");

        var item = new PurchaseItem
        {
            PurchaseId = purchaseId,
            ProductId = productId,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = quantity * unitCost
        };

        _context.PurchaseItems.Add(item);
        
        // Update purchase totals
        purchase.Subtotal += item.TotalCost;
        purchase.TotalAmount = purchase.Subtotal - purchase.Discount + purchase.Tax;
        
        await _context.SaveChangesAsync();
    }

    public async Task<Purchase?> GetPurchaseAsync(int purchaseId)
    {
        return await _context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);
    }

    public async Task<IEnumerable<Purchase>> GetAllPurchasesAsync()
    {
        return await _context.Purchases
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
    }

    public async Task CompletePurchaseAsync(int purchaseId, int userId)
    {
        var purchase = await _context.Purchases
            .Include(p => p.PurchaseItems)
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);

        if (purchase == null) throw new Exception("Purchase not found.");
        if (purchase.Status == PurchaseStatus.Completed) throw new Exception("Purchase is already completed.");

        if (!_authzService.HasPermission(Permission.CompletePurchase))
            throw new UnauthorizedAccessException("You do not have permission to complete purchases.");

        var isInMemory = ((DbContext)_context).Database.ProviderName?.Contains("InMemory") == true;
        var transaction = isInMemory ? null : await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            purchase.Status = PurchaseStatus.Completed;
            purchase.UpdatedAt = DateTime.UtcNow;

            foreach (var item in purchase.PurchaseItems)
            {
                var stockTx = new StockTransaction
                {
                    ProductId = item.ProductId,
                    TransactionType = StockTransactionType.Purchase,
                    Quantity = item.Quantity, // Positive stock in
                    UnitCost = item.UnitCost,
                    ReferenceType = "Purchase",
                    ReferenceId = purchase.PurchaseId,
                    TransactionDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    Notes = $"Purchase received: {purchase.PurchaseNumber}"
                };
                
                _context.StockTransactions.Add(stockTx);
            }

            var audit = new AuditLog
            {
                UserId = userId,
                Action = "PurchaseCompleted",
                TableName = "Purchases",
                RecordId = purchase.PurchaseId.ToString(),
                Description = $"Completed purchase {purchase.PurchaseNumber} and generated stock transactions."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private string GeneratePurchaseNumber()
    {
        return $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
    }
    public async Task DeletePurchaseAsync(int purchaseId)
    {
        var purchase = await _context.Purchases.Include(p => p.PurchaseItems).FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);
        if (purchase == null) throw new Exception("Purchase not found.");
                // If completed, we must reverse/delete the associated stock transactions
        if (purchase.Status == PurchaseStatus.Completed)
        {
            var relatedStockTxs = await _context.StockTransactions
                .Where(st => st.ReferenceType == "Purchase" && st.ReferenceId == purchase.PurchaseId)
                .ToListAsync();
            
            if (relatedStockTxs.Any())
            {
                _context.StockTransactions.RemoveRange(relatedStockTxs);
            }
        }

        _context.PurchaseItems.RemoveRange(purchase.PurchaseItems);
        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
    }

    public async Task RemovePurchaseItemAsync(int purchaseItemId)
    {
        var item = await _context.PurchaseItems.Include(pi => pi.Purchase).FirstOrDefaultAsync(pi => pi.PurchaseItemId == purchaseItemId);
        if (item == null) throw new Exception("Item not found.");
        if (item.Purchase?.Status != PurchaseStatus.Draft) throw new Exception("Cannot modify non-draft purchase.");

        var purchase = item.Purchase;
        _context.PurchaseItems.Remove(item);
        
        purchase.TotalAmount -= item.TotalCost;
        
        await _context.SaveChangesAsync();
    }
    public async Task<InventoryManagement.Application.DTOs.Criteria.PagedResult<Purchase>> SearchPurchasesAsync(InventoryManagement.Application.DTOs.Criteria.PurchaseSearchCriteria criteria)
    {
        var query = _context.Purchases
            .Include(p => p.Supplier)
            .AsQueryable();

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(p => p.PurchaseDate >= criteria.DateFrom.Value);
        }
        if (criteria.DateTo.HasValue)
        {
            var endOfDay = criteria.DateTo.Value.AddDays(1);
            query = query.Where(p => p.PurchaseDate < endOfDay);
        }
        if (criteria.SupplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == criteria.SupplierId.Value);
        }
        if (criteria.UserId.HasValue)
        {
            query = query.Where(p => p.CreatedBy == criteria.UserId.Value);
        }
        if (criteria.Status.HasValue)
        {
            query = query.Where(p => p.Status == criteria.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var text = criteria.SearchText.Trim();
            query = query.Where(p => p.PurchaseNumber.Contains(text) || (p.Supplier != null && p.Supplier.SupplierName.Contains(text)));
        }

        var totalCount = await query.CountAsync();

        if (string.IsNullOrEmpty(criteria.SortColumn))
        {
            query = query.OrderByDescending(p => p.PurchaseDate);
        }
        else
        {
            query = criteria.SortColumn switch
            {
                "PurchaseNumber" => criteria.SortDescending ? query.OrderByDescending(p => p.PurchaseNumber) : query.OrderBy(p => p.PurchaseNumber),
                "PurchaseDate" => criteria.SortDescending ? query.OrderByDescending(p => p.PurchaseDate) : query.OrderBy(p => p.PurchaseDate),
                "Supplier" => criteria.SortDescending ? query.OrderByDescending(p => p.Supplier != null ? p.Supplier.SupplierName : string.Empty) : query.OrderBy(p => p.Supplier != null ? p.Supplier.SupplierName : string.Empty),
                "TotalAmount" => criteria.SortDescending ? query.OrderByDescending(p => p.TotalAmount) : query.OrderBy(p => p.TotalAmount),
                _ => query.OrderByDescending(p => p.PurchaseDate)
            };
        }

        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();

        return new InventoryManagement.Application.DTOs.Criteria.PagedResult<Purchase>
        {
            Items = items,
            TotalCount = totalCount,
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };
    }
}




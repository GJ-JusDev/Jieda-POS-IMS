using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Services;

public class ReturnService : IReturnService
{
    private readonly IInventoryDbContext _context;
    private readonly IAuthorizationService _authzService;

    public ReturnService(IInventoryDbContext context, IAuthorizationService authzService)
    {
        _context = context;
        _authzService = authzService;
    }

    public async Task<SalesReturn> ProcessSalesReturnAsync(int saleId, IEnumerable<SalesReturnItem> items, string? reason, int createdBy)
    {
        if (!_authzService.HasPermission(Permission.ProcessSalesReturn))
            throw new UnauthorizedAccessException("You do not have permission to process sales returns.");

        var sale = await _context.Sales.Include(s => s.SaleItems).FirstOrDefaultAsync(s => s.SaleId == saleId);
        if (sale == null) throw new Exception("Sale not found.");

        var returnItems = items.ToList();
        if (!returnItems.Any()) throw new Exception("Return must contain at least one item.");

        var salesReturn = new SalesReturn
        {
            ReturnNumber = $"SR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0,6).ToUpper()}",
            SaleId = saleId,
            ReturnDate = DateTime.UtcNow,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        decimal totalRefund = 0;

        var isInMemory = ((DbContext)_context).Database.ProviderName?.Contains("InMemory") == true;
        var transaction = isInMemory ? null : await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            foreach (var item in returnItems)
            {
                var saleItem = sale.SaleItems.FirstOrDefault(si => si.ProductId == item.ProductId);
                if (saleItem == null) throw new Exception($"Product ID {item.ProductId} was not part of this sale.");
                
                var previouslyReturned = await _context.SalesReturnItems
                    .Where(sri => sri.SalesReturn.SaleId == saleId && sri.ProductId == item.ProductId)
                    .SumAsync(sri => sri.Quantity);
                
                if (item.Quantity + previouslyReturned > saleItem.Quantity) 
                    throw new Exception($"Cannot return {item.Quantity}. Only {saleItem.Quantity - previouslyReturned} remaining to return.");

                // For simplicity, prorate refund or assume refund amount is specified. If not, calculate.
                if (item.RefundAmount == 0)
                {
                    item.RefundAmount = (saleItem.TotalPrice / saleItem.Quantity) * item.Quantity;
                }

                totalRefund += item.RefundAmount;
                salesReturn.ReturnItems.Add(item);

                // Stock returned by customer -> Stock in
                var product = await _context.Products.FindAsync(item.ProductId);
                var stockTx = new StockTransaction
                {
                    ProductId = item.ProductId,
                    TransactionType = StockTransactionType.SalesReturn,
                    Quantity = item.Quantity, // Positive stock in
                    UnitCost = product?.CostPrice ?? 0,
                    ReferenceType = "SalesReturn",
                    ReferenceId = salesReturn.SalesReturnId, // This is 0 before SaveChanges
                    TransactionDate = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    Notes = $"Sales Return: {salesReturn.ReturnNumber}"
                };
                
                _context.StockTransactions.Add(stockTx);
            }

            salesReturn.TotalRefundAmount = totalRefund;
            _context.SalesReturns.Add(salesReturn);
            await _context.SaveChangesAsync(); // Save to generate SalesReturnId

            // Fix ReferenceId for stock transactions
            var addedTxs = _context.StockTransactions.Local.Where(st => st.ReferenceType == "SalesReturn" && st.ReferenceId == 0).ToList();
            foreach (var tx in addedTxs)
            {
                tx.ReferenceId = salesReturn.SalesReturnId;
            }

            var audit = new AuditLog
            {
                UserId = createdBy,
                Action = "SalesReturnProcessed",
                TableName = "SalesReturns",
                RecordId = saleId.ToString(),
                Description = $"Processed sales return {salesReturn.ReturnNumber} for sale {sale.InvoiceNumber}."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();

            return salesReturn;
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

    public async Task<PurchaseReturn> ProcessPurchaseReturnAsync(int purchaseId, IEnumerable<PurchaseReturnItem> items, string? reason, int createdBy)
    {
        if (!_authzService.HasPermission(Permission.ProcessPurchaseReturn))
            throw new UnauthorizedAccessException("You do not have permission to process purchase returns.");

        var purchase = await _context.Purchases.Include(p => p.PurchaseItems).FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);
        if (purchase == null) throw new Exception("Purchase not found.");

        var returnItems = items.ToList();
        if (!returnItems.Any()) throw new Exception("Return must contain at least one item.");

        var purchaseReturn = new PurchaseReturn
        {
            ReturnNumber = $"PR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0,6).ToUpper()}",
            PurchaseId = purchaseId,
            ReturnDate = DateTime.UtcNow,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        decimal totalRefund = 0;

        var isInMemory = ((DbContext)_context).Database.ProviderName?.Contains("InMemory") == true;
        var transaction = isInMemory ? null : await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            foreach (var item in returnItems)
            {
                var purchaseItem = purchase.PurchaseItems.FirstOrDefault(pi => pi.ProductId == item.ProductId);
                if (purchaseItem == null) throw new Exception($"Product ID {item.ProductId} was not part of this purchase.");
                
                var previouslyReturned = await _context.PurchaseReturnItems
                    .Where(pri => pri.PurchaseReturn.PurchaseId == purchaseId && pri.ProductId == item.ProductId)
                    .SumAsync(pri => pri.Quantity);

                if (item.Quantity + previouslyReturned > purchaseItem.Quantity) 
                    throw new Exception($"Cannot return {item.Quantity}. Only {purchaseItem.Quantity - previouslyReturned} remaining to return.");

                if (item.RefundAmount == 0)
                {
                    item.RefundAmount = (purchaseItem.TotalCost / purchaseItem.Quantity) * item.Quantity;
                }

                totalRefund += item.RefundAmount;
                purchaseReturn.ReturnItems.Add(item);

                // Stock returned to supplier -> Stock out
                var txs = await _context.StockTransactions.Where(st => st.ProductId == item.ProductId).Select(st => st.Quantity).ToListAsync();
                var currentStock = txs.Sum();
                if (currentStock < item.Quantity) throw new Exception($"Insufficient stock to return product ID {item.ProductId}. Available: {currentStock}");

                var product = await _context.Products.FindAsync(item.ProductId);
                var stockTx = new StockTransaction
                {
                    ProductId = item.ProductId,
                    TransactionType = StockTransactionType.PurchaseReturn,
                    Quantity = -item.Quantity, // Negative stock out
                    UnitCost = product?.CostPrice ?? 0,
                    ReferenceType = "PurchaseReturn",
                    ReferenceId = purchaseReturn.PurchaseReturnId, // This is 0 before SaveChanges
                    TransactionDate = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    Notes = $"Purchase Return: {purchaseReturn.ReturnNumber}"
                };
                
                _context.StockTransactions.Add(stockTx);
            }

            purchaseReturn.TotalRefundAmount = totalRefund;
            _context.PurchaseReturns.Add(purchaseReturn);
            await _context.SaveChangesAsync();

            var addedTxs = _context.StockTransactions.Local.Where(st => st.ReferenceType == "PurchaseReturn" && st.ReferenceId == 0).ToList();
            foreach (var tx in addedTxs)
            {
                tx.ReferenceId = purchaseReturn.PurchaseReturnId;
            }

            var audit = new AuditLog
            {
                UserId = createdBy,
                Action = "PurchaseReturnProcessed",
                TableName = "PurchaseReturns",
                RecordId = purchaseId.ToString(),
                Description = $"Processed purchase return {purchaseReturn.ReturnNumber} for purchase {purchase.PurchaseNumber}."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();

            return purchaseReturn;
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
}



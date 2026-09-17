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

    public ReturnService(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<SalesReturn> ProcessSalesReturnAsync(int saleId, IEnumerable<SalesReturnItem> items, string? reason, int createdBy)
    {
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

        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            foreach (var item in returnItems)
            {
                var saleItem = sale.SaleItems.FirstOrDefault(si => si.ProductId == item.ProductId);
                if (saleItem == null) throw new Exception($"Product ID {item.ProductId} was not part of this sale.");
                if (item.Quantity > saleItem.Quantity) throw new Exception("Cannot return more quantity than was sold.");

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
                    TransactionType = StockTransactionType.SaleReturn,
                    Quantity = item.Quantity, // Positive stock in
                    UnitCost = product?.CostPrice ?? 0,
                    ReferenceType = "SalesReturn",
                    ReferenceId = salesReturn.SalesReturnId,
                    TransactionDate = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    Notes = $"Sales Return: {salesReturn.ReturnNumber}"
                };
                
                _context.StockTransactions.Add(stockTx);
            }

            salesReturn.TotalRefundAmount = totalRefund;
            _context.SalesReturns.Add(salesReturn);

            var audit = new AuditLog
            {
                UserId = createdBy,
                Action = "ProcessSalesReturn",
                TableName = "SalesReturns",
                RecordId = saleId.ToString(),
                Description = $"Processed sales return {salesReturn.ReturnNumber} for sale {sale.InvoiceNumber}."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return salesReturn;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PurchaseReturn> ProcessPurchaseReturnAsync(int purchaseId, IEnumerable<PurchaseReturnItem> items, string? reason, int createdBy)
    {
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

        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            foreach (var item in returnItems)
            {
                var purchaseItem = purchase.PurchaseItems.FirstOrDefault(pi => pi.ProductId == item.ProductId);
                if (purchaseItem == null) throw new Exception($"Product ID {item.ProductId} was not part of this purchase.");
                if (item.Quantity > purchaseItem.Quantity) throw new Exception("Cannot return more quantity than was purchased.");

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
                    ReferenceId = purchaseReturn.PurchaseReturnId,
                    TransactionDate = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    Notes = $"Purchase Return: {purchaseReturn.ReturnNumber}"
                };
                
                _context.StockTransactions.Add(stockTx);
            }

            purchaseReturn.TotalRefundAmount = totalRefund;
            _context.PurchaseReturns.Add(purchaseReturn);

            var audit = new AuditLog
            {
                UserId = createdBy,
                Action = "ProcessPurchaseReturn",
                TableName = "PurchaseReturns",
                RecordId = purchaseId.ToString(),
                Description = $"Processed purchase return {purchaseReturn.ReturnNumber} for purchase {purchase.PurchaseNumber}."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return purchaseReturn;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}



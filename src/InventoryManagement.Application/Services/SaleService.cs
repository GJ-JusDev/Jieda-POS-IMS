using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Services;

public class SaleService : ISaleService
{
    private readonly IInventoryDbContext _context;

    public SaleService(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Sale> CreateSaleAsync(int customerId, int createdBy, IEnumerable<SaleItem> items, string paymentMethod, string? notes, bool allowNegativeStock = false)
    {
        var saleItems = items.ToList();
        if (!saleItems.Any()) throw new Exception("Sale must contain at least one item.");

        var sale = new Sale
        {
            InvoiceNumber = GenerateInvoiceNumber(),
            CustomerId = customerId,
            SaleDate = DateTime.UtcNow,
            PaymentMethod = paymentMethod,
            Notes = notes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            Status = SaleStatus.Completed // Assuming Point-of-Sale style immediate completion
        };

        decimal subtotal = 0;

        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            foreach (var item in saleItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null) throw new Exception($"Product ID {item.ProductId} not found.");

                // Check stock
                if (!allowNegativeStock)
                {
                                        var txs = await _context.StockTransactions
                        .Where(st => st.ProductId == item.ProductId)
                        .Select(st => st.Quantity)
                        .ToListAsync();
                    var currentStock = txs.Sum();

                    if (currentStock < item.Quantity)
                    {
                        throw new Exception($"Insufficient stock for product '{product.ProductName}'. Available: {currentStock}, Requested: {item.Quantity}");
                    }
                }

                item.UnitPrice = product.SellingPrice;
                item.TotalPrice = (item.Quantity * item.UnitPrice) - item.Discount;
                subtotal += item.TotalPrice;

                sale.SaleItems.Add(item);
            }

            sale.Subtotal = subtotal;
            sale.TotalAmount = subtotal - sale.Discount + sale.Tax;

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync(); // Save sale to generate ID

            // Generate Stock-Out transactions
            foreach (var item in sale.SaleItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                var stockTx = new StockTransaction
                {
                    ProductId = item.ProductId,
                    TransactionType = StockTransactionType.Sale,
                    Quantity = -item.Quantity, // Negative stock out
                    UnitCost = product?.CostPrice ?? 0,
                    ReferenceType = "Sale",
                    ReferenceId = sale.SaleId,
                    TransactionDate = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    Notes = $"Sale: {sale.InvoiceNumber}"
                };
                
                _context.StockTransactions.Add(stockTx);
            }

            var audit = new AuditLog
            {
                UserId = createdBy,
                Action = "CreateSale",
                TableName = "Sales",
                RecordId = sale.SaleId.ToString(),
                Description = $"Completed sale {sale.InvoiceNumber} for {sale.TotalAmount:C}."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return sale;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Sale?> GetSaleAsync(int saleId)
    {
        return await _context.Sales
            .Include(s => s.Customer)
            .Include(s => s.SaleItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.SaleId == saleId);
    }

    public async Task<IEnumerable<Sale>> GetAllSalesAsync()
    {
        return await _context.Sales
            .Include(s => s.Customer)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();
    }

    private string GenerateInvoiceNumber()
    {
        return $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.DTOs;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryDbContext _context;

    public InventoryService(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetCurrentStockAsync(int productId)
    {
                var tx = await _context.StockTransactions
            .Where(st => st.ProductId == productId)
            .Select(st => st.Quantity)
            .ToListAsync();
        return tx.Sum();
    }

    public async Task<IEnumerable<StockTransaction>> GetStockHistoryAsync(int productId)
    {
        return await _context.StockTransactions
            .Include(st => st.Product)
            .Where(st => st.ProductId == productId)
            .OrderByDescending(st => st.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockOverviewDto>> GetStockOverviewAsync(string searchTerm = "", int? categoryId = null, string statusFilter = "")
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

        var products = await query.ToListAsync();
        var productIds = products.Select(p => p.ProductId).ToList();

        // Get all relevant stock quantities
                var tx = await _context.StockTransactions
            .Where(st => productIds.Contains(st.ProductId))
            .Select(st => new { st.ProductId, st.Quantity })
            .ToListAsync();
            
        var stockQuantities = tx
            .GroupBy(st => st.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(st => st.Quantity));

        var overviewList = new List<StockOverviewDto>();

        foreach (var p in products)
        {
            stockQuantities.TryGetValue(p.ProductId, out decimal currentStock);

            var dto = new StockOverviewDto
            {
                ProductId = p.ProductId,
                SKU = p.SKU,
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                CategoryName = p.Category?.CategoryName ?? "",
                CurrentStock = currentStock,
                UnitSymbol = p.Unit?.Symbol ?? "",
                CostPrice = p.CostPrice,
                SellingPrice = p.SellingPrice,
                ReorderLevel = p.ReorderLevel
            };

            // Apply status filter locally since it's a computed property
            if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All" || dto.StockStatus == statusFilter)
            {
                overviewList.Add(dto);
            }
        }

        return overviewList.OrderBy(o => o.ProductName).ToList();
    }

    public async Task AddStockAdjustmentAsync(int productId, decimal quantity, string reason, int userId)
    {
        if (quantity == 0) throw new ArgumentException("Adjustment quantity cannot be zero.");

        var product = await _context.Products.FindAsync(productId);
        if (product == null) throw new Exception("Product not found.");

        var transactionType = quantity > 0 ? StockTransactionType.AdjustmentIncrease : StockTransactionType.AdjustmentDecrease;

        var transaction = new StockTransaction
        {
            ProductId = productId,
            TransactionType = transactionType,
            Quantity = quantity,
            UnitCost = product.CostPrice,
            ReferenceType = "Adjustment",
            TransactionDate = DateTime.UtcNow,
            Notes = reason,
            CreatedBy = userId
        };

        _context.StockTransactions.Add(transaction);
        
        // Create an audit log
        var audit = new AuditLog
        {
            UserId = userId,
            Action = "StockAdjustment",
            TableName = "StockTransactions",
            RecordId = productId.ToString(),
            Description = $"Adjusted stock for product {product.ProductName} by {quantity}. Reason: {reason}"
        };
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync();
    }
}


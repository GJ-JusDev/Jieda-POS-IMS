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
    private readonly IAuthorizationService _authzService;

    public InventoryService(IInventoryDbContext context, IAuthorizationService authzService)
    {
        _context = context;
        _authzService = authzService;
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

        // Get all relevant stock quantities, fetch to client first for SQLite compatibility
        var stockQuantitiesList = await _context.StockTransactions
            .Where(st => productIds.Contains(st.ProductId))
            .Select(st => new { st.ProductId, st.Quantity })
            .ToListAsync();

        var stockQuantities = stockQuantitiesList
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

            dto.StockStatus = GetStockStatus(currentStock, p.ReorderLevel);
            // Apply status filter locally since it's a computed property
            if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All" || dto.StockStatus == statusFilter)
            {
                overviewList.Add(dto);
            }
        }

        return overviewList.OrderBy(o => o.ProductName).ToList();
    }

    public async Task<InventoryManagement.Application.DTOs.Criteria.PagedResult<StockOverviewDto>> SearchInventoryAsync(InventoryManagement.Application.DTOs.Criteria.InventorySearchCriteria criteria)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var text = criteria.SearchText.Trim();
            query = query.Where(p => p.ProductName.Contains(text) || 
                                     p.SKU.Contains(text) || 
                                     (p.Barcode != null && p.Barcode.Contains(text)));
        }

        if (criteria.CategoryId.HasValue && criteria.CategoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == criteria.CategoryId.Value);
        }

        var products = await query.ToListAsync();
        var productIds = products.Select(p => p.ProductId).ToList();

        var stockQuantitiesList = await _context.StockTransactions
            .Where(st => productIds.Contains(st.ProductId))
            .Select(st => new { st.ProductId, st.Quantity })
            .ToListAsync();

        var stockQuantities = stockQuantitiesList
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

            // Use the centralized status logic!
            dto.StockStatus = GetStockStatus(currentStock, p.ReorderLevel);

            if (string.IsNullOrWhiteSpace(criteria.StockStatus) || criteria.StockStatus == "All" || dto.StockStatus == criteria.StockStatus)
            {
                overviewList.Add(dto);
            }
        }

        // In-memory sorting and pagination because we had to evaluate status in memory
        var sortedList = criteria.SortColumn switch
        {
            "ProductName" => criteria.SortDescending ? overviewList.OrderByDescending(o => o.ProductName) : overviewList.OrderBy(o => o.ProductName),
            "SKU" => criteria.SortDescending ? overviewList.OrderByDescending(o => o.SKU) : overviewList.OrderBy(o => o.SKU),
            "CurrentStock" => criteria.SortDescending ? overviewList.OrderByDescending(o => o.CurrentStock) : overviewList.OrderBy(o => o.CurrentStock),
            "Category" => criteria.SortDescending ? overviewList.OrderByDescending(o => o.CategoryName) : overviewList.OrderBy(o => o.CategoryName),
            _ => overviewList.OrderBy(o => o.ProductName)
        };

        var totalCount = sortedList.Count();
        var items = sortedList.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList();

        return new InventoryManagement.Application.DTOs.Criteria.PagedResult<StockOverviewDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };
    }

    public async Task AddStockAdjustmentAsync(int productId, StockTransactionType transactionType, decimal quantity, string reason, int userId)
    {
        if (!_authzService.HasPermission(Permission.CreateStockAdjustment))
            throw new UnauthorizedAccessException("You do not have permission to adjust stock.");

        if (quantity == 0) throw new ArgumentException("Adjustment quantity cannot be zero.");

        // Validate transaction type
        if (transactionType != StockTransactionType.Damage && 
            transactionType != StockTransactionType.Loss && 
            transactionType != StockTransactionType.Found && 
            transactionType != StockTransactionType.ManualAdjustment &&
            transactionType != StockTransactionType.OpeningBalance)
        {
            throw new ArgumentException("Invalid stock adjustment transaction type.");
        }

        // Validate quantity direction semantics
        if ((transactionType == StockTransactionType.Damage || transactionType == StockTransactionType.Loss) && quantity > 0)
            throw new ArgumentException($"{transactionType} adjustments must have a negative quantity.");
        if (transactionType == StockTransactionType.Found && quantity < 0)
            throw new ArgumentException($"{transactionType} adjustments must have a positive quantity.");

        var isInMemory = ((DbContext)_context).Database.ProviderName?.Contains("InMemory") == true;
        var dbTransaction = isInMemory ? null : await ((DbContext)_context).Database.BeginTransactionAsync();
        try
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) throw new Exception("Product not found.");
            
            if (!product.IsActive) throw new Exception("Cannot adjust stock for an inactive product.");

            // Calculate current stock to prevent negative stock
            var currentStock = await GetCurrentStockAsync(productId);
            var resultingStock = currentStock + quantity;
            
            if (resultingStock < 0)
            {
                throw new Exception($"Adjustment rejected: Insufficient stock. Current stock is {currentStock}.");
            }

            var transaction = new StockTransaction
            {
                ProductId = productId,
                TransactionType = transactionType,
                Quantity = quantity,
                UnitCost = product.CostPrice,
                ReferenceType = "StockAdjustment",
                TransactionDate = DateTime.UtcNow,
                Notes = reason,
                CreatedBy = userId
            };

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            
            // Assign ReferenceId after saving transaction to get generated ID if needed, 
            // but we can just use the transaction ID as its own reference or leave it null.
            transaction.ReferenceId = transaction.StockTransactionId; 

            // Create an audit log
            var audit = new AuditLog
            {
                UserId = userId,
                Action = "StockAdjustmentCreated",
                TableName = "StockTransactions",
                RecordId = transaction.StockTransactionId.ToString(),
                Description = $"{transactionType}: Product {product.ProductName}, {quantity:+#;-#;0}. Reason: {reason}"
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();
            if (dbTransaction != null)
                await dbTransaction.CommitAsync();
        }
        catch
        {
            if (dbTransaction != null)
                await dbTransaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (dbTransaction != null)
                await dbTransaction.DisposeAsync();
        }
    }

    public string GetStockStatus(decimal currentStock, decimal reorderLevel)
    {
        if (currentStock <= 0) return "Out of Stock";
        if (currentStock <= reorderLevel) return "Low Stock";
        return "In Stock";
    }

    public async Task<decimal> GetTotalInventoryValueAsync()
    {
        var inventory = await GetStockOverviewAsync();
        // Use cost price for valuation as per business rules
        return inventory.Sum(i => i.CurrentStock > 0 ? i.CurrentStock * i.CostPrice : 0);
    }
}


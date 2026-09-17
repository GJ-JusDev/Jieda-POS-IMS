using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Application.DTOs.Reports;
using InventoryManagement.Application.Interfaces;

namespace InventoryManagement.Application.Services;

public class ReportService : IReportService
{
    private readonly IInventoryDbContext _context;

    public ReportService(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<StockMovementReportDto>> GetStockMovementReportAsync(DateTime startDate, DateTime endDate, int? productId = null)
    {
        var query = _context.StockTransactions
            .Include(st => st.Product)
            .Where(st => st.TransactionDate >= startDate && st.TransactionDate <= endDate);

        if (productId.HasValue)
        {
            query = query.Where(st => st.ProductId == productId.Value);
        }

        // Simulating JOIN with Users for CreatedBy name since User might not be fully linked in StockTransaction depending on schema
        // For now, returning User ID string.
        
        var transactions = await query.OrderByDescending(st => st.TransactionDate).ToListAsync();
        
        return transactions.Select(st => new StockMovementReportDto
        {
            Date = st.TransactionDate,
            TransactionType = st.TransactionType.ToString(),
            Reference = st.ReferenceType + " " + st.ReferenceId,
            ProductName = st.Product?.ProductName ?? "Unknown",
            Quantity = st.Quantity,
            CreatedBy = st.CreatedBy.ToString() // In real app, join with Users table
        });
    }

    public async Task<IEnumerable<SalesReportDto>> GetSalesReportAsync(DateTime startDate, DateTime endDate)
    {
        var sales = await _context.Sales
            .Include(s => s.Customer)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();

        return sales.Select(s => new SalesReportDto
        {
            Date = s.SaleDate,
            InvoiceNumber = s.InvoiceNumber,
            CustomerName = s.Customer?.CustomerName ?? "Walk-in",
            TotalAmount = s.TotalAmount,
            CreatedBy = s.CreatedBy.ToString()
        });
    }

    public async Task<IEnumerable<PurchaseReportDto>> GetPurchaseReportAsync(DateTime startDate, DateTime endDate)
    {
        var purchases = await _context.Purchases
            .Include(p => p.Supplier)
            .Where(p => p.PurchaseDate >= startDate && p.PurchaseDate <= endDate)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();

        return purchases.Select(p => new PurchaseReportDto
        {
            Date = p.PurchaseDate,
            PurchaseNumber = p.PurchaseNumber,
            SupplierName = p.Supplier?.SupplierName ?? "Unknown",
            TotalAmount = p.TotalAmount,
            Status = p.Status.ToString()
        });
    }
}

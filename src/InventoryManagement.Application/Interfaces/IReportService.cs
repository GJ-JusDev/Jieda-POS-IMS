using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagement.Application.DTOs.Reports;

namespace InventoryManagement.Application.Interfaces;

public interface IReportService
{
    Task<IEnumerable<StockMovementReportDto>> GetStockMovementReportAsync(DateTime startDate, DateTime endDate, int? productId = null);
    Task<IEnumerable<SalesReportDto>> GetSalesReportAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<PurchaseReportDto>> GetPurchaseReportAsync(DateTime startDate, DateTime endDate);
}

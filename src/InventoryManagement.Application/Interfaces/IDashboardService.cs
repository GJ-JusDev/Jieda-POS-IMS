using System.Threading.Tasks;
using InventoryManagement.Application.DTOs.Dashboard;

namespace InventoryManagement.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardMetricsDto> GetDashboardMetricsAsync();
}

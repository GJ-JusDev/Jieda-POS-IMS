using System.Threading.Tasks;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Interfaces;

public interface IReceiptPdfService
{
    Task GenerateReceiptAsync(Sale sale, string filePath);
}

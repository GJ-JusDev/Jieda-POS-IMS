using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventoryManagement.Infrastructure.Services;

public class QuestPdfReceiptService : IReceiptPdfService
{
    public QuestPdfReceiptService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task GenerateReceiptAsync(Sale sale, string filePath)
    {
        return Task.Run(() =>
        {
            var document = Document.Create(container =>
            {
                // Thermal 80mm roll width is approx 3.14 inches.
                // We use PageSizes.Roll80 if available, otherwise define a custom size.
                var width = 3.14f; // inches
                
                container.Page(page =>
                {
                    page.ContinuousSize(width, QuestPDF.Infrastructure.Unit.Inch);
                    page.Margin(0.2f, QuestPDF.Infrastructure.Unit.Inch);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, sale));
                    page.Footer().Element(ComposeFooter);
                });
            });

            document.GeneratePdf(filePath);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("Jeida Crafts & Prints").SemiBold().FontSize(14);
            column.Item().AlignCenter().Text("INVOICE / SALES RECEIPT").FontSize(10);
            column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container, Sale sale)
    {
        container.PaddingVertical(5).Column(column =>
        {
            // Info section
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"Invoice: {sale.InvoiceNumber}");
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"Date: {sale.SaleDate.ToString("yyyy-MM-dd HH:mm")}");
            });
            column.Item().Row(row =>
            {
                var customerName = sale.Customer?.CustomerName ?? "Walk-in Customer";
                row.RelativeItem().Text($"Customer: {customerName}");
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            // Items table
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Item
                    columns.RelativeColumn(1); // Qty
                    columns.RelativeColumn(2); // Price
                    columns.RelativeColumn(2); // Total
                });

                table.Header(header =>
                {
                    header.Cell().Text("Item").SemiBold();
                    header.Cell().AlignRight().Text("Qty").SemiBold();
                    header.Cell().AlignRight().Text("Price").SemiBold();
                    header.Cell().AlignRight().Text("Total").SemiBold();
                });

                foreach (var item in sale.SaleItems)
                {
                    table.Cell().Text(item.Product?.ProductName ?? $"Item #{item.ProductId}");
                    table.Cell().AlignRight().Text(item.Quantity.ToString("0.##"));
                    table.Cell().AlignRight().Text(item.UnitPrice.ToString("C"));
                    table.Cell().AlignRight().Text(item.TotalPrice.ToString("C"));
                    
                    if (item.Discount > 0)
                    {
                        table.Cell().ColumnSpan(4).AlignRight().Text($"Discount: -{item.Discount.ToString("C")}").FontSize(9).FontColor(Colors.Grey.Medium);
                    }
                }
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            // Totals
            column.Item().AlignRight().Column(totals =>
            {
                totals.Item().Text($"Subtotal: {sale.Subtotal.ToString("C")}");
                if (sale.Discount > 0)
                    totals.Item().Text($"Discount: -{sale.Discount.ToString("C")}");
                if (sale.Tax > 0)
                    totals.Item().Text($"Tax: {sale.Tax.ToString("C")}");
                totals.Item().PaddingTop(2).Text($"Grand Total: {sale.TotalAmount.ToString("C")}").SemiBold().FontSize(12);
            });
            
            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            
            column.Item().Text($"Payment Method: {sale.PaymentMethod ?? "Cash"}").FontSize(10);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text("Thank you for your purchase!").Italic().FontSize(10);
    }
}

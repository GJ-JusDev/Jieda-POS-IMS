using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Microsoft.Win32;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Application.DTOs.Reports;
using InventoryManagement.UI.ViewModels.Base;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventoryManagement.UI.ViewModels.Reports;

public class ReportsViewModel : ViewModelBase
{
    private readonly IReportService _reportService;

    public ObservableCollection<StockMovementReportDto> StockMovements { get; set; } = new();
    public ObservableCollection<SalesReportDto> SalesReports { get; set; } = new();
    public ObservableCollection<PurchaseReportDto> PurchaseReports { get; set; } = new();

    private DateTime _startDate = DateTime.UtcNow.AddMonths(-1);
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

    private DateTime _endDate = DateTime.UtcNow;
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

    public ICommand GenerateReportCommand { get; }
    public ICommand ExportPdfCommand { get; }

    public ReportsViewModel(IReportService reportService)
    {
        _reportService = reportService;
        
        // QuestPDF requires a license setup for non-commercial/community use
        QuestPDF.Settings.License = LicenseType.Community;

        GenerateReportCommand = new RelayCommand(async _ => await GenerateReportsAsync());
        ExportPdfCommand = new RelayCommand(async _ => await ExportToPdfAsync());
    }

    public async Task InitializeAsync()
    {
        await GenerateReportsAsync();
    }

    private async Task GenerateReportsAsync()
    {
        try
        {
            var stock = await _reportService.GetStockMovementReportAsync(StartDate, EndDate);
            StockMovements.Clear();
            foreach (var s in stock) StockMovements.Add(s);

            var sales = await _reportService.GetSalesReportAsync(StartDate, EndDate);
            SalesReports.Clear();
            foreach (var s in sales) SalesReports.Add(s);

            var purchases = await _reportService.GetPurchaseReportAsync(StartDate, EndDate);
            PurchaseReports.Clear();
            foreach (var p in purchases) PurchaseReports.Add(p);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating reports: {ex.Message}");
        }
    }

    private Task ExportToPdfAsync()
    {
        if (StockMovements.Count == 0 && SalesReports.Count == 0 && PurchaseReports.Count == 0)
        {
            MessageBox.Show("There is no data to export. Please generate a report first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
            return Task.CompletedTask;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Report as PDF",
            Filter = "PDF Documents (*.pdf)|*.pdf",
            FileName = $"InventoryReport_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                GeneratePdfDocument(dialog.FileName);
                MessageBox.Show($"PDF successfully exported to:\n{dialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting PDF: {ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        return Task.CompletedTask;
    }

    private void GeneratePdfDocument(string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        })
        .GeneratePdf(filePath);
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("Inventory Management System").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Comprehensive Report ({StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy})");
            });
            row.ConstantItem(100).AlignRight().Text($"Generated: {DateTime.Now:MM/dd/yyyy}");
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(20);

            if (StockMovements.Count > 0)
            {
                column.Item().Text("Stock Movements").FontSize(14).SemiBold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Date
                        columns.RelativeColumn(3); // Type
                        columns.RelativeColumn(3); // Reference
                        columns.RelativeColumn(4); // Product
                        columns.RelativeColumn(2); // Qty
                    });
                    
                    table.Header(header =>
                    {
                        header.Cell().Text("Date").SemiBold();
                        header.Cell().Text("Type").SemiBold();
                        header.Cell().Text("Reference").SemiBold();
                        header.Cell().Text("Product").SemiBold();
                        header.Cell().AlignRight().Text("Quantity").SemiBold();
                    });
                    
                    foreach (var item in StockMovements)
                    {
                        table.Cell().Text(item.Date.ToString("g"));
                        table.Cell().Text(item.TransactionType);
                        table.Cell().Text(item.Reference);
                        table.Cell().Text(item.ProductName);
                        table.Cell().AlignRight().Text(item.Quantity.ToString("0.##"));
                    }
                });
            }

            if (SalesReports.Count > 0)
            {
                column.Item().Text("Sales Report").FontSize(14).SemiBold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });
                    
                    table.Header(header =>
                    {
                        header.Cell().Text("Date").SemiBold();
                        header.Cell().Text("Invoice No").SemiBold();
                        header.Cell().Text("Customer").SemiBold();
                        header.Cell().AlignRight().Text("Total Amount").SemiBold();
                    });
                    
                    decimal totalSales = 0;
                    foreach (var item in SalesReports)
                    {
                        table.Cell().Text(item.Date.ToString("d"));
                        table.Cell().Text(item.InvoiceNumber);
                        table.Cell().Text(item.CustomerName);
                        table.Cell().AlignRight().Text(item.TotalAmount.ToString("C"));
                        totalSales += item.TotalAmount;
                    }
                    
                    table.Cell().ColumnSpan(3).AlignRight().Text("Total Completed Sales:").SemiBold();
                    table.Cell().AlignRight().Text(totalSales.ToString("C")).SemiBold();
                });
            }

            if (PurchaseReports.Count > 0)
            {
                column.Item().Text("Purchase Report").FontSize(14).SemiBold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });
                    
                    table.Header(header =>
                    {
                        header.Cell().Text("Date").SemiBold();
                        header.Cell().Text("Supplier").SemiBold();
                        header.Cell().Text("Status").SemiBold();
                        header.Cell().AlignRight().Text("Total Amount").SemiBold();
                    });
                    
                    decimal totalPurchases = 0;
                    foreach (var item in PurchaseReports)
                    {
                        table.Cell().Text(item.Date.ToString("d"));
                        table.Cell().Text(item.SupplierName);
                        table.Cell().Text(item.Status);
                        table.Cell().AlignRight().Text(item.TotalAmount.ToString("C"));
                        if (item.Status == "Completed") totalPurchases += item.TotalAmount;
                    }
                    
                    table.Cell().ColumnSpan(3).AlignRight().Text("Total Completed Purchases:").SemiBold();
                    table.Cell().AlignRight().Text(totalPurchases.ToString("C")).SemiBold();
                });
            }
        });
    }
}


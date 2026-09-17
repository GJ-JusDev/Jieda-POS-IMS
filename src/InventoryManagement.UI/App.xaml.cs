using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using InventoryManagement.Infrastructure.Data;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Application.Services;
using InventoryManagement.Infrastructure.Services;
using InventoryManagement.UI.Views.Login;
using InventoryManagement.UI.ViewModels.Products;
using InventoryManagement.UI.ViewModels.Inventory;
using InventoryManagement.UI.ViewModels.Purchasing;
using InventoryManagement.UI.ViewModels.Sales;
using InventoryManagement.UI.ViewModels.Reports;
using InventoryManagement.UI.Views.Settings;
using InventoryManagement.UI.ViewModels.Settings;
using InventoryManagement.UI.Views.AuditLogs;
using InventoryManagement.UI.ViewModels.AuditLogs;
using InventoryManagement.UI.Views.Dashboard;
using InventoryManagement.UI.ViewModels.Purchasing;
using InventoryManagement.UI.Views.Purchasing;
using InventoryManagement.UI.ViewModels.Dashboard;

namespace InventoryManagement.UI;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    public IServiceProvider Services => _host.Services;

    public App()
    {
        // Global Exception Handling
        DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show($"An unexpected error occurred:\n{e.Exception.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent app crash
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            MessageBox.Show($"A fatal error occurred:\n{exception?.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            MessageBox.Show($"An unobserved task error occurred:\n{e.Exception.Message}", "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved();
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.SetBasePath(AppContext.BaseDirectory);
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                services.AddDbContext<InventoryDbContext>(options => options.UseSqlite(connectionString));
                services.AddTransient<IInventoryDbContext>(provider => provider.GetRequiredService<InventoryDbContext>());
                services.AddSingleton<IPasswordHasher, PasswordHasher>();
                services.AddSingleton<IAuthenticationService, AuthenticationService>();
                services.AddSingleton<IAuthorizationService, AuthorizationService>();
                services.AddTransient<ICategoryService, CategoryService>();
                services.AddTransient<IUnitService, UnitService>();
                services.AddTransient<IProductService, ProductService>();
                services.AddTransient<ISupplierService, SupplierService>();
                services.AddTransient<ICustomerService, CustomerService>();
                services.AddTransient<IInventoryService, InventoryService>();
                services.AddTransient<IPurchaseService, PurchaseService>();
                services.AddTransient<ISaleService, SaleService>();
                services.AddTransient<IReturnService, ReturnService>();
                services.AddTransient<IReportService, ReportService>();
                services.AddTransient<IBackupService, BackupService>();
                services.AddTransient<IAuditLogService, AuditLogService>();

                services.AddTransient<MainWindow>();
                services.AddTransient<LoginWindow>();
                
                services.AddTransient<ProductsViewModel>();
                services.AddTransient<PurchaseEditorViewModel>();
                services.AddTransient<PurchaseEditorWindow>();
                services.AddTransient<ProductEditorViewModel>();
                services.AddTransient<InventoryManagement.UI.Views.Products.ProductEditorWindow>();
                services.AddTransient<InventoryViewModel>();
                services.AddTransient<PurchasesViewModel>();
                services.AddTransient<SalesViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<AuditLogsViewModel>();
                services.AddTransient<DashboardViewModel>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var connString = config.GetConnectionString("DefaultConnection") ?? "";
            var dbPath = connString.Replace("Data Source=", "").Trim();
            if (!string.IsNullOrEmpty(dbPath))
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            dbContext.Database.Migrate();

                        // Seed base categories if none exist
            if (!dbContext.Categories.Any())
            {
                dbContext.Categories.AddRange(
                    new InventoryManagement.Domain.Entities.Category { CategoryName = "Bags", Description = "Manufactured bags and totes" },
                    new InventoryManagement.Domain.Entities.Category { CategoryName = "Crafts", Description = "Handmade crafts" },
                    new InventoryManagement.Domain.Entities.Category { CategoryName = "Prints", Description = "Printed materials and souvenirs" },
                    new InventoryManagement.Domain.Entities.Category { CategoryName = "Raw Materials", Description = "Raw materials for production (e.g. Fabric, Thread)" }
                );
                dbContext.SaveChanges();
            }
            else
            {
                // Ensure new categories exist
                var existingCats = dbContext.Categories.Select(c => c.CategoryName).ToList();
                if (!existingCats.Contains("Bags")) dbContext.Categories.Add(new InventoryManagement.Domain.Entities.Category { CategoryName = "Bags", Description = "Manufactured bags and totes" });
                if (!existingCats.Contains("Crafts")) dbContext.Categories.Add(new InventoryManagement.Domain.Entities.Category { CategoryName = "Crafts", Description = "Handmade crafts" });
                if (!existingCats.Contains("Prints")) dbContext.Categories.Add(new InventoryManagement.Domain.Entities.Category { CategoryName = "Prints", Description = "Printed materials and souvenirs" });
                
                // Optionally remove old ones if unused
                var electronics = dbContext.Categories.FirstOrDefault(c => c.CategoryName == "Electronics");
                if (electronics != null && !dbContext.Products.Any(p => p.CategoryId == electronics.CategoryId))
                    dbContext.Categories.Remove(electronics);

                var food = dbContext.Categories.FirstOrDefault(c => c.CategoryName == "Food & Beverage");
                if (food != null && !dbContext.Products.Any(p => p.CategoryId == food.CategoryId))
                    dbContext.Categories.Remove(food);

                dbContext.SaveChanges();
            }

                        if (!dbContext.Units.Any())
            {
                dbContext.Units.AddRange(
                    new InventoryManagement.Domain.Entities.Unit { UnitName = "pcs", Symbol = "pcs" },
                    new InventoryManagement.Domain.Entities.Unit { UnitName = "roll", Symbol = "roll" },
                    new InventoryManagement.Domain.Entities.Unit { UnitName = "m", Symbol = "m" },
                    new InventoryManagement.Domain.Entities.Unit { UnitName = "kg", Symbol = "kg" },
                    new InventoryManagement.Domain.Entities.Unit { UnitName = "box", Symbol = "box" }
                );
                dbContext.SaveChanges();
            }
            else
            {
                // Ensure new units exist
                var existingUnits = dbContext.Units.Select(u => u.UnitName).ToList();
                if (!existingUnits.Contains("pcs")) dbContext.Units.Add(new InventoryManagement.Domain.Entities.Unit { UnitName = "pcs", Symbol = "pcs" });
                if (!existingUnits.Contains("roll")) dbContext.Units.Add(new InventoryManagement.Domain.Entities.Unit { UnitName = "roll", Symbol = "roll" });
                if (!existingUnits.Contains("m")) dbContext.Units.Add(new InventoryManagement.Domain.Entities.Unit { UnitName = "m", Symbol = "m" });
                
                var oldPiece = dbContext.Units.FirstOrDefault(u => u.UnitName == "Piece");
                if (oldPiece != null) {
                    oldPiece.UnitName = "pcs";
                    oldPiece.Symbol = "pcs";
                }
                dbContext.SaveChanges();
            }

            if (!dbContext.Users.Any())
            {
                var role = new InventoryManagement.Domain.Entities.Role { RoleName = "Administrator", Description = "Full access" };
                dbContext.Roles.Add(role);
                
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var user = new InventoryManagement.Domain.Entities.User
                {
                    Username = "admin",
                    PasswordHash = passwordHasher.HashPassword("admin123"),
                    FullName = "System Administrator",
                    Role = role,
                    IsActive = true
                };
                dbContext.Users.Add(user);
                dbContext.SaveChanges();
            }
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        if (loginWindow.ShowDialog() == true)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
        else
        {
            Shutdown();
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            var backupService = _host.Services.GetRequiredService<IBackupService>();
            await backupService.CreateAutoBackupAsync();
            await backupService.EnforceRetentionPolicyAsync(7);
        }
        catch { }
        
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}











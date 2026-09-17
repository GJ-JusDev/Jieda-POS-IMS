using Microsoft.EntityFrameworkCore;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Infrastructure.Data;

using InventoryManagement.Application.Interfaces;

public class InventoryDbContext : DbContext, IInventoryDbContext {
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Unit> Units { get; set; } = null!;
        public DbSet<Product> Products { get; set; }
    public DbSet<ProductInput> ProductInputs { get; set; } = null!;
    public DbSet<ProductInputOption> ProductInputOptions { get; set; } = null!;
    public DbSet<ProductComponent> ProductComponents { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Purchase> Purchases { get; set; } = null!;
    public DbSet<PurchaseItem> PurchaseItems { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleItem> SaleItems { get; set; } = null!;
    public DbSet<StockTransaction> StockTransactions { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SalesReturn> SalesReturns { get; set; } = null!;
    public DbSet<SalesReturnItem> SalesReturnItems { get; set; } = null!;
    public DbSet<PurchaseReturn> PurchaseReturns { get; set; } = null!;
    public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
                modelBuilder.Entity<ProductComponent>()
            .HasOne(pc => pc.FinishedProduct)
            .WithMany(p => p.Components)
            .HasForeignKey(pc => pc.FinishedProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductComponent>()
            .HasOne(pc => pc.RawMaterial)
            .WithMany()
            .HasForeignKey(pc => pc.RawMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductInput>()
            .HasOne(pi => pi.Product)
            .WithMany(p => p.Inputs)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductInputOption>()
            .HasOne(pio => pio.ProductInput)
            .WithMany(pi => pi.Options)
            .HasForeignKey(pio => pio.ProductInputId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.SKU)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Barcode)
            .IsUnique();

        modelBuilder.Entity<Purchase>()
            .HasIndex(p => p.PurchaseNumber)
            .IsUnique();

        modelBuilder.Entity<Sale>()
            .HasIndex(s => s.InvoiceNumber)
            .IsUnique();
    }
}




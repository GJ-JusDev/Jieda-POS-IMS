# Offline Desktop Inventory Management System

A professional, reliable, maintainable offline Inventory Management System for Windows desktop, built with WPF, EF Core, and SQLite.

## Phase 1 - Foundation

This solution implements Phase 1 of the architecture plan.

### Technologies
- C# .NET 8 WPF
- Entity Framework Core with SQLite
- Clean Architecture (UI, Application, Domain, Infrastructure layers)
- Dependency Injection and Generic Host

### Getting Started

1. Ensure you have the .NET 8 SDK installed.
2. Build the solution:
   `ash
   dotnet build
   `
3. Run the application:
   `ash
   dotnet run --project src/InventoryManagement.UI/InventoryManagement.UI.csproj
   `
   
Upon first run, the application will automatically apply EF Core migrations and create the SQLite database in the standard C:\ProgramData\InventoryManagement\Data\Inventory.db directory.

### Project Structure
- InventoryManagement.UI: WPF views, ViewModels, Dependency Injection setup.
- InventoryManagement.Application: Business logic, Services, Interfaces, DTOs.
- InventoryManagement.Domain: Entities, Enums, core Domain models.
- InventoryManagement.Infrastructure: EF Core DbContext, Migrations, Repositories, specialized services.
- InventoryManagement.Tests: xUnit tests (to be populated in future phases).


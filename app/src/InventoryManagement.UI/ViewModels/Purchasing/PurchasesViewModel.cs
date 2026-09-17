using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.UI.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.UI.ViewModels.Purchasing;

public class PurchasesViewModel : ViewModelBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IAuthenticationService _authService;

    public ObservableCollection<Purchase> Purchases { get; set; } = new();

    private Purchase? _selectedPurchase;
    public Purchase? SelectedPurchase
    {
        get => _selectedPurchase;
        set { SetProperty(ref _selectedPurchase, value); }
    }

    public ICommand LoadCommand { get; }
    public ICommand CreateDraftCommand { get; }
    public ICommand EditDraftCommand { get; }
    public ICommand DeletePurchaseCommand { get; }
    public ICommand CompletePurchaseCommand { get; }

    public PurchasesViewModel(IPurchaseService purchaseService, ISupplierService supplierService, IAuthenticationService authService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _authService = authService;

        LoadCommand = new RelayCommand(async _ => await LoadPurchasesAsync());
        CreateDraftCommand = new RelayCommand(async _ => await CreateDraftAsync());
        CompletePurchaseCommand = new RelayCommand(async _ => await CompletePurchaseAsync(), _ => SelectedPurchase?.Status == PurchaseStatus.Draft);
        EditDraftCommand = new RelayCommand(async _ => await EditDraftAsync(), _ => SelectedPurchase?.Status == PurchaseStatus.Draft);
        DeletePurchaseCommand = new RelayCommand(async _ => await DeletePurchaseAsync(), _ => SelectedPurchase != null);
    }

    public async Task InitializeAsync()
    {
        await LoadPurchasesAsync();
    }

    private async Task LoadPurchasesAsync()
    {
        var result = await _purchaseService.GetAllPurchasesAsync();
        Purchases.Clear();
        foreach (var p in result)
        {
            Purchases.Add(p);
        }
    }

    private async Task CreateDraftAsync()
    {
        var editor = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Purchasing.PurchaseEditorWindow>();
        await ((PurchaseEditorViewModel)editor.DataContext).LoadDataAsync();
        editor.ShowDialog();
        await LoadPurchasesAsync();
    }

    private async Task EditDraftAsync()
    {
        if (SelectedPurchase == null) return;
        var editor = ((App)System.Windows.Application.Current).Services.GetRequiredService<InventoryManagement.UI.Views.Purchasing.PurchaseEditorWindow>();
        await ((PurchaseEditorViewModel)editor.DataContext).LoadDataAsync(SelectedPurchase);
        editor.ShowDialog();
        await LoadPurchasesAsync();
    }

    private async Task DeletePurchaseAsync()
    {
        if (SelectedPurchase == null) return;
        var result = MessageBox.Show($"Are you sure you want to delete PO {SelectedPurchase.PurchaseNumber}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _purchaseService.DeletePurchaseAsync(SelectedPurchase.PurchaseId);
                await LoadPurchasesAsync();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }

    private async Task CompletePurchaseAsync()
    {
        if (SelectedPurchase == null) return;

        var result = MessageBox.Show($"Complete Purchase {SelectedPurchase.PurchaseNumber}? This will lock the PO and adjust stock levels.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var userId = _authService.CurrentUser?.UserId ?? 1;
                await _purchaseService.CompletePurchaseAsync(SelectedPurchase.PurchaseId, userId);
                MessageBox.Show("Purchase completed successfully. Stock levels have been updated.");
                await LoadPurchasesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error completing purchase: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}


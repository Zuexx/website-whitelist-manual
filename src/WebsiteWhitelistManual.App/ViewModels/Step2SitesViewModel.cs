// src/WebsiteWhitelistManual.App/ViewModels/Step2SitesViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step2SitesViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;

    public Step2SitesViewModel(WizardConfigurationStore store)
    {
        _store = store;
        Sites = new ObservableCollection<AllowlistSite>(_store.Current.AllowlistSites);
        Sites.CollectionChanged += (_, _) => { Persist(); RecomputeBreakdown(); };
        RecomputeBreakdown();
    }

    public ObservableCollection<AllowlistSite> Sites { get; }

    [ObservableProperty]
    private string _newDomainInput = string.Empty;

    [ObservableProperty]
    private string? _newCategoryLabel;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private IReadOnlyList<CategoryBreakdownItem> _categoryBreakdown = Array.Empty<CategoryBreakdownItem>();

    [RelayCommand]
    private void AddSite()
    {
        if (!AllowlistSite.TryCreate(NewDomainInput, NewCategoryLabel, out var site, out var error))
        {
            ValidationError = error;
            return;
        }

        ValidationError = null;
        Sites.Add(site!);
        NewDomainInput = string.Empty;
        NewCategoryLabel = null;
    }

    [RelayCommand]
    private void RemoveSite(AllowlistSite site)
    {
        Sites.Remove(site);
    }

    private void Persist()
    {
        var current = _store.Current;
        _store.Update(new WizardConfiguration(current.BrowserTargets, Sites.ToList(), current.AdvancedOptions));
    }

    private void RecomputeBreakdown()
    {
        var total = Sites.Count;
        if (total == 0)
        {
            CategoryBreakdown = Array.Empty<CategoryBreakdownItem>();
            return;
        }

        CategoryBreakdown = Sites
            .GroupBy(s => s.CategoryLabel ?? "未分類")
            .Select(g => new CategoryBreakdownItem(g.Key, g.Count(), Math.Round(100.0 * g.Count() / total, 0)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }
}

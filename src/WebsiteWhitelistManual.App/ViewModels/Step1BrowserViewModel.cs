// src/WebsiteWhitelistManual.App/ViewModels/Step1BrowserViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step1BrowserViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;

    public Step1BrowserViewModel(WizardConfigurationStore store)
    {
        _store = store;
        var current = _store.Current.BrowserTargets;
        _isEdgeSelected = current.Any(t => t.Id == BrowserId.Edge);
        _isChromeSelected = current.Any(t => t.Id == BrowserId.Chrome);
    }

    [ObservableProperty]
    private bool _isEdgeSelected;

    [ObservableProperty]
    private bool _isChromeSelected;

    partial void OnIsEdgeSelectedChanged(bool value) => Persist();
    partial void OnIsChromeSelectedChanged(bool value) => Persist();

    private void Persist()
    {
        var targets = new List<BrowserTarget>();
        if (IsEdgeSelected) targets.Add(BrowserTarget.Edge);
        if (IsChromeSelected) targets.Add(BrowserTarget.Chrome);

        var current = _store.Current;
        _store.Update(new WizardConfiguration(targets, current.AllowlistSites, current.AdvancedOptions));
    }
}

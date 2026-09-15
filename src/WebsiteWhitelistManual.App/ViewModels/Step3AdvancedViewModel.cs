// src/WebsiteWhitelistManual.App/ViewModels/Step3AdvancedViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step3AdvancedViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;

    public Step3AdvancedViewModel(WizardConfigurationStore store)
    {
        _store = store;
        var current = _store.Current.AdvancedOptions;
        _disableIncognito = current.DisableIncognito;
        _disableAccountSwitching = current.DisableAccountSwitching;
        _disableDeveloperTools = current.DisableDeveloperTools;
    }

    [ObservableProperty]
    private bool _disableIncognito;

    [ObservableProperty]
    private bool _disableAccountSwitching;

    [ObservableProperty]
    private bool _disableDeveloperTools;

    partial void OnDisableIncognitoChanged(bool value) => Persist();
    partial void OnDisableAccountSwitchingChanged(bool value) => Persist();
    partial void OnDisableDeveloperToolsChanged(bool value) => Persist();

    private void Persist()
    {
        var current = _store.Current;
        _store.Update(new WizardConfiguration(
            current.BrowserTargets,
            current.AllowlistSites,
            new AdvancedOptionsState(DisableIncognito, DisableAccountSwitching, DisableDeveloperTools)));
    }
}

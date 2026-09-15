// src/WebsiteWhitelistManual.App/State/WizardConfigurationStore.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.State;

/// <summary>
/// Mutable, app-lifetime holder for the one WizardConfiguration instance the
/// five step pages accumulate edits into. WizardConfiguration itself is an
/// immutable value type (Core, tested independently) — this store is the
/// thin, App-only layer that lets pages replace it as the parent moves
/// through the wizard, and notifies anything watching (Dashboard, Step 4)
/// when that happens.
/// </summary>
public sealed class WizardConfigurationStore
{
    public WizardConfiguration Current { get; private set; } = new(
        browserTargets: Array.Empty<BrowserTarget>(),
        allowlistSites: Array.Empty<AllowlistSite>(),
        advancedOptions: new AdvancedOptionsState());

    public event Action? Changed;

    public void Update(WizardConfiguration next)
    {
        Current = next;
        Changed?.Invoke();
    }
}

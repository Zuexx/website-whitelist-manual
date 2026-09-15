namespace WebsiteWhitelistManual.Core.Models;

public sealed class WizardConfiguration
{
    public IReadOnlyList<BrowserTarget> BrowserTargets { get; }
    public IReadOnlyList<AllowlistSite> AllowlistSites { get; }
    public AdvancedOptionsState AdvancedOptions { get; }

    public WizardConfiguration(
        IReadOnlyList<BrowserTarget> browserTargets,
        IReadOnlyList<AllowlistSite> allowlistSites,
        AdvancedOptionsState advancedOptions)
    {
        BrowserTargets = browserTargets;
        AllowlistSites = allowlistSites;
        AdvancedOptions = advancedOptions;
    }

    /// <summary>
    /// Gates the Step 4 Apply button: at least one browser and one allowed
    /// site must be chosen before the wizard is allowed to write anything.
    /// </summary>
    public bool CanApply => BrowserTargets.Count > 0 && AllowlistSites.Count > 0;
}

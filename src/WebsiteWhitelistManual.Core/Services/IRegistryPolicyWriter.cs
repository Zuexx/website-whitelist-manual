using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryPolicyWriter
{
    void Apply(WizardConfiguration configuration);

    /// <summary>
    /// Deletes each target's entire policy key (URLBlocklist, URLAllowlist,
    /// and the three advanced-option DWORDs all go with it), reverting the
    /// browser to its default, unmanaged state. A no-op for any target that
    /// was never configured.
    /// </summary>
    void RemoveAll(IReadOnlyList<BrowserTarget> targets);
}

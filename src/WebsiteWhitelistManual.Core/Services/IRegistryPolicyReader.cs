using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryPolicyReader
{
    PolicySnapshot ReadSnapshot(IReadOnlyList<BrowserTarget> targets);
}

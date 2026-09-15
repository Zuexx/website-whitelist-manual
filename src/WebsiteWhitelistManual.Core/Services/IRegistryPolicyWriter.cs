using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryPolicyWriter
{
    void Apply(WizardConfiguration configuration);
}

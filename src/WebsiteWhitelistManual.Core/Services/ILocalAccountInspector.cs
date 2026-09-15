using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface ILocalAccountInspector
{
    IReadOnlyList<LocalAccountInfo> GetRelevantAccounts();
}

using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Abstractions;

public interface ILocalAccountSource
{
    IReadOnlyList<LocalAccountInfo> GetLocalAccounts();
}

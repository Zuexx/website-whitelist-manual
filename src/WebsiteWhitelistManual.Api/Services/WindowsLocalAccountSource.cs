using System.DirectoryServices.AccountManagement;
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Api.Services;

/// <summary>
/// Real local-machine-backed implementation of ILocalAccountSource, used
/// by Core's LocalAccountInspector to list Windows user accounts.
/// </summary>
public sealed class WindowsLocalAccountSource : ILocalAccountSource
{
    private static readonly HashSet<string> BuiltInAccountNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administrator", "Guest", "DefaultAccount", "WDAGUtilityAccount"
    };

    public IReadOnlyList<LocalAccountInfo> GetLocalAccounts()
    {
        using var context = new PrincipalContext(ContextType.Machine);
        var administratorNames = GetAdministratorGroupMemberNames(context);

        using var userPrincipalTemplate = new UserPrincipal(context);
        using var searcher = new PrincipalSearcher(userPrincipalTemplate);
        using var results = searcher.FindAll();

        var accounts = new List<LocalAccountInfo>();
        foreach (var result in results)
        {
            using var user = result as UserPrincipal;
            if (user?.SamAccountName is null)
            {
                continue;
            }

            accounts.Add(new LocalAccountInfo(
                AccountName: user.SamAccountName,
                IsAdministrator: administratorNames.Contains(user.SamAccountName),
                IsBuiltIn: BuiltInAccountNames.Contains(user.SamAccountName)));
        }

        return accounts;
    }

    private static HashSet<string> GetAdministratorGroupMemberNames(PrincipalContext context)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The built-in Administrators group is looked up by its well-known
        // SID (S-1-5-32-544), not the localized display name "Administrators"
        // — that name is only valid on English-locale Windows and would
        // silently fail to match on other UI languages, making every account
        // look like a non-administrator.
        using var administrators = GroupPrincipal.FindByIdentity(context, IdentityType.Sid, "S-1-5-32-544");
        if (administrators is null)
        {
            return names;
        }

        using var members = administrators.GetMembers();
        foreach (var member in members)
        {
            using var disposableMember = member;
            if (disposableMember.SamAccountName is not null)
            {
                names.Add(disposableMember.SamAccountName);
            }
        }

        return names;
    }
}

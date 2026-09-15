// src/WebsiteWhitelistManual.App/ViewModels/DashboardViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IRegistryPolicyReader _policyReader;
    private readonly ILocalAccountInspector _accountInspector;

    public DashboardViewModel(IRegistryPolicyReader policyReader, ILocalAccountInspector accountInspector)
    {
        _policyReader = policyReader;
        _accountInspector = accountInspector;
        Refresh();
    }

    [ObservableProperty]
    private PolicySnapshot _snapshot = new(Array.Empty<BrowserPolicySnapshot>());

    [ObservableProperty]
    private IReadOnlyList<LocalAccountInfo> _accounts = Array.Empty<LocalAccountInfo>();

    public bool IsProtectionActive => Snapshot.Browsers.Any(b => b.PolicyKeyExists && b.AllowedUrls.Count > 0);

    public int AllowedSiteCount => Snapshot.Browsers.SelectMany(b => b.AllowedUrls).Distinct().Count();

    [RelayCommand]
    private void Refresh()
    {
        Snapshot = _policyReader.ReadSnapshot(new[] { BrowserTarget.Edge, BrowserTarget.Chrome });
        Accounts = _accountInspector.GetAllAccounts();
        OnPropertyChanged(nameof(IsProtectionActive));
        OnPropertyChanged(nameof(AllowedSiteCount));
    }
}

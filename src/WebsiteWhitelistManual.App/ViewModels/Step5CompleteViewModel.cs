// src/WebsiteWhitelistManual.App/ViewModels/Step5CompleteViewModel.cs
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step5CompleteViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;
    private readonly IRegistryPolicyReader _policyReader;

    public Step5CompleteViewModel(WizardConfigurationStore store, IRegistryPolicyReader policyReader)
    {
        _store = store;
        _policyReader = policyReader;
        RunVerification();
    }

    [ObservableProperty]
    private IReadOnlyList<VerificationRow> _rows = Array.Empty<VerificationRow>();

    public bool AllPassed => Rows.Count > 0 && Rows.All(r => r.Passed);

    public bool HasAllowlistSites => _store.Current.AllowlistSites.Count > 0;

    [RelayCommand]
    private void RunVerification()
    {
        var configuration = _store.Current;
        var snapshot = _policyReader.ReadSnapshot(configuration.BrowserTargets.Count > 0
            ? configuration.BrowserTargets
            : new[] { BrowserTarget.Edge, BrowserTarget.Chrome });

        var rows = new List<VerificationRow>();
        var expectedDomains = configuration.AllowlistSites.Select(s => s.Domain).OrderBy(d => d).ToList();

        foreach (var browser in snapshot.Browsers)
        {
            rows.Add(new VerificationRow(
                $"{browser.BrowserId}：政策機碼已寫入",
                browser.PolicyKeyExists,
                browser.PolicyKeyExists ? "登錄機碼存在並可讀取。" : "尚未偵測到此瀏覽器的政策機碼。"));

            var actualDomains = browser.AllowedUrls.OrderBy(d => d).ToList();
            var domainsMatch = expectedDomains.SequenceEqual(actualDomains);
            rows.Add(new VerificationRow(
                $"{browser.BrowserId}：允許清單機碼已寫入並核對",
                domainsMatch,
                domainsMatch
                    ? $"URLAllowlist 內容與設定精靈一致，共 {actualDomains.Count} 筆。"
                    : "URLAllowlist 內容與設定精靈不一致，請重新套用一次。"));

            rows.Add(new VerificationRow(
                $"{browser.BrowserId}：無痕模式機碼已核對",
                browser.IncognitoDisabled == configuration.AdvancedOptions.DisableIncognito,
                $"IncognitoModeAvailability/InPrivateModeAvailability 目前值：{browser.IncognitoDisabled}"));
        }

        Rows = rows;
        OnPropertyChanged(nameof(AllPassed));
        OnPropertyChanged(nameof(HasAllowlistSites));
    }

    [RelayCommand]
    private void OpenAllowedSiteManually()
    {
        var firstSite = _store.Current.AllowlistSites.FirstOrDefault();
        if (firstSite is null)
        {
            return;
        }

        // Fire-and-forget by design: this button's only job is to open the
        // browser for the parent to look at. The app cannot read what
        // happens inside the browser afterward (no block-count/log API
        // exists for Chromium's URLBlocklist policy), so no result is
        // captured, polled, or checked off — see spec 資料流 step 5.
        Process.Start(new ProcessStartInfo($"https://{firstSite.Domain}") { UseShellExecute = true });
    }
}

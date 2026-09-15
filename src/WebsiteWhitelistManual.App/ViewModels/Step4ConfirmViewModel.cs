// src/WebsiteWhitelistManual.App/ViewModels/Step4ConfirmViewModel.cs
using System.IO;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step4ConfirmViewModel : ObservableObject
{
    private static readonly TimeSpan BackupTimeout = TimeSpan.FromSeconds(10);

    private readonly WizardConfigurationStore _store;
    private readonly IRegistryPolicyReader _policyReader;
    private readonly IRegistryBackupService _backupService;
    private readonly IRegistryPolicyWriter _policyWriter;

    public Step4ConfirmViewModel(
        WizardConfigurationStore store,
        IRegistryPolicyReader policyReader,
        IRegistryBackupService backupService,
        IRegistryPolicyWriter policyWriter)
    {
        _store = store;
        _policyReader = policyReader;
        _backupService = backupService;
        _policyWriter = policyWriter;
        RefreshDiff();
    }

    [ObservableProperty]
    private PolicySnapshot _currentSnapshot = new(Array.Empty<BrowserPolicySnapshot>());

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasAcknowledged;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string? _lastBackupDirectory;

    public WizardConfiguration Configuration => _store.Current;

    public bool CanApply => Configuration.CanApply && HasAcknowledged && !IsApplying;

    partial void OnHasAcknowledgedChanged(bool value) => OnPropertyChanged(nameof(CanApply));
    partial void OnIsApplyingChanged(bool value) => OnPropertyChanged(nameof(CanApply));

    [RelayCommand]
    private void RefreshDiff()
    {
        CurrentSnapshot = _policyReader.ReadSnapshot(Configuration.BrowserTargets.Count > 0
            ? Configuration.BrowserTargets
            : new[] { BrowserTarget.Edge, BrowserTarget.Chrome });
        OnPropertyChanged(nameof(Configuration));
        OnPropertyChanged(nameof(CanApply));
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        ErrorMessage = null;
        IsApplying = true;

        try
        {
            var backupBaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebsiteWhitelistManual", "Backups");

            var backupTask = Task.Run(() => _backupService.Backup(Configuration.BrowserTargets, backupBaseDirectory, DateTimeOffset.Now));
            var completed = await Task.WhenAny(backupTask, Task.Delay(BackupTimeout));

            if (completed != backupTask)
            {
                ErrorMessage = "備份逾時（超過 10 秒沒有回應），已中止套用。請重試一次；若持續逾時，請確認沒有其他程式鎖住登錄檔。";
                return;
            }

            var backupResult = await backupTask;
            if (!backupResult.Success)
            {
                ErrorMessage = $"備份失敗，已中止套用（未寫入任何變更）：{backupResult.ErrorMessage}";
                return;
            }

            LastBackupDirectory = backupResult.BackupDirectory;

            await Task.Run(() => _policyWriter.Apply(Configuration));
            RefreshDiff();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            ErrorMessage = $"寫入登錄檔時權限不足：{ex.Message}（理論上系統管理員權限已由 UAC 保證，若持續發生請重新以系統管理員身分啟動本工具）。";
        }
        finally
        {
            IsApplying = false;
        }
    }
}

using WebsiteWhitelistManual.Api.Contracts;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.Api.Endpoints;

public static class PolicyEndpoints
{
    private static readonly TimeSpan BackupTimeout = TimeSpan.FromSeconds(10);
    private static readonly BrowserTarget[] AllTargets = { BrowserTarget.Edge, BrowserTarget.Chrome };

    /// <summary>
    /// Runs the backup with the shared 10s timeout. Returns null on timeout
    /// (caller has no BackupResult to report a directory/paths from); check
    /// BackupResult.Success for an actual backup failure.
    /// </summary>
    private static async Task<BackupResult?> RunBackupWithTimeout(
        IRegistryBackupService backupService, IReadOnlyList<BrowserTarget> targets)
    {
        var backupBaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WebsiteWhitelistManual", "Backups");

        var backupTask = Task.Run(() => backupService.Backup(targets, backupBaseDirectory, DateTimeOffset.Now));
        var completed = await Task.WhenAny(backupTask, Task.Delay(BackupTimeout));

        return completed == backupTask ? await backupTask : null;
    }

    public static void MapPolicyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/policy/snapshot", (IRegistryPolicyReader reader) =>
        {
            var snapshot = reader.ReadSnapshot(AllTargets);
            return Results.Ok(PolicySnapshotResponse.FromDomain(snapshot));
        });

        app.MapPost("/api/policy/apply", async (
            ApplyPolicyRequest request,
            IRegistryPolicyReader reader,
            IRegistryBackupService backupService,
            IRegistryPolicyWriter writer) =>
        {
            var browserTargets = new List<BrowserTarget>();
            foreach (var id in request.BrowserIds)
            {
                if (!Enum.TryParse<BrowserId>(id, ignoreCase: true, out var parsed))
                {
                    return Results.BadRequest(new ApiErrorResponse($"Unknown browser id '{id}'. Expected 'Edge' or 'Chrome'."));
                }
                browserTargets.Add(BrowserTarget.FromId(parsed));
            }

            var allowlistSites = new List<AllowlistSite>();
            foreach (var site in request.AllowlistSites)
            {
                if (!AllowlistSite.TryCreate(site.Domain, site.CategoryLabel, out var created, out var error))
                {
                    return Results.BadRequest(new ApiErrorResponse(error!));
                }
                allowlistSites.Add(created!);
            }

            var advancedOptions = new AdvancedOptionsState(
                request.AdvancedOptions.DisableIncognito,
                request.AdvancedOptions.DisableAccountSwitching,
                request.AdvancedOptions.DisableDeveloperTools,
                request.AdvancedOptions.ForceYouTubeRestrict);

            var configuration = new WizardConfiguration(browserTargets, allowlistSites, advancedOptions);

            if (!configuration.CanApply)
            {
                return Results.BadRequest(new ApiErrorResponse("至少需要選擇一個瀏覽器和一個允許的網站才能套用。"));
            }

            var backupResult = await RunBackupWithTimeout(backupService, configuration.BrowserTargets);
            if (backupResult is null)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: null,
                    BackupFilePaths: Array.Empty<string>(),
                    ErrorMessage: "備份逾時（超過 10 秒沒有回應），已中止套用。請重試一次；若持續逾時，請確認沒有其他程式鎖住登錄檔。",
                    ResultingSnapshot: null));
            }

            if (!backupResult.Success)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: backupResult.BackupDirectory,
                    BackupFilePaths: backupResult.BackupFilePaths,
                    ErrorMessage: $"備份失敗，已中止套用（未寫入任何變更）：{backupResult.ErrorMessage}",
                    ResultingSnapshot: null));
            }

            try
            {
                await Task.Run(() => writer.Apply(configuration));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: backupResult.BackupDirectory,
                    BackupFilePaths: backupResult.BackupFilePaths,
                    ErrorMessage: $"寫入登錄檔時權限不足：{ex.Message}（理論上系統管理員權限已由 UAC 保證，若持續發生請重新啟動本工具）。",
                    ResultingSnapshot: null));
            }

            var resultingSnapshot = reader.ReadSnapshot(browserTargets);

            return Results.Ok(new ApplyPolicyResponse(
                Success: true,
                BackupDirectory: backupResult.BackupDirectory,
                BackupFilePaths: backupResult.BackupFilePaths,
                ErrorMessage: null,
                ResultingSnapshot: PolicySnapshotResponse.FromDomain(resultingSnapshot)));
        });

        app.MapDelete("/api/policy", async (
            IRegistryPolicyReader reader,
            IRegistryBackupService backupService,
            IRegistryPolicyWriter writer) =>
        {
            // Always targets both browsers, regardless of which one(s) the
            // wizard last applied to — "移除防護" means fully reverting the
            // machine, not just undoing the last session's choices.
            var backupResult = await RunBackupWithTimeout(backupService, AllTargets);
            if (backupResult is null)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: null,
                    BackupFilePaths: Array.Empty<string>(),
                    ErrorMessage: "備份逾時（超過 10 秒沒有回應），已中止移除。請重試一次；若持續逾時，請確認沒有其他程式鎖住登錄檔。",
                    ResultingSnapshot: null));
            }

            if (!backupResult.Success)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: backupResult.BackupDirectory,
                    BackupFilePaths: backupResult.BackupFilePaths,
                    ErrorMessage: $"備份失敗，已中止移除（未變更任何設定）：{backupResult.ErrorMessage}",
                    ResultingSnapshot: null));
            }

            try
            {
                await Task.Run(() => writer.RemoveAll(AllTargets));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: backupResult.BackupDirectory,
                    BackupFilePaths: backupResult.BackupFilePaths,
                    ErrorMessage: $"移除登錄檔設定時權限不足：{ex.Message}（理論上系統管理員權限已由 UAC 保證，若持續發生請重新啟動本工具）。",
                    ResultingSnapshot: null));
            }

            var resultingSnapshot = reader.ReadSnapshot(AllTargets);

            return Results.Ok(new ApplyPolicyResponse(
                Success: true,
                BackupDirectory: backupResult.BackupDirectory,
                BackupFilePaths: backupResult.BackupFilePaths,
                ErrorMessage: null,
                ResultingSnapshot: PolicySnapshotResponse.FromDomain(resultingSnapshot)));
        });
    }
}

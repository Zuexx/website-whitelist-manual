using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class RegistryBackupServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "wwm-backup-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static readonly DateTimeOffset FixedTimestamp = new(2026, 9, 15, 16, 42, 0, TimeSpan.Zero);

    [Fact]
    public void Backup_CreatesTimestampedSubfolder()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        Assert.True(Directory.Exists(result.BackupDirectory));
        Assert.Equal(Path.Combine(_tempDirectory, "20260915_164200"), result.BackupDirectory);
    }

    [Fact]
    public void Backup_InvokesRegExportForEachTarget()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        service.Backup(new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, _tempDirectory, FixedTimestamp);

        Assert.Equal(2, runner.Invocations.Count);
        Assert.All(runner.Invocations, invocation => Assert.Equal("reg.exe", invocation.FileName));
        Assert.Contains(runner.Invocations, i => i.Arguments.Contains(@"HKLM\SOFTWARE\Policies\Microsoft\Edge"));
        Assert.Contains(runner.Invocations, i => i.Arguments.Contains(@"HKLM\SOFTWARE\Policies\Google\Chrome"));
    }

    [Fact]
    public void Backup_ReturnsSuccessTrue_WhenAllExportsSucceed()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.BackupFilePaths);
    }

    [Fact]
    public void Backup_ReturnsSuccessFalse_WhenAnExportFails()
    {
        var runner = new FakeProcessRunner(new ProcessResult(1, string.Empty, "access denied"));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        Assert.False(result.Success);
        Assert.Equal("access denied", result.ErrorMessage);
    }

    [Fact]
    public void Backup_NamesEachFileAfterItsBrowser()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, _tempDirectory, FixedTimestamp);

        Assert.Contains(result.BackupFilePaths, p => p.EndsWith("Edge.reg"));
        Assert.Contains(result.BackupFilePaths, p => p.EndsWith("Chrome.reg"));
    }
}

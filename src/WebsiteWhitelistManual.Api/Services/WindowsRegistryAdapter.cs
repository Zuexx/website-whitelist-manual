using Microsoft.Win32;
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Api.Services;

/// <summary>
/// Real HKEY_LOCAL_MACHINE-backed implementation of IWindowsRegistry.
/// Its behavior is designed to match FakeWindowsRegistry's documented
/// semantics (WebsiteWhitelistManual.Core.Tests) exactly — this class
/// itself cannot be unit tested outside a real Windows machine.
/// </summary>
public sealed class WindowsRegistryAdapter : IWindowsRegistry
{
    public bool SubKeyExists(string subKeyPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key is not null;
    }

    public void EnsureSubKeyExists(string subKeyPath)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true);
    }

    public IReadOnlyList<string> GetValueNames(string subKeyPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValueNames() ?? Array.Empty<string>();
    }

    public string? GetStringValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public int? GetDwordValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(valueName) as int?;
    }

    public void SetStringValue(string subKeyPath, string valueName, string value)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not create or open registry key '{subKeyPath}'.");
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void SetDwordValue(string subKeyPath, string valueName, int value)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not create or open registry key '{subKeyPath}'.");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    public void DeleteValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

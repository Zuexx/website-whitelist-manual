namespace WebsiteWhitelistManual.Core.Abstractions;

/// <summary>
/// Thin abstraction over HKEY_LOCAL_MACHINE. Every path is relative to HKLM,
/// e.g. "SOFTWARE\Policies\Microsoft\Edge\URLAllowlist". The app never reads
/// or writes any other hive, so the hive itself is not a parameter.
/// </summary>
public interface IWindowsRegistry
{
    bool SubKeyExists(string subKeyPath);
    void EnsureSubKeyExists(string subKeyPath);
    IReadOnlyList<string> GetValueNames(string subKeyPath);
    string? GetStringValue(string subKeyPath, string valueName);
    int? GetDwordValue(string subKeyPath, string valueName);
    void SetStringValue(string subKeyPath, string valueName, string value);
    void SetDwordValue(string subKeyPath, string valueName, int value);
    void DeleteValue(string subKeyPath, string valueName);
}

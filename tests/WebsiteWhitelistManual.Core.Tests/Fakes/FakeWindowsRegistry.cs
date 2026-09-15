using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed class FakeWindowsRegistry : IWindowsRegistry
{
    private readonly Dictionary<string, Dictionary<string, object>> _store =
        new(StringComparer.OrdinalIgnoreCase);

    public bool SubKeyExists(string subKeyPath) => _store.ContainsKey(subKeyPath);

    public void EnsureSubKeyExists(string subKeyPath)
    {
        if (!_store.ContainsKey(subKeyPath))
        {
            _store[subKeyPath] = new Dictionary<string, object>();
        }
    }

    public IReadOnlyList<string> GetValueNames(string subKeyPath)
    {
        if (!_store.TryGetValue(subKeyPath, out var values))
        {
            return Array.Empty<string>();
        }
        return values.Keys.ToList();
    }

    public string? GetStringValue(string subKeyPath, string valueName)
    {
        if (_store.TryGetValue(subKeyPath, out var values) && values.TryGetValue(valueName, out var value))
        {
            return value as string;
        }
        return null;
    }

    public int? GetDwordValue(string subKeyPath, string valueName)
    {
        if (_store.TryGetValue(subKeyPath, out var values) && values.TryGetValue(valueName, out var value))
        {
            return value as int?;
        }
        return null;
    }

    public void SetStringValue(string subKeyPath, string valueName, string value)
    {
        EnsureSubKeyExists(subKeyPath);
        _store[subKeyPath][valueName] = value;
    }

    public void SetDwordValue(string subKeyPath, string valueName, int value)
    {
        EnsureSubKeyExists(subKeyPath);
        _store[subKeyPath][valueName] = value;
    }

    public void DeleteValue(string subKeyPath, string valueName)
    {
        if (_store.TryGetValue(subKeyPath, out var values))
        {
            values.Remove(valueName);
        }
    }
}

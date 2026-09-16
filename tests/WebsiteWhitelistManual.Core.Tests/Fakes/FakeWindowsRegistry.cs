using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed class FakeWindowsRegistry : IWindowsRegistry
{
    private readonly Dictionary<string, Dictionary<string, object>> _store =
        new(StringComparer.OrdinalIgnoreCase);

    public bool SubKeyExists(string subKeyPath) => _store.ContainsKey(subKeyPath);

    public void EnsureSubKeyExists(string subKeyPath)
    {
        // A real registry key's existence always implies its ancestors
        // exist, so creating a child key must also create every missing
        // ancestor along the way (without disturbing ones that already
        // exist).
        var segments = subKeyPath.Split('\\');
        for (var i = 0; i < segments.Length; i++)
        {
            var ancestorPath = string.Join('\\', segments.Take(i + 1));
            if (!_store.ContainsKey(ancestorPath))
            {
                _store[ancestorPath] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
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

    public void DeleteSubKeyTree(string subKeyPath)
    {
        var toRemove = _store.Keys
            .Where(key => key.Equals(subKeyPath, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(subKeyPath + "\\", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in toRemove)
        {
            _store.Remove(key);
        }
    }
}

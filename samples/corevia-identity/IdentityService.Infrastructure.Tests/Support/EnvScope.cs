namespace IdentityService.Infrastructure.Tests.Support;

/// <summary>Sets process environment variables for one test and restores them; tests using it must be in the "env" collection.</summary>
public sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _original = new();

    public EnvScope Set(string key, string? value)
    {
        if (!_original.ContainsKey(key))
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
        return this;
    }

    public void Dispose()
    {
        foreach (var (key, value) in _original)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

[CollectionDefinition("env", DisableParallelization = true)]
public sealed class EnvCollection;

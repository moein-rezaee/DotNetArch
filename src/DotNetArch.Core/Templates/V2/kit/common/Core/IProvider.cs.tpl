using {{Prefix}}.Kit.{{Area}}.Abstractions;
using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.{{Area}}.Core;

/// <summary>
/// Implemented by each <c>{{Prefix}}.Kit.{{Area}}.Providers.*</c> package and registered by that package's own
/// <c>Add&lt;Provider&gt;{{Area}}Provider()</c> extension. Core never references a provider package.
/// </summary>
public interface I{{Area}}Provider
{
    /// <summary>Name matched (case-insensitively) against <c>{{Area}}:Provider</c>.</summary>
    string Name { get; }

    /// <summary>Builds the implementation from configuration; throws <see cref="InvalidOperationException"/> naming the key when options are invalid.</summary>
    I{{Area}} Create(IConfiguration configuration);
}

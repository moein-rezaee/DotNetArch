using Corevia.Kit.MediaStorage.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Corevia.Kit.MediaStorage.Core;

/// <summary>
/// Implemented by each <c>Corevia.Kit.MediaStorage.Providers.*</c> package and registered into DI by
/// that package's own <c>AddCoreviaXxxProvider</c> extension. Core references no provider package;
/// <c>AddCoreviaMediaStorage</c> resolves the registered provider whose <see cref="Kind"/> matches
/// the configured <c>MediaStorage:Provider</c>.
/// </summary>
public interface IMediaStorageProvider
{
    MediaStorageProviderKind Kind { get; }

    /// <summary>Creates the storage from configuration; throws <see cref="InvalidOperationException"/> on invalid options.</summary>
    IMediaStorage Create(IConfiguration configuration);
}

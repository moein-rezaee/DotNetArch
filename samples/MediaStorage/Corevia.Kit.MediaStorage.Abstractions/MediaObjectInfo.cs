namespace Corevia.Kit.MediaStorage.Abstractions;

/// <summary>Metadata of a stored object. <see cref="Key"/> is the object key relative to the bucket.</summary>
public sealed record MediaObjectInfo(
    string Key,
    long Size,
    DateTimeOffset? LastModified,
    string? ETag,
    string? ContentType = null);

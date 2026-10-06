namespace {{Prefix}}.Kit.MediaStorage.Abstractions;

/// <summary>The requested object does not exist (error code <c>media_object_not_found</c>).</summary>
public sealed class MediaObjectNotFoundException : MediaStorageException
{
    public MediaObjectNotFoundException(string key, Exception? innerException = null)
        : base($"Media object '{key}' was not found.", "media_object_not_found", innerException)
    {
        Key = key;
    }

    public string Key { get; }
}

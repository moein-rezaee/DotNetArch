namespace {{Prefix}}.Kit.MediaStorage.Abstractions;

/// <summary>
/// A downloaded object. Owns the response stream: dispose it (or the stream) when done reading.
/// </summary>
public sealed class MediaObject : IDisposable
{
    private readonly IDisposable? _owner;

    public MediaObject(MediaObjectInfo info, Stream content, IDisposable? owner = null)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        _owner = owner;
    }

    public MediaObjectInfo Info { get; }

    public Stream Content { get; }

    public void Dispose()
    {
        Content.Dispose();
        _owner?.Dispose();
    }
}

namespace Corevia.Kit.MediaStorage.Abstractions;

/// <summary>
/// Object-store providers supported by <c>Corevia.Kit.MediaStorage</c>. The provider a deployment
/// uses is chosen in configuration (<c>MediaStorage:Provider</c>), never in service code.
/// </summary>
public enum MediaStorageProviderKind
{
    Minio = 0,
    RustFs = 1
}

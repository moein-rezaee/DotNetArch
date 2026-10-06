using System.Net;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;

namespace Corevia.Kit.MediaStorage.Abstractions;

/// <summary>
/// The requested object does not exist. Maps to the Kit <see cref="NotFoundException"/> (HTTP 404,
/// error code <c>media_object_not_found</c>).
/// </summary>
public sealed class MediaObjectNotFoundException : NotFoundException
{
    public MediaObjectNotFoundException(string key, Exception? innerException = null)
        : base($"Media object '{key}' was not found.", "media_object_not_found", innerException)
    {
        Key = key;
    }

    public string Key { get; }
}

/// <summary>
/// The object store failed or is unreachable, or rejected our credentials. Maps to the Kit
/// <see cref="ExternalServiceException"/>; <see cref="ServiceException.ErrorCode"/> is one of
/// <c>media_storage_error</c>, <c>media_storage_unavailable</c>, <c>media_storage_access_denied</c>
/// or <c>media_storage_bucket_not_found</c>.
/// </summary>
public sealed class MediaStorageException : ExternalServiceException
{
    public MediaStorageException(
        string message,
        HttpStatusCode statusCode = HttpStatusCode.BadGateway,
        string errorCode = "media_storage_error",
        Exception? innerException = null)
        : base(message, statusCode, errorCode, innerException)
    {
    }
}

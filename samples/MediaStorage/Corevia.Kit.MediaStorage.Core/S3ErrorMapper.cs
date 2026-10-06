using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Corevia.Kit.MediaStorage.Abstractions;

namespace Corevia.Kit.MediaStorage.Core;

/// <summary>Maps S3/transport failures onto the Kit ErrorHandling exception types.</summary>
internal static class S3ErrorMapper
{
    public static Exception Map(Exception exception, string operation, string? key)
    {
        switch (exception)
        {
            case AmazonS3Exception s3 when s3.ErrorCode == "NoSuchBucket":
                return new MediaStorageException(
                    $"Media storage bucket was not found while performing {operation}.",
                    HttpStatusCode.BadGateway, "media_storage_bucket_not_found", s3);
            case AmazonS3Exception s3 when key is not null &&
                                           (s3.StatusCode == HttpStatusCode.NotFound || s3.ErrorCode is "NoSuchKey" or "NotFound"):
                return new MediaObjectNotFoundException(key, s3);
            case AmazonServiceException svc when svc.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                return new MediaStorageException(
                    $"Media storage rejected the credentials or permissions while performing {operation} ({(int)svc.StatusCode}).",
                    HttpStatusCode.BadGateway, "media_storage_access_denied", svc);
            case AmazonServiceException svc when (int)svc.StatusCode >= 400:
                return new MediaStorageException(
                    $"Media storage {operation} failed with HTTP {(int)svc.StatusCode} ({svc.ErrorCode}).",
                    HttpStatusCode.BadGateway, "media_storage_error", svc);
            default:
                return new MediaStorageException(
                    $"Media storage is unavailable while performing {operation}: {exception.Message}",
                    HttpStatusCode.ServiceUnavailable, "media_storage_unavailable", exception);
        }
    }
}

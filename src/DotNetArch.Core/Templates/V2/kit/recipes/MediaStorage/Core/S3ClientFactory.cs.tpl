using Amazon;
using Amazon.Runtime;
using Amazon.S3;

namespace {{Prefix}}.Kit.MediaStorage.Core;

/// <summary>Builds the AWS SDK S3 client for any S3-compatible endpoint.</summary>
public static class S3ClientFactory
{
    /// <param name="options">Validated options.</param>
    /// <param name="handler">Optional HTTP handler (tests/stubs). Null uses the SDK default transport.</param>
    public static IAmazonS3 Create(S3StorageOptions options, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
            // S3-compatible stores do not all implement the newer default integrity checksums.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        if (handler is not null)
        {
            config.HttpClientFactory = new HandlerHttpClientFactory(handler);
        }

        AWSCredentials credentials = options.IsAnonymous
            ? new AnonymousAWSCredentials()
            : new BasicAWSCredentials(options.AccessKey!, options.SecretKey!);
        return new AmazonS3Client(credentials, config);
    }

    private sealed class HandlerHttpClientFactory : HttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public HandlerHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
            => new(_handler, disposeHandler: false);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }
}

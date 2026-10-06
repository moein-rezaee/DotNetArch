namespace DotNetArch.Core.Scaffolding.Kits;

/// <summary>Catalogue of the built-in kit areas. Any other area name produces a generic kit skeleton.</summary>
public static class KitRecipes
{
    public static readonly KitRecipe Cache = new(
        "Cache",
        "Provider-neutral key/value cache with optional expiry.",
        "cache کلید/مقدار مستقل از provider با انقضای اختیاری.",
        "`ICache`: `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`, `ExistsAsync`, `GetOrCreateAsync<T>` (extension). Values are JSON-serialisable; every I/O member takes a `CancellationToken`. `CacheException` for provider failures.",
        Array.Empty<KitPackage>(),
        new[]
        {
            new KitProviderRecipe(
                "InMemory",
                "in-process cache (single node, tests, development)",
                new[] { new KitPackage("Microsoft.Extensions.Caching.Memory", major => major >= 9 ? "9.0.20" : "8.0.1") },
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            new KitProviderRecipe(
                "Redis",
                "Redis server or cluster (StackExchange.Redis)",
                new[] { KitPackage.Fixed("StackExchange.Redis", "2.9.11") },
                new Dictionary<string, string> { ["Endpoint"] = "localhost:6379", ["Database"] = "0", ["KeyPrefix"] = "" },
                new Dictionary<string, string>
                {
                    ["Endpoint"] = "`host:port` of the server (required)",
                    ["Database"] = "database index, default 0",
                    ["KeyPrefix"] = "prefix added to every key, default empty",
                },
                new Dictionary<string, string> { ["REDIS_PASSWORD"] = "Redis password (optional for servers without auth)" }),
        });

    public static readonly KitRecipe MessageBroker = new(
        "MessageBroker",
        "Provider-neutral publish/subscribe messaging over named destinations.",
        "پیام‌رسانی publish/subscribe مستقل از provider روی destinationهای نام‌دار.",
        "`IMessageBroker`: `PublishAsync<T>(destination, message)` and `SubscribeAsync<T>(destination, handler)` returning an `IAsyncDisposable` subscription. Messages are JSON-serialised. `MessageBrokerException` for provider failures.",
        Array.Empty<KitPackage>(),
        new[]
        {
            new KitProviderRecipe(
                "RabbitMq",
                "RabbitMQ (RabbitMQ.Client 7)",
                new[] { KitPackage.Fixed("RabbitMQ.Client", "7.1.2") },
                new Dictionary<string, string> { ["Host"] = "localhost", ["Port"] = "5672", ["VirtualHost"] = "/" },
                new Dictionary<string, string>
                {
                    ["Host"] = "broker host name (required)",
                    ["Port"] = "AMQP port, default 5672",
                    ["VirtualHost"] = "virtual host, default `/`",
                },
                new Dictionary<string, string>
                {
                    ["RABBITMQ_USER"] = "broker user name",
                    ["RABBITMQ_PASSWORD"] = "broker password",
                }),
        });

    public static readonly KitRecipe MediaStorage = new(
        "MediaStorage",
        "Provider-neutral access to a deployment's media/object-file store (list, head, get, put, delete, public URLs).",
        "دسترسی مستقل از provider به فضای ذخیره‌سازی فایل/شیء (list، head، get، put، delete و آدرس عمومی).",
        "`IMediaStorage`: `ListAsync`/`ListKeysAsync`, `GetInfoAsync`/`ExistsAsync`, `GetAsync` (stream, dispose it), `PutAsync`, `DeleteAsync` (idempotent), `GetPublicUrl`. `MediaObjectNotFoundException` and `MediaStorageException` (error codes `media_storage_access_denied`, `media_storage_bucket_not_found`, `media_storage_error`, `media_storage_unavailable`).",
        new[] { KitPackage.Fixed("AWSSDK.S3", "4.0.104.1") },
        new[]
        {
            S3Provider("Minio", "MinIO", "MINIO"),
            S3Provider("RustFs", "RustFS", "RUSTFS"),
        });

    public static IReadOnlyList<KitRecipe> All { get; } = new[] { MediaStorage, Cache, MessageBroker };

    public static KitRecipe? Find(string area) =>
        All.FirstOrDefault(recipe => recipe.Area.Equals(area, StringComparison.OrdinalIgnoreCase));

    private static KitProviderRecipe S3Provider(string name, string product, string secretPrefix) => new(
        name,
        $"{product} (S3-compatible object store)",
        Array.Empty<KitPackage>(),
        new Dictionary<string, string>
        {
            ["Endpoint"] = "http://localhost:9000",
            ["Bucket"] = "media",
            ["Region"] = "us-east-1",
            ["ForcePathStyle"] = "true",
            ["PublicBaseUrl"] = "",
        },
        new Dictionary<string, string>
        {
            ["Endpoint"] = "internal service endpoint used for API calls, e.g. `http://minio:9000` (required)",
            ["Bucket"] = "single bucket name (required)",
            ["Region"] = "signing region, default `us-east-1`",
            ["ForcePathStyle"] = "path-style addressing, default `true`",
            ["PublicBaseUrl"] = "browser-facing base URL; required only when `GetPublicUrl` is used",
        },
        new Dictionary<string, string>
        {
            [$"{secretPrefix}_ACCESS_KEY"] = "access key (leave both keys empty for anonymous access to a public bucket)",
            [$"{secretPrefix}_SECRET_KEY"] = "secret key",
        });
}

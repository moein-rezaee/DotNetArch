namespace IdentityService.Application.Features.Identity.Options;

public class IdentityClientOptions
{
    public string? DefaultPublicClientId { get; set; }

    /// <summary>
    /// M2M (Machine-to-Machine) client configurations for service-to-service authentication.
    /// Each key is the client_id and value is the pre-shared secret (plain text, will be hashed on seed).
    /// </summary>
    public Dictionary<string, M2MClientConfig>? M2MClients { get; set; }
}

public class M2MClientConfig
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool Optional { get; set; }
    public string? RequiredSecretKey { get; set; }
    public string[] Scopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional tenant binding for service tokens. It is required when an M2M client
    /// is granted tenant-scoped Identity MCP admin capabilities.
    /// </summary>
    public Guid? TenantId { get; set; }
}

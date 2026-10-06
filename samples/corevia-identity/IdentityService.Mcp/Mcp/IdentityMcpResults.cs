namespace IdentityService.Mcp.Mcp;

public sealed record McpSelfProfileResult(
    Guid UserId,
    string PhoneLastFour,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? FirstName,
    string? LastName,
    string? Email,
    string? AvatarUrl);

public sealed record McpSelfSessionResult(
    Guid SessionId,
    DateTime CreatedAt,
    DateTime? EndedAt,
    string? DeviceInfo,
    string? IpAddressMasked,
    bool IsRevoked);

public sealed record McpUserAccessSummaryResult(
    Guid UserId,
    string PhoneLastFour,
    bool IsActive,
    IReadOnlyCollection<McpRoleResult> Roles,
    IReadOnlyCollection<McpTenantResult> Tenants);

public sealed record McpRoleResult(Guid Id, string Name, string DisplayName);

public sealed record McpTenantResult(Guid Id, string Name, string DisplayName, bool IsActive, bool IsDefault);

public sealed record McpSessionRevocationResult(
    Guid UserId,
    Guid? SessionId,
    bool OtherSessions,
    bool Accepted,
    Guid? ExcludedSessionId = null);

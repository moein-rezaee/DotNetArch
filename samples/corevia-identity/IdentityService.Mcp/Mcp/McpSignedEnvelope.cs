using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace IdentityService.Mcp.Mcp;

public interface IMcpNonceStore
{
    bool TryConsume(string purpose, string nonce, DateTimeOffset expiresAt);
}

public sealed class InMemoryMcpNonceStore : IMcpNonceStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nonces = new(StringComparer.Ordinal);

    public bool TryConsume(string purpose, string nonce, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(nonce) || expiresAt <= now)
        {
            return false;
        }

        foreach (var item in _nonces)
        {
            if (item.Value <= now)
            {
                _nonces.TryRemove(item.Key, out _);
            }
        }

        var key = $"{purpose}:{nonce}";
        return _nonces.TryAdd(key, expiresAt);
    }
}

public sealed class McpSignedEnvelopeVerifier
{
    private readonly IdentityMcpOptions _options;

    public McpSignedEnvelopeVerifier(IOptions<IdentityMcpOptions> options)
    {
        _options = options.Value;
    }

    public bool TryRead(
        string? compact,
        string purpose,
        out JsonObject payload,
        out string failureCode)
    {
        payload = new JsonObject();
        failureCode = string.Empty;

        if (string.IsNullOrWhiteSpace(compact))
        {
            failureCode = "missing_signed_context";
            return false;
        }

        if (compact.Length > 32768)
        {
            failureCode = "signed_context_too_large";
            return false;
        }

        var key = purpose switch
        {
            "delegation" => _options.DelegationSigningKey,
            "approval" => _options.ApprovalSigningKey,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            failureCode = "signed_context_key_unavailable";
            return false;
        }

        var parts = compact.Split('.', StringSplitOptions.None);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            failureCode = "invalid_signed_context_format";
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var providedSignature = Base64UrlDecode(parts[1]);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
            {
                failureCode = "invalid_signed_context_signature";
                return false;
            }

            payload = JsonNode.Parse(payloadBytes) as JsonObject ?? new JsonObject();
            if (!string.Equals(GetString(payload, "type"), purpose, StringComparison.Ordinal))
            {
                failureCode = "invalid_signed_context_type";
                return false;
            }

            return true;
        }
        catch (FormatException)
        {
            failureCode = "invalid_signed_context_format";
            return false;
        }
        catch (JsonException)
        {
            failureCode = "invalid_signed_context_payload";
            return false;
        }
        catch (ArgumentException)
        {
            failureCode = "invalid_signed_context_format";
            return false;
        }
    }

    public static string? GetString(JsonObject payload, string name)
    {
        if (!payload.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;
    }

    public static bool TryGetGuid(JsonObject payload, string name, out Guid value)
    {
        value = default;
        return Guid.TryParse(GetString(payload, name), out value);
    }

    public static bool TryGetExpiry(JsonObject payload, out DateTimeOffset expiresAt)
        => TryGetTimestamp(payload, "expiresAt", out expiresAt);

    public static bool TryGetTimestamp(JsonObject payload, string name, out DateTimeOffset timestamp)
    {
        return DateTimeOffset.TryParse(
            GetString(payload, name),
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    public static IReadOnlyCollection<string> GetStringArray(JsonObject payload, string name)
    {
        if (!payload.TryGetPropertyValue(name, out var node) || node is not JsonArray array)
        {
            return [];
        }

        return array
            .Select(item => item is JsonValue value && value.TryGetValue<string>(out var result) ? result : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}

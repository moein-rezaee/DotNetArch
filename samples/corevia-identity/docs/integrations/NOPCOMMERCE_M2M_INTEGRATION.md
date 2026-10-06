[فارسی](./NOPCOMMERCE_M2M_INTEGRATION.fa.md) | [Integrations Index](./README.md) | [Identity README](../../README.md)

# NopCommerce To IdentityService M2M Integration Guide

Last Updated: 2026-03-29
Owner: IdentityService Team
Status: Active
Version: 1.0.0

## Purpose
This guide is for an external NopCommerce consumer that needs to call OnlineShop services through the public Gateway using `IdentityService` machine-to-machine authentication.

## Scope
- Gateway-first token acquisition
- Direct-service URLs for troubleshooting only
- Sample token request and authenticated API call
- Token refresh pattern on the consumer side

## Prerequisites
- A provisioned M2M client id and client secret
- Reachability to the public Gateway endpoint
- Permission to call the required downstream scopes

## Configuration
Use the customer secret only in the consumer environment:

```dotenv
M2M_NOPCOMMERCE_SERVICE_SECRET=<provided-by-secret-store>
SERVER_IP=87.107.8.155
GATEWAY_BASE_URL=http://87.107.8.155:5280
M2M_CLIENT_ID=nopcommerce-service
M2M_CLIENT_SECRET=<provided-by-secret-store>
```

Gateway paths:
- Token: `/v1/api/Identity/M2M/Token`
- Order read: `/v1/api/Order`

Direct service URLs are for troubleshooting only:
- Identity: `http://87.107.8.155:5270`
- Customer: `http://87.107.8.155:5265`
- Catalog: `http://87.107.8.155:5267`
- Order: `http://87.107.8.155:5271`
- Invoice: `http://87.107.8.155:5272`

## Run / Usage
Acquire a token:

```bash
curl -X POST "http://87.107.8.155:5280/v1/api/Identity/M2M/Token" \
  -H "Content-Type: application/json" \
  -d '{
    "ClientId": "nopcommerce-service",
    "ClientSecret": "<provided-by-secret-store>"
  }'
```

Expected response shape:

```json
{
  "access_token": "<ACCESS_TOKEN>",
  "token_type": "Bearer",
  "expires_in": 900,
  "scope": "order.write orders.read orders.status invoices.read invoices.create customer.read customer.write catalog.read"
}
```

Call a protected endpoint through Gateway:

```bash
TOKEN="<ACCESS_TOKEN>"

curl -X GET "http://87.107.8.155:5280/v1/api/Order?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

Consumer-side refresh example:

```csharp
public sealed class NopM2MTokenProvider
{
  private readonly HttpClient _httpClient;
  private string? _accessToken;
  private DateTimeOffset _expiresAtUtc;

  public NopM2MTokenProvider(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  public async Task<string> GetValidTokenAsync(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow < _expiresAtUtc.AddSeconds(-60))
    {
      return _accessToken;
    }

    var requestBody = new
    {
      ClientId = "nopcommerce-service",
      ClientSecret = Environment.GetEnvironmentVariable("M2M_NOPCOMMERCE_SERVICE_SECRET")
    };

    using var response = await _httpClient.PostAsJsonAsync(
      "http://87.107.8.155:5280/v1/api/Identity/M2M/Token",
      requestBody,
      cancellationToken);

    response.EnsureSuccessStatusCode();

    var tokenResponse = await response.Content.ReadFromJsonAsync<M2MTokenResponse>(cancellationToken: cancellationToken)
      ?? throw new InvalidOperationException("Token response is empty.");

    _accessToken = tokenResponse.AccessToken;
    _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

    return _accessToken;
  }

  private sealed record M2MTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string Scope);
}
```

## Validation / Verification
- Prefer the public Gateway path, not direct service URLs
- Token lifetime is 15 minutes (`expires_in = 900`)
- Refresh the token before expiry
- Verify the expected scopes match Gateway route requirements

## Troubleshooting
- If a request works directly against a service but fails behind Gateway with `403`, check scope naming drift between Identity and Gateway

## Change Log
- 2026-04-11: Unified M2M scope format to dot-separated names (for example, `order.write`).
- 2026-03-29: Moved customer-facing NopCommerce guide under `IdentityService/docs/integrations/`.

## Ownership
- IdentityService Team

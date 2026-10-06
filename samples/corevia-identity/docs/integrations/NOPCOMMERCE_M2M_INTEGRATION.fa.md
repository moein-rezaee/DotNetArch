[English](./NOPCOMMERCE_M2M_INTEGRATION.md) | [فهرست integrationها](./README.fa.md) | [README هویت](../../README.fa.md)

# راهنمای اتصال M2M بین NopCommerce و IdentityService

Last Updated: 2026-03-29
Owner: IdentityService Team
Status: Active
Version: 1.0.0

## Purpose
این راهنما برای مشتری بیرونی NopCommerce است که می‌خواهد از طریق Gateway عمومی و با احراز هویت machine-to-machine مبتنی بر `IdentityService` به سرویس‌های OnlineShop متصل شود.

## Scope
- دریافت توکن از مسیر Gateway
- آدرس‌های مستقیم سرویس فقط برای عیب‌یابی
- نمونه درخواست دریافت توکن و فراخوانی API محافظت‌شده
- الگوی refresh توکن در سمت مصرف‌کننده

## Prerequisites
- داشتن `client id` و `client secret` تخصیص‌داده‌شده
- دسترسی شبکه‌ای به Gateway عمومی
- مجوز استفاده از scopeهای لازم

## Configuration
از secret مشتری فقط در محیط مصرف‌کننده استفاده کنید:

```dotenv
M2M_NOPCOMMERCE_SERVICE_SECRET=<provided-by-secret-store>
SERVER_IP=87.107.8.155
GATEWAY_BASE_URL=http://87.107.8.155:5280
M2M_CLIENT_ID=nopcommerce-service
M2M_CLIENT_SECRET=<provided-by-secret-store>
```

مسیرهای Gateway:
- دریافت توکن: `/v1/api/Identity/M2M/Token`
- خواندن سفارش: `/v1/api/Order`

آدرس‌های مستقیم سرویس فقط برای تست مستقیم هستند:
- Identity: `http://87.107.8.155:5270`
- Customer: `http://87.107.8.155:5265`
- Catalog: `http://87.107.8.155:5267`
- Order: `http://87.107.8.155:5271`
- Invoice: `http://87.107.8.155:5272`

## Run / Usage
دریافت توکن:

```bash
curl -X POST "http://87.107.8.155:5280/v1/api/Identity/M2M/Token" \
  -H "Content-Type: application/json" \
  -d '{
    "ClientId": "nopcommerce-service",
    "ClientSecret": "<provided-by-secret-store>"
  }'
```

نمونه پاسخ:

```json
{
  "access_token": "<ACCESS_TOKEN>",
  "token_type": "Bearer",
  "expires_in": 900,
  "scope": "order.write orders.read orders.status invoices.read invoices.create customer.read customer.write catalog.read"
}
```

فراخوانی یک endpoint محافظت‌شده از پشت Gateway:

```bash
TOKEN="<ACCESS_TOKEN>"

curl -X GET "http://87.107.8.155:5280/v1/api/Order?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

نمونه refresh در سمت مصرف‌کننده:

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
- مسیر اصلی باید Gateway عمومی باشد، نه آدرس مستقیم سرویس
- توکن ۱۵ دقیقه اعتبار دارد (`expires_in = 900`)
- قبل از انقضا باید توکن جدید گرفته شود
- scopeهای دریافتی باید با route requirementهای Gateway هم‌راستا باشند

## Troubleshooting
- اگر فراخوانی مستقیم به سرویس جواب بدهد ولی از پشت Gateway با `403` رد شود، معمولاً مشکل از ناهماهنگی نام scope بین Identity و Gateway است

## Change Log
- 2026-04-11: یکپارچه‌سازی فرمت scopeهای M2M به حالت نقطه‌ای (مثال: `order.write`).
- 2026-03-29: انتقال راهنمای مشتری‌محور NopCommerce به `IdentityService/docs/integrations/`.

## Ownership
- تیم IdentityService

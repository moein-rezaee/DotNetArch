using System.Net;
using System.Text.Json;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using Corevia.Kit.HttpClientRestExtension.Abstractions;
using Corevia.Kit.HttpClientRestExtension.Core;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Infrastructure.Otp;

/// <summary>
/// OTP service client built on the Corevia.Kit.Http REST client (named provider
/// <see cref="ProviderName"/>, registered in <see cref="DependencyInjection"/>). Failure mapping is
/// identical to the previous hand-rolled HttpClient wrapper.
/// </summary>
public class OtpRestClient : IOtpClient
{
    public const string ProviderName = "IdentityOtp";

    private readonly IHttpRestClient _http;

    public OtpRestClient(IHttpRestClientFactory factory)
    {
        _http = factory.Get(ProviderName);
    }

    public async Task SendCodeAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { PhoneNumber = phoneNumber };
            await _http.PostAsync("v1/api/Otp/Send", payload, ct: cancellationToken);
        }
        catch (HttpRequestFailedException ex)
        {
            if (ex.StatusCodeValue == HttpStatusCode.BadRequest)
            {
                var (message, errorCode) = ReadProblem(ex.ResponseBody);
                throw new BadRequestException(message, errorCode);
            }

            throw new ExternalServiceException("Failed to send verification code.", ex.StatusCodeValue);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("OTP service is unavailable.", ex.StatusCode ?? HttpStatusCode.BadGateway, innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException("OTP service request timed out.", HttpStatusCode.GatewayTimeout, innerException: ex);
        }
    }

    public async Task<bool> VerifyCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { PhoneNumber = phoneNumber, Code = code };
            var body = await _http.PostAsync("v1/api/Otp/Verify", payload, ct: cancellationToken);
            return JsonSerializer.Deserialize<bool>(body);
        }
        catch (JsonException ex)
        {
            // A 2xx with a non-boolean body (for example a proxy error page) is an upstream failure, not a 500 from Identity.
            throw new ExternalServiceException("OTP service returned an invalid response.", HttpStatusCode.BadGateway, innerException: ex);
        }
        catch (HttpRequestFailedException ex)
        {
            if (ex.StatusCodeValue == HttpStatusCode.BadRequest)
            {
                return false;
            }

            throw new ExternalServiceException("Failed to verify code with OTP service.", ex.StatusCodeValue);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("OTP service is unavailable.", ex.StatusCode ?? HttpStatusCode.BadGateway, innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException("OTP service request timed out.", HttpStatusCode.GatewayTimeout, innerException: ex);
        }
    }

    private static (string Message, string ErrorCode) ReadProblem(string body)
    {
        var fallbackMessage = "ارسال کد تایید برای این شماره امکان‌پذیر نیست.";
        var fallbackErrorCode = "otp_send_bad_request";

        try
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return (fallbackMessage, fallbackErrorCode);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var message = !string.IsNullOrWhiteSpace(detail) ? detail : (!string.IsNullOrWhiteSpace(title) ? title : fallbackMessage);

            var errorCode = root.TryGetProperty("errorCode", out var ec) ? ec.GetString() : null;
            return (message ?? fallbackMessage, errorCode ?? fallbackErrorCode);
        }
        catch
        {
            return (fallbackMessage, fallbackErrorCode);
        }
    }
}

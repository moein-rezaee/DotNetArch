using System.Net;
using System.Text;
using System.Text.Json;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Otp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Corevia.Kit.ServiceDiscoveryExtension.Abstractions;

namespace IdentityService.Infrastructure.Tests.Otp;

/// <summary>
/// Real Kit Http REST client + the real Identity DI wiring, with only the primary HttpMessageHandler replaced by a stub,
/// so URL/body construction, the Kit failure type and Identity's mapping to Corevia.Kit.ErrorHandling exceptions are all exercised.
/// </summary>
public sealed class OtpRestClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri? Uri, string Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri, body));
            return await respond(request, cancellationToken);
        }
    }

    private sealed class FakeDiscovery(ServiceEndpoint? endpoint, bool throws = false) : IServiceDiscoveryService
    {
        public List<string> Asked { get; } = new();

        public Task<ServiceEndpoint?> ResolveOneAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            Asked.Add(serviceName);
            return throws ? throw new InvalidOperationException("consul down") : Task.FromResult(endpoint);
        }

        public Task<IReadOnlyCollection<ServiceEndpoint>> ResolveAllAsync(string serviceName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ServiceEndpoint>>(Array.Empty<ServiceEndpoint>());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (ServiceProvider Provider, StubHandler Handler) Build(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        Dictionary<string, string?>? settings = null,
        IServiceDiscoveryService? discovery = null)
    {
        var handler = new StubHandler(respond);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new()).Build();
        var services = new ServiceCollection().AddLogging();
        if (discovery is not null)
        {
            services.AddSingleton(discovery);
        }

        services.AddIdentityInfrastructure(configuration);
        services.AddHttpClient(OtpRestClient.ProviderName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return (services.BuildServiceProvider(), handler);
    }

    private static IOtpClient Client(ServiceProvider sp) => sp.CreateScope().ServiceProvider.GetRequiredService<IOtpClient>();

    // The Kit serializer decides property casing; the wire contract is case-insensitive (ASP.NET model binding).
    private static string? Prop(string body, string name)
        => JsonDocument.Parse(body).RootElement.EnumerateObject()
            .Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value.GetString();

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond(HttpStatusCode status, string body = "")
        => (_, _) => Task.FromResult(Json(status, body));

    // ------------------------------------------------------------------ send
    [Fact]
    public async Task Send_posts_the_phone_number_to_the_otp_send_route_on_the_configured_base_url()
    {
        var (sp, handler) = Build(Respond(HttpStatusCode.OK), new() { ["OTP_BASE_URL"] = "http://otp.test:7000" });

        await Client(sp).SendCodeAsync("09121234567");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://otp.test:7000/v1/api/Otp/Send", request.Uri!.ToString());
        Assert.Equal("09121234567", Prop(request.Body, "PhoneNumber"));
    }

    [Fact]
    public async Task Send_bad_request_with_a_problem_document_becomes_BadRequestException_with_its_detail_and_error_code()
    {
        var (sp, _) = Build(Respond(HttpStatusCode.BadRequest, """{"detail":"too many requests","title":"t","errorCode":"otp_rate_limited"}"""));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Client(sp).SendCodeAsync("09121234567"));

        Assert.Equal("too many requests", ex.Message);
        Assert.Equal("otp_rate_limited", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task Send_bad_request_falls_back_to_title_then_to_the_default_message_and_code()
    {
        var (titleOnly, _) = Build(Respond(HttpStatusCode.BadRequest, """{"title":"only title"}"""));
        var (empty, _) = Build(Respond(HttpStatusCode.BadRequest, ""));
        var (notJson, _) = Build(Respond(HttpStatusCode.BadRequest, "<html>nope</html>"));

        var a = await Assert.ThrowsAsync<BadRequestException>(() => Client(titleOnly).SendCodeAsync("09121234567"));
        var b = await Assert.ThrowsAsync<BadRequestException>(() => Client(empty).SendCodeAsync("09121234567"));
        var c = await Assert.ThrowsAsync<BadRequestException>(() => Client(notJson).SendCodeAsync("09121234567"));

        Assert.Equal(("only title", "otp_send_bad_request"), (a.Message, a.ErrorCode));
        Assert.Equal("otp_send_bad_request", b.ErrorCode);
        Assert.Equal("otp_send_bad_request", c.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(b.Message));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Send_other_http_failures_become_ExternalServiceException_carrying_the_upstream_status(HttpStatusCode status)
    {
        var (sp, _) = Build(Respond(status, "upstream body must not leak"));

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(sp).SendCodeAsync("09121234567"));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("external_service_error", ex.ErrorCode);
        Assert.Equal("Failed to send verification code.", ex.Message);
        Assert.DoesNotContain("leak", ex.Message);
    }

    [Fact]
    public async Task Send_network_failure_becomes_ExternalServiceException_bad_gateway_with_the_inner_exception()
    {
        var (sp, _) = Build((_, _) => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(sp).SendCodeAsync("09121234567"));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal("OTP service is unavailable.", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Send_timeout_becomes_ExternalServiceException_gateway_timeout()
    {
        var (sp, _) = Build((_, _) => throw new TaskCanceledException("timed out"));

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(sp).SendCodeAsync("09121234567"));

        Assert.Equal(HttpStatusCode.GatewayTimeout, ex.StatusCode);
        Assert.Equal("OTP service request timed out.", ex.Message);
    }

    [Fact]
    public async Task Send_caller_cancellation_is_not_mapped_to_a_timeout()
    {
        using var cts = new CancellationTokenSource();
        var (sp, _) = Build(async (_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(sp).SendCodeAsync("09121234567", cts.Token));
    }

    // ------------------------------------------------------------------ verify
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Verify_returns_the_boolean_answer_from_the_otp_verify_route(string body, bool expected)
    {
        var (sp, handler) = Build(Respond(HttpStatusCode.OK, body), new() { ["OTP_BASE_URL"] = "http://otp.test:7000" });

        var result = await Client(sp).VerifyCodeAsync("09121234567", "12345");

        Assert.Equal(expected, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://otp.test:7000/v1/api/Otp/Verify", request.Uri!.ToString());
        Assert.Equal("09121234567", Prop(request.Body, "PhoneNumber"));
        Assert.Equal("12345", Prop(request.Body, "Code"));
    }

    [Fact]
    public async Task Verify_bad_request_means_the_code_is_wrong_not_an_error()
    {
        var (sp, _) = Build(Respond(HttpStatusCode.BadRequest, """{"detail":"wrong code"}"""));

        Assert.False(await Client(sp).VerifyCodeAsync("09121234567", "00000"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Verify_other_http_failures_become_ExternalServiceException_carrying_the_upstream_status(HttpStatusCode status)
    {
        var (sp, _) = Build(Respond(status));

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(sp).VerifyCodeAsync("09121234567", "12345"));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("Failed to verify code with OTP service.", ex.Message);
    }

    [Fact]
    public async Task Verify_network_failure_and_timeout_map_to_bad_gateway_and_gateway_timeout()
    {
        var (down, _) = Build((_, _) => throw new HttpRequestException("refused"));
        var (slow, _) = Build((_, _) => throw new TaskCanceledException());

        var a = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(down).VerifyCodeAsync("09121234567", "12345"));
        var b = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(slow).VerifyCodeAsync("09121234567", "12345"));

        Assert.Equal(HttpStatusCode.BadGateway, a.StatusCode);
        Assert.Equal(HttpStatusCode.GatewayTimeout, b.StatusCode);
    }

    [Fact]
    public async Task Verify_garbage_success_body_is_an_external_service_failure_not_a_raw_JsonException()
    {
        var (sp, _) = Build(Respond(HttpStatusCode.OK, "<html>gateway page</html>"));

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(sp).VerifyCodeAsync("09121234567", "12345"));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    // ------------------------------------------------------------------ wiring: base address + timeout
    private static HttpClient OtpHttpClient(ServiceProvider sp) => sp.GetRequiredService<IHttpClientFactory>().CreateClient(OtpRestClient.ProviderName);

    [Fact]
    public void Base_address_defaults_to_the_otp_service_host_when_nothing_is_configured()
    {
        var (sp, _) = Build(Respond(HttpStatusCode.OK));

        Assert.Equal("http://otp-service:5254/", OtpHttpClient(sp).BaseAddress!.ToString());
    }

    [Fact]
    public void Discovery_endpoint_wins_over_OTP_BASE_URL_and_uses_the_configured_scheme_and_names()
    {
        var discovery = new FakeDiscovery(new ServiceEndpoint { Address = "10.1.2.3", Port = 5254 });
        var (sp, _) = Build(Respond(HttpStatusCode.OK), new()
        {
            ["OTP_BASE_URL"] = "http://fallback:1",
            ["ServiceDiscovery:Services:Otp:ServiceNames"] = "otp-a, otp-b",
            ["ServiceDiscovery:Services:Otp:Scheme"] = "https"
        }, discovery);

        Assert.Equal("https://10.1.2.3:5254/", OtpHttpClient(sp).BaseAddress!.ToString());
        Assert.Equal("otp-a", discovery.Asked[0]);
    }

    [Fact]
    public void Discovery_miss_or_failure_falls_back_to_OTP_BASE_URL()
    {
        var (miss, _) = Build(Respond(HttpStatusCode.OK), new() { ["OTP_BASE_URL"] = "http://fallback:1" }, new FakeDiscovery(null));
        var (down, _) = Build(Respond(HttpStatusCode.OK), new() { ["OTP_BASE_URL"] = "http://fallback:2" }, new FakeDiscovery(null, throws: true));

        Assert.Equal("http://fallback:1/", OtpHttpClient(miss).BaseAddress!.ToString());
        Assert.Equal("http://fallback:2/", OtpHttpClient(down).BaseAddress!.ToString());
    }

    [Theory]
    [InlineData(null, null, 8)]
    [InlineData("5", null, 5)]
    [InlineData(null, "12", 12)]
    [InlineData("5", "12", 5)]
    [InlineData("0", null, 8)]
    [InlineData("31", null, 8)]
    [InlineData("abc", null, 8)]
    public void Timeout_is_bounded_to_1_to_30_seconds_with_8_as_the_default(string? env, string? section, int expectedSeconds)
    {
        var settings = new Dictionary<string, string?>();
        if (env is not null) settings["OTP_HTTP_TIMEOUT_SECONDS"] = env;
        if (section is not null) settings["IdentityOtp:TimeoutSeconds"] = section;
        var (sp, _) = Build(Respond(HttpStatusCode.OK), settings);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), OtpHttpClient(sp).Timeout);
    }
}

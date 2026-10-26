using System.Net;
using {{App}}.Api.Tests.Support;

namespace {{App}}.Api.Tests;

public class HealthAndErrorTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_routes_are_not_served()
    {
        // 404 when the host has no fallback authorization, 401 when it is secure by default.
        var status = (await factory.CreateClient().GetAsync("/api/does-not-exist")).StatusCode;

        Assert.Contains(status, new[] { HttpStatusCode.NotFound, HttpStatusCode.Unauthorized });
    }
}

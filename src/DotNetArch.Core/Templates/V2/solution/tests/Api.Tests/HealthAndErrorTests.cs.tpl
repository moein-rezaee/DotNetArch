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
    public async Task Unknown_routes_return_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/does-not-exist")).StatusCode);
}

using System.Net;
using System.Net.Http.Json;
using {{App}}.Api.Tests.Support;
using {{App}}.Application.Features.{{Plural}}.Dtos;

namespace {{App}}.Api.Tests.Features;

public class {{Plural}}ApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public {{Plural}}ApiTests(ApiFactory factory)
    {
        factory.EnsureDatabase();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Crud_flow_works_end_to_end()
    {
        var created = await _client.PostAsJsonAsync("/api/{{RouteName}}", new Create{{Entity}}Request("Widget"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = (await created.Content.ReadFromJsonAsync<{{Entity}}Dto>())!;

        var fetched = await _client.GetFromJsonAsync<{{Entity}}Dto>($"/api/{{RouteName}}/{dto.Id}");
        Assert.Equal("Widget", fetched!.Name);

        var updated = await _client.PutAsJsonAsync($"/api/{{RouteName}}/{dto.Id}", new Update{{Entity}}Request("Gadget"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/{{RouteName}}/{dto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/{{RouteName}}/{dto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_a_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/{{RouteName}}", new Create{{Entity}}Request(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Validation failed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_entities_return_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/{{RouteName}}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Listing_is_paged()
    {
        await _client.PostAsJsonAsync("/api/{{RouteName}}", new Create{{Entity}}Request("Listed"));

        var response = await _client.GetAsync("/api/{{RouteName}}?pageNumber=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("totalCount", await response.Content.ReadAsStringAsync());
    }
}

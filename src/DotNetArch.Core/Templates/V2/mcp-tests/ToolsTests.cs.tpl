using {{App}}.Application;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Mcp.Tests.Support;
using {{App}}.Mcp.Tools.{{Plural}};
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace {{App}}.Mcp.Tests.Tools;

/// <summary>The tools run the same use cases as the API: these tests drive them through the real MediatR pipeline with an in-memory unit of work.</summary>
public class {{Plural}}ToolsTests
{
    private static {{Plural}}Tools CreateTools()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<IUnitOfWork>(new FakeUnitOfWork());
        return new {{Plural}}Tools(services.BuildServiceProvider().GetRequiredService<ISender>());
    }

    [Fact]
    public async Task Create_get_list_update_and_delete_follow_the_same_rules_as_the_api()
    {
        var tools = CreateTools();

        var created = await tools.Create("Widget");
        Assert.Equal("Widget", (await tools.Get(created.Id)).Name);
        Assert.Single((await tools.List()).Items);

        Assert.Equal("Gadget", (await tools.Update(created.Id, "Gadget")).Name);
        Assert.Equal("Deleted.", await tools.Delete(created.Id));
        await Assert.ThrowsAsync<McpException>(() => tools.Get(created.Id));
    }

    [Fact]
    public async Task Validation_failures_become_readable_mcp_errors()
    {
        var error = await Assert.ThrowsAsync<McpException>(() => CreateTools().Create(string.Empty));

        Assert.Contains("Validation failed", error.Message);
    }
}

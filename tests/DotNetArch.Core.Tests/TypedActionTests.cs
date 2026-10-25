using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>Typed use cases: a request with its own properties and its own result, not tied to an id (R-J2).</summary>
public sealed class TypedActionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-typed-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public TypedActionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IDisposable Host() => ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

    private string P(string relative) => Path.Combine(_root, "Acme", relative);

    private SolutionConfig Solution(bool mcp)
    {
        using var _ = Host();
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite",
            Ops: new OpsOptions(Ci: "none", NoGit: true, NoDocker: true, Mcp: mcp)));
        var config = ConfigManager.Load(P(""))!;
        CrudScaffolder.Generate(config, "Product");
        return config;
    }

    private static void Generate(SolutionConfig config, string input, string? output, bool anonymous = false, string method = "POST", string action = "Activate")
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner()));
        ActionScaffolder.Generate(config, "Product", action, method, crudStyle: false, input, output, anonymous, "Activate a device");
    }

    [Fact]
    public void Input_and_output_become_a_request_a_result_a_stub_handler_a_validator_and_a_controller_action()
    {
        var config = Solution(mcp: false);

        Generate(config, "Code:string,DeviceName:string?", "Token:string,ExpiresAtUtc:DateTime?");

        var folder = "src/Acme.Application/Features/Products/Actions/ActivateProduct";
        var command = File.ReadAllText(P($"{folder}/ActivateProductCommand.cs"));
        Assert.Contains("ActivateProductCommand(string Code, string? DeviceName) : IRequest<ActivateProductResult>", command);
        Assert.Contains("ActivateProductResult(string Token, DateTime? ExpiresAtUtc)", File.ReadAllText(P($"{folder}/ActivateProductResult.cs")));
        Assert.Contains("NotImplementedException", File.ReadAllText(P($"{folder}/ActivateProductCommandHandler.cs")));
        Assert.Contains("RuleFor(command => command.Code).NotEmpty().MaximumLength(512);", File.ReadAllText(P($"{folder}/ActivateProductCommandValidator.cs")));
        var controller = File.ReadAllText(P("src/Acme.Api/Controllers/Products/ProductsController.Activate.cs"));
        Assert.Contains("[HttpPost(\"activate\")]", controller);
        Assert.Contains("[FromBody] ActivateProductCommand request", controller);
        Assert.DoesNotContain("AllowAnonymous]", controller);
        Assert.Equal("POST|protected|Code:string,DeviceName:string?|Token:string,ExpiresAtUtc:DateTime?", ConfigManager.Load(P(""))!.UseCases["Product.Activate"] is { } u ? $"{u.Method}|{(u.Anonymous ? "anonymous" : "protected")}|{u.Input}|{u.Output}" : string.Empty);
    }

    [Fact]
    public void Anonymous_get_with_no_output_and_a_none_output_use_the_right_shapes()
    {
        var config = Solution(mcp: false);

        Generate(config, "Slug:string", "Name:string", anonymous: true, method: "GET", action: "Lookup");
        Generate(config, "Reason:string", "none", action: "Retire");

        var lookup = File.ReadAllText(P("src/Acme.Api/Controllers/Products/ProductsController.Lookup.cs"));
        Assert.Contains("[AllowAnonymous]", lookup);
        Assert.Contains("[FromQuery] LookupProductQuery request", lookup);
        Assert.True(File.Exists(P("src/Acme.Application/Features/Products/Actions/LookupProduct/LookupProductQueryHandler.cs")));
        var retire = File.ReadAllText(P("src/Acme.Api/Controllers/Products/ProductsController.Retire.cs"));
        Assert.Contains("return NoContent();", retire);
        Assert.Contains("RetireProductCommand(string Reason) : IRequest;", File.ReadAllText(P("src/Acme.Application/Features/Products/Actions/RetireProduct/RetireProductCommand.cs")));
    }

    [Fact]
    public void An_mcp_host_gets_a_tool_with_the_same_arguments_and_later_hosts_regenerate_it_from_the_recorded_use_case()
    {
        var config = Solution(mcp: true);

        Generate(config, "Code:string,DeviceName:string?", "Token:string");

        var tool = File.ReadAllText(P("src/Acme.Mcp/Tools/Products/ProductsTools.Activate.cs"));
        Assert.Contains("string code, [Description(\"The device name.\")] string? deviceName = default", tool.Replace("[Description(\"The code.\")] ", string.Empty));
        Assert.Contains("new ActivateProductCommand(Code: code, DeviceName: deviceName)", tool);
        Assert.Contains("Name = \"product_activate\"", tool);

        File.Delete(P("src/Acme.Mcp/Tools/Products/ProductsTools.Activate.cs"));
        McpV2Generator.AddTypedActionTool(ConfigManager.Load(P(""))!, ConfigManager.Load(P(""))!.UseCases["Product.Activate"]);
        Assert.Equal(tool, File.ReadAllText(P("src/Acme.Mcp/Tools/Products/ProductsTools.Activate.cs")));
    }

    [Theory]
    [InlineData("code", "Name:Type")]
    [InlineData("Code:Dictionary<int,string>", "none")]
    [InlineData("Code:string,Code:string", "none")]
    [InlineData("Code:System.IO.File", "none")]
    public void Unsupported_properties_are_rejected_before_anything_is_written(string input, string output)
    {
        Assert.Throws<ArgumentException>(() => UseCaseSpec.Parse(input, output));
    }

    [Fact]
    public void Generic_types_keep_their_commas_when_the_property_list_is_split()
    {
        var spec = UseCaseSpec.Parse("Settings:Dictionary<string, Dictionary<string, string>>,Names:List<string>", "Rows:string[]");

        Assert.Equal(new[] { "Settings", "Names" }, spec.Input.Select(p => p.Name));
        Assert.Equal("Dictionary<string,Dictionary<string,string>>", spec.Input[0].Type);
        Assert.Equal("string[]", spec.Output.Properties[0].Type);
    }
}

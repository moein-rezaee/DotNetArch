using System;
using System.IO;
using System.Linq;
using DotNetArch.Scaffolding;

static class ServiceScaffolder
{
    public static void Generate(SolutionConfig config)
    {
        var type = Program.AskOption("Select service type", new[] { "Custom", "Cache", "Message Broker" });
        var solution = config.SolutionName;
        var layer = "Application";
        string? provider = null;
        string serviceName = string.Empty;
        string iface = string.Empty;
        string? entityName = null;
        string ns;
        string servicesRoot;

        if (type == "Custom")
        {
            var isLogic = Program.AskYesNo("Is this a logic (internal) service?", true);
            layer = isLogic ? "Application" : "Infrastructure";
            serviceName = Program.Ask("Enter service name");
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                Program.Error("Service name is required.");
                return;
            }

            if (isLogic)
            {
                entityName = Program.Ask("Enter entity name (optional)");
                var root = Path.Combine(config.SolutionPath, $"{solution}.{layer}");
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    var commonRoot = Path.Combine(root, "Common");
                    var interfaceRoot = Path.Combine(commonRoot, "Interfaces");
                    var implRoot = Path.Combine(commonRoot, "Services");
                    var modelRoot = Path.Combine(commonRoot, "Models");
                    Directory.CreateDirectory(interfaceRoot);
                    Directory.CreateDirectory(implRoot);
                    Directory.CreateDirectory(modelRoot);
                    EnsurePageResult(modelRoot, solution, layer);
                    servicesRoot = implRoot;
                    ns = $"{solution}.{layer}.Common";
                    iface = $"I{serviceName}";
                    WriteInterface(interfaceRoot, ns + ".Interfaces", iface);
                    WriteClass(implRoot, ns + ".Services", serviceName, iface);
                }
                else
                {
                    servicesRoot = Path.Combine(root, entityName, "Services");
                    Directory.CreateDirectory(servicesRoot);
                    ns = $"{solution}.{layer}.{entityName}.Services";
                    iface = $"I{serviceName}";
                    WriteInterface(servicesRoot, ns, iface);
                    WriteClass(servicesRoot, ns, serviceName, iface);
                }
            }
            else
            {
                var root = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure");
                servicesRoot = Path.Combine(root, "Services", serviceName);
                Directory.CreateDirectory(servicesRoot);
                ns = $"{solution}.{layer}.Services.{serviceName}";
                iface = $"I{serviceName}";
                WriteInterface(servicesRoot, ns, iface);
                WriteClass(servicesRoot, ns, serviceName, iface);
            }
        }
        else if (type == "Cache")
        {
            provider = Program.AskOption("Select cache provider", new[] { "Redis" });
            layer = "Infrastructure";
            var root = Path.Combine(config.SolutionPath, $"{solution}.{layer}");
            servicesRoot = Path.Combine(root, "Services", provider);
            Directory.CreateDirectory(servicesRoot);
            if (provider == "Redis")
            {
                serviceName = "RedisService";
                iface = "IRedisService";
                ns = $"{solution}.{layer}.Services.Redis";
                WriteRedisWrapper(servicesRoot, ns, serviceName, iface);
                InstallPackage(config, "StackExchange.Redis");
            }
        }
        else // Message Broker
        {
            provider = Program.AskOption("Select message broker", new[] { "RabbitMQ" });
            layer = "Infrastructure";
            var root = Path.Combine(config.SolutionPath, $"{solution}.{layer}");
            servicesRoot = Path.Combine(root, "Services", provider);
            Directory.CreateDirectory(servicesRoot);
            serviceName = "RabbitMqService";
            iface = "IRabbitMqService";
            ns = $"{solution}.{layer}.Services.RabbitMq";
            WriteRabbitWrapper(servicesRoot, ns, serviceName, iface);
            InstallPackage(config, "RabbitMQ.Client");
        }

        var lifetime = Program.AskOption("Select service lifetime", new[] { "Scoped", "Transient", "Singleton" });
        var regMethod = lifetime switch
        {
            "Singleton" => "AddSingleton",
            "Transient" => "AddTransient",
            _ => "AddScoped"
        };

        var programPath = Path.Combine(config.SolutionPath, $"{config.StartupProject}/Program.cs");
        if (File.Exists(programPath))
        {
            var lines = File.ReadAllLines(programPath).ToList();
            var idx = lines.FindIndex(l => l.Contains("var builder"));
            if (idx >= 0)
            {
                var registration = $"builder.Services.{regMethod}<{iface}, {serviceName}>();";
                if (!lines.Any(l => l.Contains(registration)))
                {
                    lines.Insert(idx + 1, registration);
                    File.WriteAllLines(programPath, lines);
                }
            }
        }

        Program.Success($"{serviceName} service generated.");
    }

    static void WriteInterface(string path, string ns, string iface)
    {
        File.WriteAllText(Path.Combine(path, $"{iface}.cs"), $"namespace {ns};{Environment.NewLine}{Environment.NewLine}public interface {iface} {{ }}{Environment.NewLine}");
    }

    static void WriteClass(string path, string ns, string cls, string iface)
    {
        File.WriteAllText(Path.Combine(path, $"{cls}.cs"), $"namespace {ns};{Environment.NewLine}{Environment.NewLine}public class {cls} : {iface} {{ }}{Environment.NewLine}");
    }

    static void EnsurePageResult(string modelRoot, string solution, string layer)
    {
        var prPath = Path.Combine(modelRoot, "PageResult.cs");
        if (!File.Exists(prPath))
        {
            File.WriteAllText(prPath, $"using System.Collections.Generic;{Environment.NewLine}{Environment.NewLine}namespace {solution}.{layer}.Common.Models;{Environment.NewLine}{Environment.NewLine}public class PageResult<T>{Environment.NewLine}{{{Environment.NewLine}    public IReadOnlyCollection<T> Items {{ get; init; }} = Array.Empty<T>();{Environment.NewLine}    public int TotalCount {{ get; init; }}{Environment.NewLine}}}{Environment.NewLine}");
        }
    }

    static void WriteRedisWrapper(string root, string ns, string cls, string iface)
    {
        var ifacePath = Path.Combine(root, $"{iface}.cs");
        var clsPath = Path.Combine(root, $"{cls}.cs");
        var ifaceLines = new[]
        {
            "using System;",
            "using System.Threading.Tasks;",
            "",
            $"namespace {ns};",
            "",
            $"public interface {iface}",
            "{",
            "    Task<string?> GetAsync(string key);",
            "    Task SetAsync(string key, string value, TimeSpan? expiry = null);",
            "}",
            ""
        };
        File.WriteAllLines(ifacePath, ifaceLines);

        var clsLines = new[]
        {
            "using System;",
            "using System.Threading.Tasks;",
            "using Microsoft.Extensions.Configuration;",
            "using StackExchange.Redis;",
            "",
            $"namespace {ns};",
            "",
            $"public class {cls} : {iface}",
            "{",
            "    private readonly IDatabase _db;",
            $"    public {cls}(IConfiguration configuration)",
            "    {",
            "        var mux = ConnectionMultiplexer.Connect(configuration.GetConnectionString(\"Redis\"));",
            "        _db = mux.GetDatabase();",
            "    }",
            "    public Task<string?> GetAsync(string key) => _db.StringGetAsync(key).ContinueWith(t => (string?)t.Result);",
            "    public Task SetAsync(string key, string value, TimeSpan? expiry = null) => _db.StringSetAsync(key, value, expiry);",
            "}",
            ""
        };
        File.WriteAllLines(clsPath, clsLines);
    }

    static void WriteRabbitWrapper(string root, string ns, string cls, string iface)
    {
        var ifacePath = Path.Combine(root, $"{iface}.cs");
        var clsPath = Path.Combine(root, $"{cls}.cs");
        var ifaceLines = new[]
        {
            "using System;",
            "",
            $"namespace {ns};",
            "",
            $"public interface {iface}",
            "{",
            "    void Publish(string exchange, string routingKey, byte[] body);",
            "}",
            ""
        };
        File.WriteAllLines(ifacePath, ifaceLines);

        var clsLines = new[]
        {
            "using System;",
            "using Microsoft.Extensions.Configuration;",
            "using RabbitMQ.Client;",
            "",
            $"namespace {ns};",
            "",
            $"public class {cls} : {iface}, IDisposable",
            "{",
            "    private readonly IConnection _connection;",
            "    private readonly IModel _channel;",
            $"    public {cls}(IConfiguration configuration)",
            "    {",
            "        var factory = new ConnectionFactory",
            "        {",
            "            Uri = new Uri(configuration.GetConnectionString(\"RabbitMq\"))",
            "        };",
            "        _connection = factory.CreateConnection();",
            "        _channel = _connection.CreateModel();",
            "    }",
            "    public void Publish(string exchange, string routingKey, byte[] body)",
            "    {",
            "        _channel.BasicPublish(exchange, routingKey, null, body);",
            "    }",
            "    public void Dispose()",
            "    {",
            "        _channel.Dispose();",
            "        _connection.Dispose();",
            "    }",
            "}",
            ""
        };
        File.WriteAllLines(clsPath, clsLines);
    }

    static void InstallPackage(SolutionConfig config, string package)
    {
        var infraProj = Path.Combine(config.SolutionPath, $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj");
        if (File.Exists(infraProj))
            Program.RunCommand($"dotnet add {infraProj} package {package}", config.SolutionPath, print: false);
    }
}

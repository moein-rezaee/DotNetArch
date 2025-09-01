using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetArch.Scaffolding;
using Microsoft.Extensions.Configuration;

static class ServiceScaffolder
{
    public static void Generate(SolutionConfig config)
    {
        var type = Program.AskOption("Select service type", new[] { "Custom", "Cache", "Message Broker" });
        var solution = config.SolutionName;

        if (type == "Custom")
            HandleCustom(config, solution);
        else if (type == "Cache")
            HandleCache(config, solution);
        else
            HandleMessageBroker(config, solution);
    }

    static void HandleCustom(SolutionConfig config, string solution)
    {
        var entityName = Program.Ask("Enter entity name (optional)");
        string serviceName;
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            serviceName = Program.Ask("Enter service name (optional)");
            if (string.IsNullOrWhiteSpace(serviceName))
                serviceName = entityName + "Service";
            serviceName = NormalizeServiceName(serviceName);

            var appRoot = Path.Combine(config.SolutionPath, $"{solution}.Application");
            var serviceDir = Path.Combine(appRoot, "Features", entityName, "Services", serviceName);
            Directory.CreateDirectory(serviceDir);
            var iface = "I" + serviceName;
            var ns = $"{solution}.Application.Features.{entityName}.Services.{serviceName}";
            WriteInterface(serviceDir, ns, iface);
            WriteClass(serviceDir, ns, serviceName, iface);

            var reg = AskLifetime();
            AddServiceToDi(config, "Application", $"{iface}, {serviceName}", reg);
            EnsureProgramCalls(config, "Application");
            Program.Success($"{serviceName} service generated.");
            return;
        }

        serviceName = Program.Ask("Enter service name");
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Program.Error("Service name is required.");
            return;
        }
        serviceName = NormalizeServiceName(serviceName);
        var isExternal = Program.AskYesNo("Is this an external service?", false);
        if (isExternal)
        {
            var iface = "I" + serviceName;
            var appRoot = Path.Combine(config.SolutionPath, $"{solution}.Application");
            var ifaceDir = Path.Combine(appRoot, "Common", "Interfaces");
            Directory.CreateDirectory(ifaceDir);
            WriteInterface(ifaceDir, $"{solution}.Application.Common.Interfaces", iface);

            var infraRoot = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure");
            var implDir = Path.Combine(infraRoot, "Services", serviceName);
            Directory.CreateDirectory(implDir);
            var ns = $"{solution}.Infrastructure.Services.{serviceName}";
            WriteClass(implDir, ns, serviceName, iface);

            var reg = AskLifetime();
            AddServiceToDi(config, "Infrastructure", $"{iface}, {serviceName}", reg);
            EnsureProgramCalls(config, "Infrastructure");
        }
        else
        {
            var iface = "I" + serviceName;
            var appRoot = Path.Combine(config.SolutionPath, $"{solution}.Application");
            var ifaceDir = Path.Combine(appRoot, "Common", "Interfaces");
            var implDir = Path.Combine(appRoot, "Common", "Services", serviceName);
            Directory.CreateDirectory(ifaceDir);
            Directory.CreateDirectory(implDir);
            WriteInterface(ifaceDir, $"{solution}.Application.Common.Interfaces", iface);
            WriteClass(implDir, $"{solution}.Application.Common.Services.{serviceName}", serviceName, iface);

            var reg = AskLifetime();
            AddServiceToDi(config, "Application", $"{iface}, {serviceName}", reg);
            EnsureProgramCalls(config, "Application");
        }
        Program.Success($"{serviceName} service generated.");
    }

    static void HandleCache(SolutionConfig config, string solution)
    {
        Program.AskOption("Select cache provider", new[] { "Redis" });
        var iface = "IRedisService";
        var cls = "RedisService";
        var appIfaceDir = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces");
        Directory.CreateDirectory(appIfaceDir);
        WriteRedisInterface(appIfaceDir, $"{solution}.Application.Common.Interfaces", iface);
        var infraDir = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Services", "Redis");
        Directory.CreateDirectory(infraDir);
        WriteRedisClass(infraDir, $"{solution}.Infrastructure.Services.Redis", cls, iface, $"{solution}.Application.Common.Interfaces");
        InstallPackage(config, "StackExchange.Redis");
        AddServiceToDi(config, "Infrastructure", $"{iface}, {cls}", "AddSingleton");
        EnsureProgramCalls(config, "Infrastructure");
        EnsureDotEnv(config);
        EnsureEnvFiles(config, "Redis");
        Program.Success("Redis service generated.");
    }

    static void HandleMessageBroker(SolutionConfig config, string solution)
    {
        Program.AskOption("Select message broker", new[] { "RabbitMQ" });
        var iface = "IRabbitMqService";
        var cls = "RabbitMqService";
        var appIfaceDir = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces");
        Directory.CreateDirectory(appIfaceDir);
        WriteRabbitInterface(appIfaceDir, $"{solution}.Application.Common.Interfaces", iface);
        var infraDir = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Services", "RabbitMq");
        Directory.CreateDirectory(infraDir);
        WriteRabbitClass(infraDir, $"{solution}.Infrastructure.Services.RabbitMq", cls, iface, $"{solution}.Application.Common.Interfaces");
        InstallPackage(config, "RabbitMQ.Client");
        AddServiceToDi(config, "Infrastructure", $"{iface}, {cls}", "AddSingleton");
        EnsureProgramCalls(config, "Infrastructure");
        EnsureDotEnv(config);
        EnsureEnvFiles(config, "RabbitMq");
        Program.Success("RabbitMQ service generated.");
    }

    static string NormalizeServiceName(string name)
    {
        var pascal = Regex.Split(name, @"[^a-zA-Z0-9]+").Where(s => s.Length > 0)
            .Select(s => char.ToUpper(s[0]) + s.Substring(1).ToLower());
        var result = string.Concat(pascal);
        if (!result.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
            result += "Service";
        return result;
    }

    static string AskLifetime()
    {
        var lifetime = Program.AskOption("Select service lifetime", new[] { "Scoped", "Transient", "Singleton" });
        return lifetime switch
        {
            "Singleton" => "AddSingleton",
            "Transient" => "AddTransient",
            _ => "AddScoped"
        };
    }

    static void WriteInterface(string dir, string ns, string iface)
    {
        File.WriteAllText(Path.Combine(dir, $"{iface}.cs"),
            $"namespace {ns};{Environment.NewLine}{Environment.NewLine}public interface {iface} {{ }}{Environment.NewLine}");
    }

    static void WriteClass(string dir, string ns, string cls, string iface)
    {
        File.WriteAllText(Path.Combine(dir, $"{cls}.cs"),
            $"namespace {ns};{Environment.NewLine}{Environment.NewLine}public class {cls} : {iface} {{ }}{Environment.NewLine}");
    }

    static void WriteRedisInterface(string dir, string ns, string iface)
    {
        var lines = new[]
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
            "",
        };
        File.WriteAllLines(Path.Combine(dir, $"{iface}.cs"), lines);
    }

    static void WriteRedisClass(string dir, string ns, string cls, string iface, string ifaceNs)
    {
        var lines = new[]
        {
            "using System;",
            "using System.Threading.Tasks;",
            "using Microsoft.Extensions.Configuration;",
            "using StackExchange.Redis;",
            $"using {ifaceNs};",
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
            "",
        };
        File.WriteAllLines(Path.Combine(dir, $"{cls}.cs"), lines);
    }

    static void WriteRabbitInterface(string dir, string ns, string iface)
    {
        var lines = new[]
        {
            "using System;",
            "",
            $"namespace {ns};",
            "",
            $"public interface {iface}",
            "{",
            "    void Publish(string exchange, string routingKey, byte[] body);",
            "}",
            "",
        };
        File.WriteAllLines(Path.Combine(dir, $"{iface}.cs"), lines);
    }

    static void WriteRabbitClass(string dir, string ns, string cls, string iface, string ifaceNs)
    {
        var lines = new[]
        {
            "using System;",
            "using Microsoft.Extensions.Configuration;",
            "using RabbitMQ.Client;",
            $"using {ifaceNs};",
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
            "",
        };
        File.WriteAllLines(Path.Combine(dir, $"{cls}.cs"), lines);
    }

    static void AddServiceToDi(SolutionConfig config, string layer, string types, string method)
    {
        var root = Path.Combine(config.SolutionPath, $"{config.SolutionName}.{layer}");
        var diPath = Path.Combine(root, "DependencyInjection.cs");
        if (!File.Exists(diPath))
        {
            Directory.CreateDirectory(root);
            var header = layer == "Application"
                ? new[]
                {
                    "using Microsoft.Extensions.DependencyInjection;",
                    "",
                    $"namespace {config.SolutionName}.Application;",
                    "",
                    "public static class DependencyInjection",
                    "{",
                    "    public static IServiceCollection AddApplication(this IServiceCollection services)",
                    "    {",
                    "        return services;",
                    "    }",
                    "}"
                }
                : new[]
                {
                    "using Microsoft.Extensions.DependencyInjection;",
                    "using Microsoft.Extensions.Configuration;",
                    "",
                    $"namespace {config.SolutionName}.Infrastructure;",
                    "",
                    "public static class DependencyInjection",
                    "{",
                    "    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)",
                    "    {",
                    "        return services;",
                    "    }",
                    "}"
                };
            File.WriteAllLines(diPath, header);
        }
        var lines = File.ReadAllLines(diPath).ToList();
        var insertIdx = lines.FindLastIndex(l => l.Contains("return services;"));
        var registration = $"        services.{method}<{types}>();";
        if (insertIdx >= 0 && !lines.Any(l => l.Contains(registration)))
        {
            lines.Insert(insertIdx, registration);
            File.WriteAllLines(diPath, lines);
        }
    }

    static void EnsureProgramCalls(SolutionConfig config, string layer)
    {
        var programPath = Path.Combine(config.SolutionPath, $"{config.StartupProject}/Program.cs");
        if (!File.Exists(programPath))
            return;
        var lines = File.ReadAllLines(programPath).ToList();
        var usingLine = $"using {config.SolutionName}.{layer};";
        if (!lines.Any(l => l.Trim() == usingLine))
        {
            var uIdx = lines.FindLastIndex(l => l.StartsWith("using "));
            lines.Insert(uIdx + 1, usingLine);
        }

        string call = layer == "Application" ? "builder.Services.AddApplication();" : "builder.Services.AddInfrastructure(builder.Configuration);";
        if (!lines.Any(l => l.Contains(call)))
        {
            var idx = lines.FindIndex(l => l.Contains("var builder"));
            if (idx >= 0)
                lines.Insert(idx + 1, call);
        }

        File.WriteAllLines(programPath, lines);
    }

    static void EnsureDotEnv(SolutionConfig config)
    {
        var proj = Path.Combine(config.SolutionPath, $"{config.StartupProject}/{config.StartupProject}.csproj");
        if (File.Exists(proj))
            Program.RunCommand($"dotnet add {proj} package DotNetEnv", config.SolutionPath, print: false);

        var programPath = Path.Combine(config.SolutionPath, $"{config.StartupProject}/Program.cs");
        if (!File.Exists(programPath)) return;
        var lines = File.ReadAllLines(programPath).ToList();
        var loadLine = "DotNetEnv.Env.TraversePath().Load();";
        if (!lines.Any(l => l.Contains(loadLine)))
        {
            var idx = lines.FindIndex(l => l.Contains("var builder"));
            if (idx >= 0)
                lines.Insert(idx, loadLine);
        }
        File.WriteAllLines(programPath, lines);
    }

    static void EnsureEnvFiles(SolutionConfig config, string provider)
    {
        foreach (var env in new[] { "development", "test", "production" })
        {
            var path = Path.Combine(config.SolutionPath, $".env.{env}");
            var user = env switch { "production" => "produser", "test" => "testuser", _ => "devuser" };
            var pass = env switch { "production" => "prodpass", "test" => "testpass", _ => "devpass" };
            if (!File.Exists(path))
                File.WriteAllLines(path, Array.Empty<string>());
            var lines = File.ReadAllLines(path).ToList();
            if (provider == "Redis")
            {
                EnsureEnvVar(lines, "REDIS_USER", user);
                EnsureEnvVar(lines, "REDIS_PASSWORD", pass);
            }
            else
            {
                EnsureEnvVar(lines, "RABBITMQ_USER", user);
                EnsureEnvVar(lines, "RABBITMQ_PASSWORD", pass);
            }
            File.WriteAllLines(path, lines);
        }
    }

    static void EnsureEnvVar(System.Collections.Generic.List<string> lines, string key, string value)
    {
        if (!lines.Any(l => l.StartsWith(key + "=")))
            lines.Add($"{key}={value}");
    }

    static void InstallPackage(SolutionConfig config, string package)
    {
        var infraProj = Path.Combine(config.SolutionPath, $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj");
        if (File.Exists(infraProj))
            Program.RunCommand($"dotnet add {infraProj} package {package}", config.SolutionPath, print: false);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetArch.Scaffolding;

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
            var entityFolder = GetEntityFolder(appRoot, entityName);
            var ifaceDir = Path.Combine(appRoot, "Features", entityFolder, "Interfaces");
            var implDir = Path.Combine(appRoot, "Features", entityFolder, "Services");
            Directory.CreateDirectory(ifaceDir);
            Directory.CreateDirectory(implDir);
            var iface = "I" + serviceName;
            var ifaceNs = $"{solution}.Application.Features.{entityFolder}.Interfaces";
            var classNs = $"{solution}.Application.Features.{entityFolder}.Services";
            WriteInterface(ifaceDir, ifaceNs, iface);
            WriteClass(implDir, classNs, serviceName, iface);

            var reg = AskLifetime();
            var ifaceFqn = $"{ifaceNs}.{iface}";
            var classFqn = $"{classNs}.{serviceName}";
            AddServiceToDi(config, "Application", $"{ifaceFqn}, {classFqn}", reg);
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
            var ifaceNs = $"{solution}.Application.Common.Interfaces";
            WriteInterface(ifaceDir, ifaceNs, iface);

            var infraRoot = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure");
            var implDir = Path.Combine(infraRoot, "Services", serviceName);
            Directory.CreateDirectory(implDir);
            var classNs = $"{solution}.Infrastructure.Services.{serviceName}";
            WriteClass(implDir, classNs, serviceName, iface);

            var reg = AskLifetime();
            var ifaceFqn = $"{ifaceNs}.{iface}";
            var classFqn = $"{classNs}.{serviceName}";
            AddServiceToDi(config, "Infrastructure", $"{ifaceFqn}, {classFqn}", reg);
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
            var ifaceNs = $"{solution}.Application.Common.Interfaces";
            var classNs = $"{solution}.Application.Common.Services.{serviceName}";
            WriteInterface(ifaceDir, ifaceNs, iface);
            WriteClass(implDir, classNs, serviceName, iface);

            var reg = AskLifetime();
            var ifaceFqn = $"{ifaceNs}.{iface}";
            var classFqn = $"{classNs}.{serviceName}";
            AddServiceToDi(config, "Application", $"{ifaceFqn}, {classFqn}", reg);
            EnsureProgramCalls(config, "Application");
        }
        Program.Success($"{serviceName} service generated.");
    }

    static void HandleCache(SolutionConfig config, string solution)
    {
        Program.AskOption("Select cache provider", new[] { "Redis" });
        var iface = "IRedisService";
        var cls = "RedisService";
        var ifaceNs = $"{solution}.Application.Common.Interfaces";
        var appIfaceDir = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces");
        Directory.CreateDirectory(appIfaceDir);
        WriteRedisInterface(appIfaceDir, ifaceNs, iface);
        var infraDir = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Services", "Redis");
        Directory.CreateDirectory(infraDir);
        var classNs = $"{solution}.Infrastructure.Services.Redis";
        WriteRedisClass(infraDir, classNs, cls, iface, ifaceNs);
        InstallPackage(config, "StackExchange.Redis");
        var ifaceFqn = $"{ifaceNs}.{iface}";
        var classFqn = $"{classNs}.{cls}";
        AddServiceToDi(config, "Infrastructure", $"{ifaceFqn}, {classFqn}", "AddSingleton");
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
        var ifaceNs = $"{solution}.Application.Common.Interfaces";
        var appIfaceDir = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces");
        Directory.CreateDirectory(appIfaceDir);
        WriteRabbitInterface(appIfaceDir, ifaceNs, iface);
        var infraDir = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Services", "RabbitMq");
        Directory.CreateDirectory(infraDir);
        var classNs = $"{solution}.Infrastructure.Services.RabbitMq";
        WriteRabbitClass(infraDir, classNs, cls, iface, ifaceNs);
        InstallPackage(config, "RabbitMQ.Client");
        var ifaceFqn = $"{ifaceNs}.{iface}";
        var classFqn = $"{classNs}.{cls}";
        AddServiceToDi(config, "Infrastructure", $"{ifaceFqn}, {classFqn}", "AddSingleton");
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

    static string GetEntityFolder(string appRoot, string entityName)
    {
        var features = Path.Combine(appRoot, "Features");
        if (Directory.Exists(features))
        {
            var dirs = Directory.GetDirectories(features).Select(Path.GetFileName);
            var plural = Pluralize(entityName);
            foreach (var d in dirs)
            {
                if (string.Equals(d, entityName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d, plural, StringComparison.OrdinalIgnoreCase))
                    return d!;
            }
        }
        return Pluralize(entityName);
    }

    static string Pluralize(string word)
    {
        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) && word.Length > 1 && !IsVowel(word[^2]))
            return word.Substring(0, word.Length - 1) + "ies";
        if (Regex.IsMatch(word, "(s|x|z|ch|sh)$", RegexOptions.IgnoreCase))
            return word + "es";
        return word + "s";
    }

    static bool IsVowel(char c) => "aeiouAEIOU".IndexOf(c) >= 0;

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
            "    Task<string?> GetAsync(string key, bool decrypt = false);",
            "    Task SetAsync(string key, string value, TimeSpan? expiry = null, bool encrypt = false);",
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
            "using System.Text;",
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
            "    public async Task<string?> GetAsync(string key, bool decrypt = false)",
            "    {",
            "        var value = await _db.StringGetAsync(key);",
            "        if (value.IsNull) return null;",
            "        var str = value.ToString();",
            "        return decrypt ? Decrypt(str) : str;",
            "    }",
            "    public Task SetAsync(string key, string value, TimeSpan? expiry = null, bool encrypt = false)",
            "    {",
            "        var stored = encrypt ? Encrypt(value) : value;",
            "        return _db.StringSetAsync(key, stored, expiry);",
            "    }",
            "    private static string Encrypt(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));",
            "    private static string Decrypt(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));",
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
            $"public interface {iface} : IDisposable",
            "{",
            "    void Publish(string exchange, string routingKey, byte[] body);",
            "    void Subscribe(string queue, Action<byte[]> handler);",
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
            "using RabbitMQ.Client.Events;",
            $"using {ifaceNs};",
            "",
            $"namespace {ns};",
            "",
            $"public class {cls} : {iface}",
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
            "    public void Subscribe(string queue, Action<byte[]> handler)",
            "    {",
            "        var consumer = new EventingBasicConsumer(_channel);",
            "        consumer.Received += (_, ea) => handler(ea.Body.ToArray());",
            "        _channel.BasicConsume(queue, true, consumer);",
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
        var usingLine = "using DotNetEnv;";
        var ioUsing = "using System.IO;";
        var uIdx = lines.FindLastIndex(l => l.StartsWith("using "));
        if (!lines.Any(l => l.Trim() == usingLine))
            lines.Insert(uIdx + 1, usingLine);
        if (!lines.Any(l => l.Trim() == ioUsing))
            lines.Insert(uIdx + 2, ioUsing);
        var loadLine = "DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), $\".env.{builder.Environment.EnvironmentName.ToLower()}\"));";
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
            var path = Path.Combine(config.SolutionPath, config.StartupProject, $".env.{env}");
            var user = env switch { "production" => "produser", "test" => "testuser", _ => "devuser" };
            var pass = env switch { "production" => "prodpass", "test" => "testpass", _ => "devpass" };
            if (!File.Exists(path))
                File.WriteAllLines(path, Array.Empty<string>());
            var lines = File.ReadAllLines(path).ToList();
            if (provider == "Redis")
            {
                var conn = $"redis://{user}:{pass}@localhost:6379";
                EnsureEnvVar(lines, "ConnectionStrings__Redis", conn);
            }
            else
            {
                var conn = $"amqp://{user}:{pass}@localhost:5672";
                EnsureEnvVar(lines, "ConnectionStrings__RabbitMq", conn);
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

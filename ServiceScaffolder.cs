using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Xml.Linq;
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
            WriteClass(implDir, classNs, serviceName, iface, ifaceNs);

            var reg = AskLifetime();
            AddServiceToDi(config, "Application", iface, ifaceNs, serviceName, classNs, reg);
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
            WriteClass(implDir, classNs, serviceName, iface, ifaceNs);

            var reg = AskLifetime();
            AddServiceToDi(config, "Infrastructure", iface, ifaceNs, serviceName, classNs, reg);
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
            WriteClass(implDir, classNs, serviceName, iface, ifaceNs);

            var reg = AskLifetime();
            AddServiceToDi(config, "Application", iface, ifaceNs, serviceName, classNs, reg);
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
        InstallPackage(config, "StackExchange.Redis", "2.9.11");
        var extra = string.Join(Environment.NewLine, new[]
        {
            "        services.AddSingleton<IConnectionMultiplexer>(sp =>",
            "        {",
            "            var cfg = sp.GetRequiredService<IConfiguration>();",
            "            var host = cfg[\"Redis:Host\"];",
            "            var port = cfg[\"Redis:Port\"];",
            "            var user = cfg[\"REDIS_USER\"];",
            "            var pass = cfg[\"REDIS_PASSWORD\"];",
            "            return ConnectionMultiplexer.Connect($\"{host}:{port},user={user},password={pass}\");",
            "        });"
        });
        AddServiceToDi(config, "Infrastructure", iface, ifaceNs, cls, classNs, "AddSingleton", extra,
            new[] { "StackExchange.Redis", "Microsoft.Extensions.Configuration", "System" });
        EnsureProgramCalls(config, "Infrastructure");
        EnsureDotEnv(config);
        EnsureEnvFiles(config, "Redis");
        EnsureAppSettingsFiles(config, "Redis");
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
        InstallPackage(config, "RabbitMQ.Client", "7.1.2");
        var extra = string.Join(Environment.NewLine, new[]
        {
            "        services.AddSingleton<IConnection>(sp =>",
            "        {",
            "            var cfg = sp.GetRequiredService<IConfiguration>();",
            "            var host = cfg[\"RabbitMq:Host\"];",
            "            var port = int.Parse(cfg[\"RabbitMq:Port\"] ?? \"5672\");",
            "            var user = cfg[\"RABBITMQ_USER\"];",
            "            var pass = cfg[\"RABBITMQ_PASSWORD\"];",
            "            var factory = new ConnectionFactory { HostName = host, Port = port, UserName = user, Password = pass };",
            "            return factory.CreateConnection();",
            "        });"
        });
        AddServiceToDi(config, "Infrastructure", iface, ifaceNs, cls, classNs, "AddSingleton", extra,
            new[] { "RabbitMQ.Client", "Microsoft.Extensions.Configuration", "System" });
        EnsureProgramCalls(config, "Infrastructure");
        EnsureDotEnv(config);
        EnsureEnvFiles(config, "RabbitMq");
        EnsureAppSettingsFiles(config, "RabbitMq");
        Program.Success("RabbitMQ service generated.");
    }

    static string NormalizeServiceName(string name)
    {
        name = Regex.Replace(name, "Service$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", " $1");
        var parts = Regex.Split(name, @"[^a-zA-Z0-9]+").Where(p => p.Length > 0);
        var pascal = parts.Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower());
        return string.Concat(pascal) + "Service";
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

    static void WriteClass(string dir, string ns, string cls, string iface, string ifaceNs)
    {
        File.WriteAllText(
            Path.Combine(dir, $"{cls}.cs"),
            $"using {ifaceNs};{Environment.NewLine}{Environment.NewLine}namespace {ns};{Environment.NewLine}{Environment.NewLine}public class {cls} : {iface} {{ }}{Environment.NewLine}");
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
            "    Task<string?> GetAsync(string key, bool decode = false);",
            "    Task SetAsync(string key, string value, TimeSpan? expiry = null, bool encode = false);",
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
            "using System.Security.Cryptography;",
            "using Microsoft.Extensions.Configuration;",
            "using StackExchange.Redis;",
            $"using {ifaceNs};",
            "",
            $"namespace {ns};",
            "",
            $"public class {cls} : {iface}",
            "{",
            "    private readonly IDatabase _db;",
            "    private readonly byte[] _key;",
            "    private readonly byte[] _iv;",
            $"    public {cls}(IConnectionMultiplexer mux, IConfiguration cfg)",
            "    {",
            "        _db = mux.GetDatabase();",
            "        var k = cfg[\"REDIS_SECRET_KEY\"];",
            "        var v = cfg[\"REDIS_SECRET_IV\"];",
            "        _key = string.IsNullOrWhiteSpace(k) ? Array.Empty<byte>() : Convert.FromBase64String(k);",
            "        _iv = string.IsNullOrWhiteSpace(v) ? Array.Empty<byte>() : Convert.FromBase64String(v);",
            "    }",
            "    public async Task<string?> GetAsync(string key, bool decode = false)",
            "    {",
            "        var value = await _db.StringGetAsync(key);",
            "        if (value.IsNull) return null;",
            "        var str = value.ToString();",
            "        return decode ? Decode(str) : str;",
            "    }",
            "    public Task SetAsync(string key, string value, TimeSpan? expiry = null, bool encode = false)",
            "    {",
            "        var stored = encode ? Encode(value) : value;",
            "        return _db.StringSetAsync(key, stored, expiry);",
            "    }",
            "    private string Encode(string value)",
            "    {",
            "        if (_key.Length == 0 || _iv.Length == 0) return value;",
            "        using var aes = Aes.Create();",
            "        aes.Key = _key;",
            "        aes.IV = _iv;",
            "        var bytes = Encoding.UTF8.GetBytes(value);",
            "        var enc = aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);",
            "        return Convert.ToBase64String(enc);",
            "    }",
            "    private string Decode(string value)",
            "    {",
            "        if (_key.Length == 0 || _iv.Length == 0) return value;",
            "        using var aes = Aes.Create();",
            "        aes.Key = _key;",
            "        aes.IV = _iv;",
            "        var bytes = Convert.FromBase64String(value);",
            "        var dec = aes.CreateDecryptor().TransformFinalBlock(bytes, 0, bytes.Length);",
            "        return Encoding.UTF8.GetString(dec);",
            "    }",
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
            "using System.Threading.Tasks;",
            "",
            $"namespace {ns};",
            "",
            $"public interface {iface} : IDisposable",
            "{",
            "    Task PublishAsync(string exchange, string routingKey, byte[] body);",
            "    Task SubscribeAsync(string queue, Func<byte[], Task> handler);",
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
            "using System.Threading.Tasks;",
            "using RabbitMQ.Client;",
            "using RabbitMQ.Client.Events;",
            $"using {ifaceNs};",
            "",
            $"namespace {ns};",
            "",
            $"public class {cls} : {iface}",
            "{",
            "    private readonly IConnection _connection;",
            "    private readonly IChannel _channel;",
            $"    public {cls}(IConnection connection)",
            "    {",
            "        _connection = connection;",
            "        _channel = _connection.CreateChannel();",
            "    }",
            "    public async Task PublishAsync(string exchange, string routingKey, byte[] body)",
            "    {",
            "        await _channel.BasicPublishAsync(exchange, routingKey, body);",
            "    }",
            "    public async Task SubscribeAsync(string queue, Func<byte[], Task> handler)",
            "    {",
            "        var consumer = new AsyncEventingBasicConsumer(_channel);",
            "        consumer.Received += async (_, ea) => await handler(ea.Body.ToArray());",
            "        await _channel.BasicConsumeAsync(queue, true, consumer);",
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

    static void AddServiceToDi(SolutionConfig config, string layer,
        string iface, string ifaceNs, string cls, string clsNs,
        string method, string? extra = null, string[]? extraUsings = null)
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
        AddUsing(lines, ifaceNs);
        AddUsing(lines, clsNs);
        if (extraUsings != null)
            foreach (var u in extraUsings)
                AddUsing(lines, u);
        var insertIdx = lines.FindLastIndex(l => l.Contains("return services;"));
        if (insertIdx >= 0)
        {
            if (!string.IsNullOrEmpty(extra) && !lines.Any(l => l.Trim() == extra.Trim()))
            {
                lines.Insert(insertIdx, extra);
                insertIdx++;
            }
            var registration = $"        services.{method}<{iface}, {cls}>();";
            if (!lines.Any(l => l.Contains(registration)))
                lines.Insert(insertIdx, registration);
            File.WriteAllLines(diPath, lines);
        }
    }

    static void AddUsing(System.Collections.Generic.List<string> lines, string ns)
    {
        var usingLine = $"using {ns};";
        if (!lines.Any(l => l.Trim() == usingLine))
        {
            var uIdx = lines.FindLastIndex(l => l.StartsWith("using "));
            lines.Insert(uIdx + 1, usingLine);
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
        var cfgUsing = "using Microsoft.Extensions.Configuration;";
        var sysUsing = "using System;";
        var uIdx = lines.FindLastIndex(l => l.StartsWith("using "));
        if (!lines.Any(l => l.Trim() == usingLine))
            lines.Insert(uIdx + 1, usingLine);
        if (!lines.Any(l => l.Trim() == ioUsing))
            lines.Insert(uIdx + 2, ioUsing);
        if (!lines.Any(l => l.Trim() == cfgUsing))
            lines.Insert(uIdx + 3, cfgUsing);
        if (!lines.Any(l => l.Trim() == sysUsing))
            lines.Insert(uIdx + 4, sysUsing);

        var envVarLine = "var env = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\") ?? \"development\";";
        if (!lines.Any(l => l.Trim() == envVarLine))
        {
            var idx = lines.FindIndex(l => l.Contains("var builder"));
            if (idx >= 0)
                lines.Insert(idx, envVarLine);
        }

        lines.RemoveAll(l => l.Contains("DotNetEnv.Env.Load("));
        var loadLine = "DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), \"config\", \"env\", $\".env.{env.ToLower()}\"));";
        var loadIdx = lines.FindIndex(l => l.Contains("var builder"));
        if (loadIdx >= 0)
            lines.Insert(loadIdx + 1, loadLine);

        lines.RemoveAll(l => l.Contains("AddJsonFile") && l.Contains("appsettings"));
        var addJsonLine = "builder.Configuration.AddJsonFile(Path.Combine(\"config\", \"settings\", $\"appsettings.{env.ToLower()}.json\"), optional: true, reloadOnChange: true);";
        var jsonIdx = lines.FindIndex(l => l.Contains("var builder"));
        if (jsonIdx >= 0)
            lines.Insert(jsonIdx + 3, addJsonLine);

        File.WriteAllLines(programPath, lines);
    }

    static void EnsureEnvFiles(SolutionConfig config, string provider)
    {
        var projectDir = Path.Combine(config.SolutionPath, config.StartupProject);
        var configDir = Path.Combine(projectDir, "config", "env");
        Directory.CreateDirectory(configDir);
        foreach (var env in new[] { "development", "test", "production" })
        {
            var fileName = $".env.{env}";
            var path = Path.Combine(configDir, fileName);

            var oldUpper = Path.Combine(projectDir, $".env.{char.ToUpper(env[0]) + env[1..]}");
            if (File.Exists(oldUpper)) File.Move(oldUpper, path, true);
            var oldLower = Path.Combine(projectDir, fileName);
            if (File.Exists(oldLower)) File.Move(oldLower, path, true);

            if (!File.Exists(path))
                continue;
            var user = env switch { "production" => "produser", "test" => "testuser", _ => "devuser" };
            var pass = env switch { "production" => "prodpass", "test" => "testpass", _ => "devpass" };
            var lines = File.ReadAllLines(path).ToList();
            if (provider == "Redis")
            {
                var keys = GenerateAesKey();
                EnsureEnvVar(lines, "REDIS_USER", user);
                EnsureEnvVar(lines, "REDIS_PASSWORD", pass);
                EnsureEnvVar(lines, "REDIS_SECRET_KEY", keys.key);
                EnsureEnvVar(lines, "REDIS_SECRET_IV", keys.iv);
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

    static (string key, string iv) GenerateAesKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.GenerateKey();
        aes.GenerateIV();
        return (Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV));
    }

    static void InstallPackage(SolutionConfig config, string package, string version)
    {
        var infraProj = Path.Combine(config.SolutionPath, $"{config.SolutionName}.Infrastructure", $"{config.SolutionName}.Infrastructure.csproj");
        if (!File.Exists(infraProj)) return;
        var addCmd = $"dotnet add \"{infraProj}\" package {package} --version {version}";
        if (!Program.RunCommand(addCmd, config.SolutionPath, print: false))
        {
            var doc = XDocument.Load(infraProj);
            var ns = doc.Root!.Name.Namespace;
            var itemGroup = doc.Root.Elements(ns + "ItemGroup").FirstOrDefault();
            if (itemGroup == null)
            {
                itemGroup = new XElement(ns + "ItemGroup");
                doc.Root.Add(itemGroup);
            }
            var exists = itemGroup.Elements(ns + "PackageReference")
                .Any(x => string.Equals(x.Attribute("Include")?.Value, package, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                itemGroup.Add(new XElement(ns + "PackageReference",
                    new XAttribute("Include", package),
                    new XAttribute("Version", version)));
                doc.Save(infraProj);
            }
        }
    }

    static void EnsureAppSettingsFiles(SolutionConfig config, string provider)
    {
        var projectDir = Path.Combine(config.SolutionPath, config.StartupProject);
        var configDir = Path.Combine(projectDir, "config", "settings");
        Directory.CreateDirectory(configDir);

        var defaultFile = Path.Combine(projectDir, "appsettings.json");
        if (File.Exists(defaultFile)) File.Delete(defaultFile);

        foreach (var env in new[] { "development", "test", "production" })
        {
            var fileName = $"appsettings.{env}.json";
            var path = Path.Combine(configDir, fileName);

            var oldCap = Path.Combine(projectDir, $"appsettings.{char.ToUpper(env[0]) + env[1..]}.json");
            if (File.Exists(oldCap)) File.Move(oldCap, path, true);
            var oldLower = Path.Combine(projectDir, fileName);
            if (File.Exists(oldLower)) File.Move(oldLower, path, true);

            if (!File.Exists(path))
                continue;

            JsonNode root = File.Exists(path) && !string.IsNullOrWhiteSpace(File.ReadAllText(path))
                ? JsonNode.Parse(File.ReadAllText(path))!
                : new JsonObject();
            if (root[provider] == null)
            {
                var obj = new JsonObject
                {
                    ["Host"] = "localhost",
                    ["Port"] = provider == "Redis" ? 6379 : 5672
                };
                root[provider] = obj;
                File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }
}

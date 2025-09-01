using System;
using System.IO;
using System.Linq;
using DotNetArch.Scaffolding;

static class ServiceScaffolder
{
    public static void Generate(SolutionConfig config)
    {
        var type = Program.AskOption("Select service type", new[] { "Custom", "Cache", "Message Broker" });
        var isExternal = false;
        string provider = string.Empty;
        if (type == "Custom")
        {
            isExternal = !Program.AskYesNo("Is this an internal logic service?", true);
        }
        else if (type == "Cache")
        {
            isExternal = true;
            provider = Program.AskOption("Select cache provider", new[] { "Redis" });
        }
        else if (type == "Message Broker")
        {
            isExternal = true;
            provider = Program.AskOption("Select message broker", new[] { "RabbitMQ" });
        }

        var serviceName = Program.Ask("Enter service name");
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Program.Error("Service name is required.");
            return;
        }
        var entityName = Program.Ask("Enter entity name (optional)");
        var layer = isExternal ? "Infrastructure" : "Application";
        var solution = config.SolutionName;
        var root = Path.Combine(config.SolutionPath, $"{solution}.{layer}");
        var servicesRoot = Path.Combine(root, "Services");
        if (string.IsNullOrWhiteSpace(entityName))
            servicesRoot = Path.Combine(servicesRoot, "Common");
        else
            servicesRoot = Path.Combine(servicesRoot, entityName);
        Directory.CreateDirectory(servicesRoot);

        var ns = $"{solution}.{layer}.Services" + (string.IsNullOrWhiteSpace(entityName) ? ".Common" : $".{entityName}");
        var iface = $"I{serviceName}";
        File.WriteAllText(Path.Combine(servicesRoot, $"{iface}.cs"), $"namespace {ns};{Environment.NewLine}{Environment.NewLine}public interface {iface} {{ }}{Environment.NewLine}");
        File.WriteAllText(Path.Combine(servicesRoot, $"{serviceName}.cs"), $"namespace {ns};{Environment.NewLine}{Environment.NewLine}public class {serviceName} : {iface} {{ }}{Environment.NewLine}");

        // register in DI
        var programPath = Path.Combine(config.SolutionPath, $"{config.StartupProject}/Program.cs");
        if (File.Exists(programPath))
        {
            var lines = File.ReadAllLines(programPath).ToList();
            var idx = lines.FindIndex(l => l.Contains("var builder"));
            if (idx >= 0)
            {
                var registration = $"builder.Services.AddScoped<{iface}, {serviceName}>();";
                if (!lines.Any(l => l.Contains(registration)))
                    lines.Insert(idx + 1, registration);
                File.WriteAllLines(programPath, lines);
            }
        }

        Program.Success($"{serviceName} service generated.");
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetArch.Scaffolding;

static class EventScaffolder
{
    public static string[] ListEvents(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var eventsDir = Path.Combine(config.SolutionPath, $"{solution}.Application", "Features", plural, "Events");
        if (!Directory.Exists(eventsDir))
            return Array.Empty<string>();
        return Directory.GetFiles(eventsDir, $"{entity}*Event.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n.StartsWith(entity) && n.EndsWith("Event"))
            .Select(n => n.Substring(entity.Length, n.Length - entity.Length - "Event".Length))
            .ToArray();
    }

    public static bool GenerateEvent(SolutionConfig config, string entity, string eventName)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) || string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(eventName))
        {
            Program.Error("Solution, entity and event names are required.");
            return false;
        }
        eventName = Upper(eventName);
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var appDir = Path.Combine(config.SolutionPath, $"{solution}.Application");
        var featureDir = Path.Combine(appDir, "Features", plural);
        if (!Directory.Exists(featureDir))
            return false;
        var eventsDir = Path.Combine(featureDir, "Events");
        Directory.CreateDirectory(eventsDir);
        var eventClass = $"{entity}{eventName}Event";
        var file = Path.Combine(eventsDir, eventClass + ".cs");
        if (!File.Exists(file))
        {
            var content = $@"using MediatR;
using {solution}.Core.Features.{plural}.Entities;

namespace {solution}.Application.Features.{plural}.Events;

public class {eventClass} : INotification
{{
    public {entity} {entity} {{ get; }}
    public {eventClass}({entity} {LowerFirst(entity)}) => {entity} = {LowerFirst(entity)};
}}";
            File.WriteAllText(file, content);
        }
        return true;
    }

    public static bool AddSubscriber(SolutionConfig config, string eventEntity, string eventName, string subscriberEntity)
    {
        var solution = config.SolutionName;
        var subscriberPlural = Naming.Pluralize(subscriberEntity);
        var subFeatureDir = Path.Combine(config.SolutionPath, $"{solution}.Application", "Features", subscriberPlural);
        if (!Directory.Exists(subFeatureDir))
            return false;
        var handlersDir = Path.Combine(subFeatureDir, "EventHandlers");
        Directory.CreateDirectory(handlersDir);
        eventName = Upper(eventName);
        var handlerClass = $"{eventEntity}{eventName}EventHandler";
        var file = Path.Combine(handlersDir, handlerClass + ".cs");
        var eventPlural = Naming.Pluralize(eventEntity);
        var content = $@"using System.Threading;
using System.Threading.Tasks;
using MediatR;
using {solution}.Application.Features.{eventPlural}.Events;

namespace {solution}.Application.Features.{subscriberPlural}.EventHandlers;

public class {handlerClass} : INotificationHandler<{eventEntity}{eventName}Event>
{{
    public Task Handle({eventEntity}{eventName}Event notification, CancellationToken cancellationToken)
    {{
        // TODO: Add handling logic
        return Task.CompletedTask;
    }}
}}";
        File.WriteAllText(file, content);
        return true;
    }

    static string LowerFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToLower(text[0]) + text.Substring(1);
    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
}

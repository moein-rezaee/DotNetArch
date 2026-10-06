namespace DotNetArch.Cli.Commands;

internal sealed class NewEventCommand : SolutionCommandBase
{
    private const string CreateNew = "Create new event";
    private const string AddToExisting = "Add subscriber to existing events";
    private const string Back = "Back";
    private const string Cancel = "Cancel";

    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "event");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var entity = parsed.Get("entity");
        var eventName = parsed.Get("name");

        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        while (true)
        {
            if (string.IsNullOrWhiteSpace(entity))
                entity = ToolHost.Ask("Enter entity name");
            if (string.IsNullOrWhiteSpace(entity))
            {
                ToolHost.Error("Entity name is required.");
                return 1;
            }
            entity = Identifier.Sanitize(entity);

            if (!EventScaffolder.EntityExists(config, entity))
            {
                ToolHost.Error($"Entity '{entity}' does not exist.");
                entity = null;
                eventName = null;
                continue;
            }

            var events = EventScaffolder.ListEvents(config, entity);
            if (events.Length == 0 && string.IsNullOrWhiteSpace(eventName))
                eventName = ToolHost.Ask("Enter event name");

            if (string.IsNullOrWhiteSpace(eventName))
            {
                var action = ToolHost.AskOption($"Entity '{entity}' has existing events. Select action", new[] { CreateNew, AddToExisting, Cancel });
                if (action == Cancel)
                    return 0;

                if (action == CreateNew)
                {
                    eventName = ToolHost.Ask("Enter event name");
                    if (string.IsNullOrWhiteSpace(eventName))
                    {
                        ToolHost.Error("Event name is required.");
                        return 1;
                    }
                    eventName = Identifier.Sanitize(eventName);
                    if (!EventScaffolder.GenerateEvent(config, entity, eventName))
                    {
                        eventName = null;
                        continue;
                    }
                    ToolHost.Success($"Event {eventName} for {entity} generated.");
                    events = events.Append(eventName).ToArray();
                }
                else
                {
                    var selected = ToolHost.AskOption("Select event", events.Concat(new[] { Back, Cancel }).ToArray());
                    if (selected == Back)
                    {
                        eventName = null;
                        continue;
                    }
                    if (selected == Cancel)
                        return 0;
                    eventName = selected;
                }
            }
            else
            {
                eventName = Identifier.Sanitize(eventName);
                if (!EventScaffolder.GenerateEvent(config, entity, eventName))
                {
                    eventName = null;
                    continue;
                }
                ToolHost.Success($"Event {eventName} for {entity} generated.");
                events = events.Append(eventName).ToArray();
            }

            return AddSubscribers(config, entity, eventName!, events);
        }
    }

    private static int AddSubscribers(SolutionConfig config, string entity, string eventName, string[] events)
    {
        var currentEvent = eventName;
        while (true)
        {
            var options = events.Length > 1
                ? new[] { "Add subscriber", "Add subscriber for other events", "Finish" }
                : new[] { "Add subscriber", "Finish" };
            var choice = ToolHost.AskOption("Select action", options);
            if (choice == "Add subscriber")
            {
                var sub = Identifier.Sanitize(ToolHost.Ask("Enter subscriber entity"));
                if (!EventScaffolder.AddSubscriber(config, entity, currentEvent, sub))
                    continue;

                ToolHost.Success($"Subscriber {sub} added.");
            }
            else if (choice == "Add subscriber for other events")
            {
                var selected = ToolHost.AskOption("Select event", events.Concat(new[] { Back, Cancel }).ToArray());
                if (selected == Back)
                    continue;
                if (selected == Cancel)
                    return 0;
                currentEvent = selected;
            }
            else
            {
                return 0;
            }
        }
    }
}

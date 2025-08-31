using System.Collections.Generic;

public class SolutionConfig
{
    public string SolutionName { get; set; } = "";
    public string SolutionPath { get; set; } = "";
    public string StartupProject { get; set; } = "";
    public string DatabaseProvider { get; set; } = "";
    public string ApiStyle { get; set; } = "controller";
    public string Os { get; set; } = "";

    public Dictionary<string, EntityStatus> Entities { get; set; } = new();
}

public class EntityStatus
{
    public bool HasCrud { get; set; }
    public bool HasAction { get; set; }
}

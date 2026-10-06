using System;

namespace DotNetArch.Core.Scaffolding;

public static class DatabaseProviderSelector
{
    /// <summary>Provider choice for v2 solutions: every supported engine is enabled, SQLite is the default.</summary>
    public static string ChooseV2()
    {
        var option = ToolHost.AskOption("Select database provider", new[] { "SQLite", "SQL Server", "PostgreSQL" }, 0);
        return option switch
        {
            "SQL Server" => V2.DatabaseProviders.SqlServer,
            "PostgreSQL" => V2.DatabaseProviders.Postgres,
            _ => V2.DatabaseProviders.Sqlite
        };
    }

    public static string Choose()
    {
        var option = ToolHost.AskOption(
            "Select database provider",
            new[] { "SQL Server", "SQLite", "PostgreSQL", "MongoDB", "No Database" },
            1,
            // keep advanced providers disabled by default; allow SQLite and No Database
            new[] { 0, 2, 3 });
        return option switch
        {
            "SQL Server"  => "SqlServer",
            "PostgreSQL"  => "Postgres",
            "MongoDB"     => "Mongo",
            "No Database" => "None",
            _              => "SQLite"
        };
    }
}

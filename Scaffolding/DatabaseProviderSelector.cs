using System;

namespace DotNetArch.Scaffolding;

public static class DatabaseProviderSelector
{
    public static string Choose()
    {
        var option = Program.AskOption(
            "Select database provider",
            new[] { "SQL Server", "SQLite", "PostgreSQL", "MongoDB" },
            1,
            new[] { 0, 2, 3 });
        return option switch
        {
            "SQL Server"  => "SqlServer",
            "PostgreSQL"  => "Postgres",
            "MongoDB"     => "Mongo",
            _              => "SQLite"
        };
    }
}

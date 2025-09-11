using System;

namespace DotNetArch.Scaffolding;

public static class DatabaseProviderSelector
{
    public static string Choose()
    {
        var option = Program.AskOption(
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

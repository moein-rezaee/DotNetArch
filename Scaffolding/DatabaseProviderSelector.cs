using System;

namespace DotNetArch.Scaffolding;

public static class DatabaseProviderSelector
{
    public static string Choose()
    {
        Console.WriteLine("Select database provider (default SQLite):");
        Console.WriteLine("1 - SQL Server");
        Console.WriteLine("2 - SQLite");
        Console.WriteLine("3 - PostgreSQL");
        Console.WriteLine("4 - MongoDB");
        Console.Write("Your choice: ");
        var choice = Console.ReadLine();
        return choice switch
        {
            "1" => "SqlServer",
            "3" => "Postgres",
            "4" => "Mongo",
            _   => "SQLite"
        };
    }
}

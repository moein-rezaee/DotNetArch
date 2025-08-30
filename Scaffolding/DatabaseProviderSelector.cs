using System;

namespace DotNetArch.Scaffolding;

public static class DatabaseProviderSelector
{
    public static string Choose()
    {
        Console.WriteLine("Select database provider:");
        Console.WriteLine("1 - SQL Server");
        Console.WriteLine("2 - SQLite");
        Console.Write("Your choice: ");
        var choice = Console.ReadLine();
        return choice == "2" ? "SQLite" : "SqlServer";
    }
}

using DotNetArch.Core.Hosting;

namespace DotNetArch.Cli.Console;

/// <summary>Interactive terminal prompts (text, yes/no, arrow-key option list).</summary>
public sealed class ConsolePrompter(IToolOutput output) : IPrompter
{
    public string Ask(string message, string? defaultValue = null)
    {
        output.Blank();
        System.Console.Write($"{message}{(defaultValue != null ? $" [{defaultValue}]" : "")}: ");
        var input = System.Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? (defaultValue ?? string.Empty) : input;
    }

    public bool AskYesNo(string message, bool defaultYes)
    {
        output.Blank();
        var def = defaultYes ? "y" : "n";
        while (true)
        {
            System.Console.Write($"{message} (y/n) [{def}]: ");
            var input = System.Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input))
                return defaultYes;
            if (input is "y" or "yes")
                return true;
            if (input is "n" or "no")
                return false;
            System.Console.WriteLine("Please enter 'y' or 'n'.");
        }
    }

    public string AskOption(string message, string[] options, int defaultIndex = 0, int[]? disabledIndices = null)
    {
        output.Blank();
        System.Console.WriteLine(message);
        var disabled = disabledIndices != null ? new HashSet<int>(disabledIndices) : new HashSet<int>();

        var index = Math.Clamp(defaultIndex, 0, options.Length - 1);
        if (disabled.Contains(index))
            index = Enumerable.Range(0, options.Length).First(i => !disabled.Contains(i));

        // Piped/redirected input cannot drive an arrow-key menu: take the default.
        if (System.Console.IsInputRedirected)
            return options[index];

        System.Console.CursorVisible = false;
        while (true)
        {
            for (var i = 0; i < options.Length; i++)
            {
                var isDisabled = disabled.Contains(i);
                var isSelected = i == index;
                var prefix = isSelected ? "➤" : " ";
                if (isDisabled)
                    System.Console.ForegroundColor = ConsoleColor.DarkGray;
                else if (isSelected)
                    System.Console.ForegroundColor = ConsoleColor.Cyan;

                System.Console.WriteLine($"{prefix} {options[i]}");
                System.Console.ResetColor();
            }

            var key = System.Console.ReadKey(true).Key;
            if (key == ConsoleKey.UpArrow)
            {
                do { index = index == 0 ? options.Length - 1 : index - 1; } while (disabled.Contains(index));
            }
            else if (key == ConsoleKey.DownArrow)
            {
                do { index = index == options.Length - 1 ? 0 : index + 1; } while (disabled.Contains(index));
            }
            else if (key == ConsoleKey.Enter)
            {
                System.Console.CursorVisible = true;
                System.Console.SetCursorPosition(0, System.Console.CursorTop);
                return options[index];
            }

            System.Console.SetCursorPosition(0, System.Console.CursorTop - options.Length);
        }
    }
}

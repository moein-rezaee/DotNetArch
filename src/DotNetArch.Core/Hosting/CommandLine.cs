using System.Text;
using System.Text.RegularExpressions;

namespace DotNetArch.Core.Hosting;

/// <summary>
/// Splits a command line string into a <see cref="ProcessSpec"/> without involving a shell.
/// Supports double/single quotes and leading <c>KEY=value</c> environment assignments.
/// Shell operators (<c>&amp;&amp;</c>, pipes, redirects) are intentionally unsupported.
/// </summary>
public static class CommandLine
{
    private static readonly Regex EnvAssignment = new("^[A-Za-z_][A-Za-z0-9_]*=", RegexOptions.Compiled);

    public static ProcessSpec Parse(string commandLine, string? workingDirectory = null)
    {
        var tokens = Tokenize(commandLine);
        var env = new Dictionary<string, string>();
        var index = 0;
        while (index < tokens.Count && EnvAssignment.IsMatch(tokens[index]))
        {
            var token = tokens[index++];
            var eq = token.IndexOf('=');
            env[token[..eq]] = token[(eq + 1)..];
        }

        if (index >= tokens.Count)
            throw new ArgumentException("Command line has no executable.", nameof(commandLine));

        return new ProcessSpec(tokens[index], tokens.Skip(index + 1).ToArray(), workingDirectory, env.Count == 0 ? null : env);
    }

    public static List<string> Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var hasToken = false;
        foreach (var ch in commandLine)
        {
            if (quote is not null)
            {
                if (ch == quote) quote = null;
                else current.Append(ch);
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (hasToken || current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (quote is not null)
            throw new ArgumentException("Unterminated quote in command line.", nameof(commandLine));
        if (hasToken || current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }
}

namespace Kirin_Tool.Cli;

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}

/// <summary>One documented command parameter (also feeds the MCP input schema).</summary>
internal sealed record CliParam(
    string Name,
    string Type,
    string Description,
    bool Required = false,
    bool Positional = false,
    IReadOnlyList<string>? Enum = null,
    string? Default = null);

internal sealed class ParsedArgs
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Positionals { get; } = new();

    public IReadOnlyDictionary<string, string> Values => _values;
    public bool HasFlag(string name) => _flags.Contains(name);
    public bool HasValue(string name) => _values.ContainsKey(name);
    public string? Value(string name) => _values.TryGetValue(name, out string? value) ? value : null;
    public void SetFlag(string name) => _flags.Add(name);
    public void SetValue(string name, string value) => _values[name] = value;
}

internal static class ArgParser
{
    /// <summary>
    /// Minimal, predictable parser: <c>--name value</c>, <c>--name=value</c>, short aliases
    /// (-o output, -y yes, -j json, -h help) and bare positionals. Unknown options are flags.
    /// </summary>
    public static ParsedArgs Parse(IReadOnlyList<string> args, IEnumerable<string> valueOptions)
    {
        var parsed = new ParsedArgs();
        var values = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (token == "--")
            {
                for (int j = i + 1; j < args.Count; j++) parsed.Positionals.Add(args[j]);
                break;
            }

            if (token.Length > 1 && token[0] == '-')
            {
                string name = token.StartsWith("--", StringComparison.Ordinal) ? token[2..] : token[1..];
                string? inline = null;
                int equals = name.IndexOf('=');
                if (equals >= 0)
                {
                    inline = name[(equals + 1)..];
                    name = name[..equals];
                }

                name = name.ToLowerInvariant() switch
                {
                    "o" => "output",
                    "y" => "yes",
                    "j" => "json",
                    "h" => "help",
                    _ => name,
                };

                if (inline != null)
                {
                    parsed.SetValue(name, inline);
                    continue;
                }

                if (!values.Contains(name))
                {
                    parsed.SetFlag(name);
                    continue;
                }

                if (i + 1 >= args.Count)
                    throw new CliUsageException($"option --{name} requires a value");
                parsed.SetValue(name, args[++i]);
                continue;
            }

            parsed.Positionals.Add(token);
        }

        return parsed;
    }
}

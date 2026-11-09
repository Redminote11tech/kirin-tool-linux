namespace Kirin_Tool.Cli.Commands;

/// <summary>Local file/parse failure mapped to exit code 5.</summary>
internal sealed class CliFileException : Exception
{
    public CliFileException(string message) : base(message) { }
}

internal interface ICliCommand
{
    string Name { get; }
    string McpName { get; }
    string Summary { get; }
    string Usage { get; }
    bool IsWrite { get; }
    IReadOnlyList<CliParam> Params { get; }
    Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken);
}

internal sealed record WritePlan(object Details, IReadOnlyList<string> Lines);

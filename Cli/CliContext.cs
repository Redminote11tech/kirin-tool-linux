using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kirin_Tool.Cli;

/// <summary>
/// Everything a command may touch. Console access is injected so the same
/// command implementation can serve the CLI (stdout) and the MCP server
/// (JSON-RPC response text) without ever polluting stdout in MCP mode.
/// </summary>
internal sealed class CliContext
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public CliContext(TextWriter output, TextWriter error, bool json, bool yes, string workingDirectory)
    {
        Output = output;
        Error = error;
        Json = json;
        Yes = yes;
        WorkingDirectory = workingDirectory;
    }

    public TextWriter Output { get; }
    public TextWriter Error { get; }
    public bool Json { get; }
    public bool Yes { get; }
    public string WorkingDirectory { get; }

    public void WriteJson(object value) => Output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    /// <summary>Humans-only progress/diagnostics: always stderr.</summary>
    public void Diag(string message) => Error.WriteLine(message);
}

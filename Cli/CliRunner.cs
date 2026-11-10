using System.Text;
using Kirin_Tool.Cli.Commands;
using Kirin_Tool.Cli.Mcp;

namespace Kirin_Tool.Cli;

/// <summary>Headless entry point shared by the KirinTool.Cli executable and the main exe's --mcp switch.</summary>
public static class CliRunner
{
    internal static IReadOnlyList<ICliCommand> Commands { get; } = new ICliCommand[]
    {
        new InfoCommand(),
        new DoctorCommand(),
        new GetVarCommand(),
        new ExtractCommand(),
        new DumpCommand(),
        new FlashOtaCommand(),
        new FlashXmlCommand(),
        new VcomUnlockCommand(),
        new OemInfoCommand(),
        new DowngradeEnableCommand(),
    };

    public static async Task<int> RunAsync(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        TextWriter output = stdout ?? Console.Out;
        TextWriter error = stderr ?? Console.Error;

        try
        {
            if (args.Length == 0)
            {
                WriteUsage(error);
                return ExitCodes.Usage;
            }

            string first = args[0];
            string normalized = first.TrimStart('-').ToLowerInvariant();

            if (normalized is "help" or "h")
            {
                WriteUsage(output);
                return ExitCodes.Ok;
            }

            if (normalized == "version")
            {
                output.WriteLine($"{ToolInfo.Name} {ToolInfo.Version} ({ToolInfo.Framework})");
                return ExitCodes.Ok;
            }

            if (normalized == "mcp")
                return await McpEntry.RunAsync();

            ICliCommand? command = Commands.FirstOrDefault(candidate => candidate.Name == normalized);
            if (command == null)
            {
                error.WriteLine($"error: unknown command '{first}'");
                WriteUsage(error);
                return ExitCodes.Usage;
            }

            ParsedArgs parsed = ArgParser.Parse(args.Skip(1).ToArray(), ValueOptions(command));
            if (parsed.HasFlag("help"))
            {
                output.WriteLine(command.Usage);
                return ExitCodes.Ok;
            }

            var context = new CliContext(
                output,
                error,
                parsed.HasFlag("json"),
                parsed.HasFlag("yes"),
                Directory.GetCurrentDirectory());

            return await command.ExecuteAsync(parsed, context, CancellationToken.None);
        }
        catch (CliUsageException ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return ExitCodes.Usage;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("error: cancelled");
            return ExitCodes.FileOrVerify;
        }
        catch (Exception ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return ExitCodes.FileOrVerify;
        }
    }

    private static string[] ValueOptions(ICliCommand command) =>
        command.Params
            .Where(parameter => !parameter.Positional && parameter.Type != "boolean")
            .Select(parameter => parameter.Name)
            .ToArray();

    private static void WriteUsage(TextWriter writer)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{ToolInfo.Name} {ToolInfo.Version} - headless Kirin-Tool (read-only first, write commands are gated)");
        builder.AppendLine();
        builder.AppendLine("usage: kirintool <command> [options]");
        builder.AppendLine();
        foreach (ICliCommand command in Commands)
        {
            builder.AppendLine($"  {command.Name,-16} {command.Summary}");
        }
        builder.AppendLine();
        builder.AppendLine("  mcp              Run the MCP stdio JSON-RPC server (tools/list, tools/call)");
        builder.AppendLine();
        builder.AppendLine("global options: --json (structured output), --yes (acknowledge a write plan), -h/--help");
        builder.AppendLine("diagnostics always go to stderr.");
        builder.AppendLine("exit codes: 0 ok, 1 usage, 2 no device, 3 device rejected, 4 gate refused, 5 file/verify failed");
        writer.Write(builder.ToString());
    }
}

/// <summary>Public entry so the Avalonia exe can answer --mcp without starting the GUI.</summary>
public static class McpEntry
{
    public static Task<int> RunAsync(CancellationToken cancellationToken = default) =>
        McpServer.RunAsync(Console.In, Console.Out, Console.Error, cancellationToken);
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Kirin_Tool.Cli.Commands;

namespace Kirin_Tool.Cli.Mcp;

/// <summary>
/// Minimal MCP server over stdio. Transport is newline-delimited JSON-RPC 2.0
/// (one request per line), which keeps stdout a clean protocol stream; all
/// logging goes to stderr. Implements initialize, ping, tools/list, tools/call.
/// </summary>
internal static class McpServer
{
    private const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<int> RunAsync(TextReader input, TextWriter output, TextWriter log, CancellationToken cancellationToken = default)
    {
        log.WriteLine($"[mcp] {ToolInfo.Name} {ToolInfo.Version} stdio JSON-RPC (newline-delimited); protocol {ProtocolVersion}");
        log.WriteLine("[mcp] tools: " + string.Join(", ", CliRunner.Commands.Select(command => command.McpName)));

        string? line;
        while ((line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            JsonObject? request;
            try
            {
                request = JsonNode.Parse(trimmed) as JsonObject;
            }
            catch (JsonException ex)
            {
                await WriteAsync(output, ErrorResponse(null, -32700, "Parse error: " + ex.Message));
                continue;
            }

            if (request == null)
            {
                await WriteAsync(output, ErrorResponse(null, -32600, "Invalid Request"));
                continue;
            }

            JsonNode? id = request["id"];
            string? method = request["method"]?.GetValue<string>();
            if (string.IsNullOrEmpty(method))
            {
                if (id != null) await WriteAsync(output, ErrorResponse(id, -32600, "Invalid Request"));
                continue;
            }

            bool isNotification = id == null;
            JsonObject? parameters = request["params"] as JsonObject;

            try
            {
                switch (method)
                {
                    case "initialize":
                        if (!isNotification) await WriteAsync(output, ResultResponse(id!, Initialize()));
                        break;
                    case "notifications/initialized":
                    case "initialized":
                        break;
                    case "ping":
                        if (!isNotification) await WriteAsync(output, ResultResponse(id!, new JsonObject()));
                        break;
                    case "tools/list":
                        if (!isNotification) await WriteAsync(output, ResultResponse(id!, new JsonObject { ["tools"] = BuildTools() }));
                        break;
                    case "tools/call":
                        if (isNotification) break;
                        if (parameters == null)
                        {
                            await WriteAsync(output, ErrorResponse(id, -32602, "Invalid params: missing tool name"));
                            break;
                        }
                        JsonObject callResult = await CallToolAsync(parameters, log, cancellationToken);
                        await WriteAsync(output, ResultResponse(id!, callResult));
                        break;
                    default:
                        if (!isNotification) await WriteAsync(output, ErrorResponse(id, -32601, $"Method not found: {method}"));
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.WriteLine("[mcp] internal error: " + ex);
                if (!isNotification) await WriteAsync(output, ErrorResponse(id, -32603, "Internal error: " + ex.Message));
            }
        }

        log.WriteLine("[mcp] stdin closed, exiting");
        return ExitCodes.Ok;
    }

    private static JsonObject Initialize() => new()
    {
        ["protocolVersion"] = ProtocolVersion,
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject { ["listChanged"] = false },
        },
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "kirin-tool",
            ["version"] = ToolInfo.Version,
        },
        ["instructions"] = "Read-only tools run immediately. Write tools (flash_ota, flash_xml, vcom_unlock, oeminfo, downgrade_enable) require confirm:true, return a plan, and are gate-only in this build (exit 4, nothing is written).",
    };

    private static JsonArray BuildTools()
    {
        var tools = new JsonArray();
        foreach (ICliCommand command in CliRunner.Commands) tools.Add(BuildTool(command));
        return tools;
    }

    private static JsonObject BuildTool(ICliCommand command)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (CliParam parameter in command.Params)
        {
            var schema = new JsonObject
            {
                ["type"] = parameter.Type,
                ["description"] = parameter.Description,
            };
            if (parameter.Enum is { Count: > 0 })
            {
                schema["enum"] = new JsonArray(parameter.Enum.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());
            }
            if (parameter.Default != null) schema["default"] = parameter.Default;
            properties[parameter.Name] = schema;
            if (parameter.Required) required.Add(parameter.Name);
        }

        if (command.IsWrite)
        {
            properties["confirm"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "Acknowledge the printed plan. Required for write tools; true still does not write in this build (gate-only).",
                ["default"] = false,
            };
            required.Add("confirm");
        }

        var inputSchema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
        if (required.Count > 0) inputSchema["required"] = required;

        var tool = new JsonObject
        {
            ["name"] = command.McpName,
            ["description"] = command.Summary + " Usage: " + command.Usage,
            ["inputSchema"] = inputSchema,
            ["annotations"] = command.IsWrite
                ? new JsonObject { ["readOnlyHint"] = false, ["destructiveHint"] = true, ["idempotentHint"] = false }
                : new JsonObject { ["readOnlyHint"] = true, ["idempotentHint"] = true },
        };
        return tool;
    }

    private static async Task<JsonObject> CallToolAsync(JsonObject parameters, TextWriter log, CancellationToken cancellationToken)
    {
        string? name = parameters["name"]?.GetValue<string>();
        var arguments = parameters["arguments"] as JsonObject;

        ICliCommand? command = CliRunner.Commands.FirstOrDefault(candidate =>
            string.Equals(candidate.McpName, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (command == null)
            return ToolResult($"Unknown tool: {name}", ExitCodes.Usage, stderr: null);

        ParsedArgs parsed = ToParsedArgs(command, arguments);
        bool confirm = arguments?["confirm"]?.GetValue<bool>() == true;
        if (confirm) parsed.SetFlag("yes");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var context = new CliContext(stdout, stderr, json: true, yes: confirm, workingDirectory: Directory.GetCurrentDirectory());

        int exit;
        try
        {
            exit = await command.ExecuteAsync(parsed, context, cancellationToken);
        }
        catch (CliUsageException ex)
        {
            exit = ExitCodes.Usage;
            stderr.WriteLine("error: " + ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            exit = ExitCodes.FileOrVerify;
            stderr.WriteLine("error: " + ex.Message);
        }

        string diagnostics = stderr.ToString().Trim();
        if (diagnostics.Length > 0) log.WriteLine($"[mcp] {command.McpName} stderr: {diagnostics}");
        log.WriteLine($"[mcp] {command.McpName} exit={exit}");

        string text = stdout.ToString().Trim();
        if (text.Length == 0)
            text = new JsonObject { ["exitCode"] = exit }.ToJsonString();

        return ToolResult(text, exit, diagnostics);
    }

    private static ParsedArgs ToParsedArgs(ICliCommand command, JsonObject? arguments)
    {
        var parsed = new ParsedArgs();
        if (arguments == null) return parsed;

        foreach (CliParam parameter in command.Params)
        {
            if (!arguments.TryGetPropertyValue(parameter.Name, out JsonNode? node) || node == null) continue;

            if (parameter.Type == "boolean")
            {
                if (node.GetValue<bool>()) parsed.SetFlag(parameter.Name);
                continue;
            }

            string value = node.GetValue<string>();
            parsed.SetValue(parameter.Name, value);
        }

        foreach (CliParam parameter in command.Params.Where(parameter => parameter.Positional))
        {
            string? value = parsed.Value(parameter.Name);
            if (value != null) parsed.Positionals.Add(value);
        }

        return parsed;
    }

    private static JsonObject ToolResult(string text, int exitCode, string? stderr)
    {
        JsonNode? structured = null;
        try
        {
            structured = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            structured = null;
        }

        // Exit 4 is the gate refusing a write: the plan is a valid result, not a protocol error.
        bool isError = exitCode != ExitCodes.Ok && exitCode != ExitCodes.Gated;
        var result = new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = text,
            }),
            ["isError"] = isError,
            ["_meta"] = new JsonObject
            {
                ["exitCode"] = exitCode,
                ["stderr"] = stderr,
            },
        };
        if (structured != null) result["structuredContent"] = structured;
        return result;
    }

    private static JsonObject ResultResponse(JsonNode id, JsonObject result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.DeepClone(),
        ["result"] = result,
    };

    private static JsonObject ErrorResponse(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        },
    };

    private static async Task WriteAsync(TextWriter output, JsonObject message)
    {
        await output.WriteLineAsync(message.ToJsonString(CompactJson)).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }
}

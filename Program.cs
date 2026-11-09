using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Kirin_Tool.Cli;

namespace Kirin_Tool;

class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Headless MCP entry: must not initialize Avalonia nor write anything to
        // stdout except JSON-RPC responses (the server logs to stderr only).
        if (args.Length > 0 && args[0] is "--mcp" or "mcp")
        {
            return McpEntry.RunAsync().GetAwaiter().GetResult();
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseSkia()
        .LogToTrace();
}

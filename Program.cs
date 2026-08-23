using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Kirin_Tool;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseSkia()
        .LogToTrace();
}

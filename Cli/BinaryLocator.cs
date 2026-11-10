namespace Kirin_Tool.Cli;

/// <summary>
/// Resolves fastboot/adb without touching the GUI's helpers: explicit env override,
/// the app-local bundled layout, then PATH. Never downloads anything.
/// </summary>
internal static class BinaryLocator
{
    public const string FastbootOverrideVariable = "KIRIN_FASTBOOT";
    public const string AdbOverrideVariable = "KIRIN_ADB";

    public static string? FindFastboot() => Find(FastbootOverrideVariable, "fastboot");

    public static string? FindAdb() => Find(AdbOverrideVariable, "adb");

    private static string? Find(string overrideVariable, string name)
    {
        var overrideValue = Environment.GetEnvironmentVariable(overrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            string expanded = Environment.ExpandEnvironmentVariables(overrideValue.Trim());
            if (File.Exists(expanded)) return Path.GetFullPath(expanded);
            string? located = FindOnPath(expanded);
            if (located != null) return located;
        }

        string executable = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (string candidate in Candidates(name, executable))
        {
            if (File.Exists(candidate) && IsExecutable(candidate)) return Path.GetFullPath(candidate);
        }

        return FindOnPath(executable);
    }

    private static IEnumerable<string> Candidates(string name, string executable)
    {
        yield return Path.Combine(AppContext.BaseDirectory, executable);
        yield return Path.Combine(AppContext.BaseDirectory, name, executable);
        yield return Path.Combine(AppContext.BaseDirectory, "tools", executable);
        // Side-by-side build output: <repo>/KirinTool.Cli/bin/... vs <repo>/bin/...
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", executable));
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Kirin-Tool", executable));
        if (!OperatingSystem.IsWindows())
        {
            yield return "/usr/bin/" + executable;
            yield return "/usr/local/bin/" + executable;
        }
    }

    public static string? FindOnPath(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return null;
        if (executable.Contains(Path.DirectorySeparatorChar) || executable.Contains('/'))
            return File.Exists(executable) && IsExecutable(executable) ? Path.GetFullPath(executable) : null;

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(directory, executable);
            }
            catch (ArgumentException)
            {
                continue;
            }
            if (File.Exists(candidate) && IsExecutable(candidate)) return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static bool IsExecutable(string path)
    {
        if (OperatingSystem.IsWindows()) return true;
        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

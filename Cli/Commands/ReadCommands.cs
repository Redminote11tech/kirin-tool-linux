using System.Diagnostics;
using Kirin_Tool.Models;
using Kirin_Tool.Services;

namespace Kirin_Tool.Cli.Commands;

internal sealed class InfoCommand : ICliCommand
{
    public string Name => "info";
    public string McpName => "info";
    public string Summary => "Show tool, fastboot and connected-device information (read-only).";
    public string Usage => "info [--json]";
    public bool IsWrite => false;
    public IReadOnlyList<CliParam> Params => Array.Empty<CliParam>();

    public async Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        var session = await FastbootSession.TryOpenAsync();
        int exit = session == null ? ExitCodes.FileOrVerify
            : session.Connected ? ExitCodes.Ok
            : ExitCodes.NoDevice;

        var variables = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        bool psidReadable = false;
        int? partitionCount = null;

        if (session is { Connected: true })
        {
            foreach (string name in new[] { "product", "version", "serialno", "secure", "unlocked", "chipset", "base-version", "bootloader" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                variables[name] = await DeviceVars.TryReadAsync(session.Client, name);
            }
            psidReadable = await DeviceVars.PsidReadableAsync(session.Client);
            partitionCount = await DeviceVars.PartitionCountAsync(session.Client);
        }

        string chip = variables.TryGetValue("chipset", out string? chipValue) && !string.IsNullOrWhiteSpace(chipValue)
            ? chipValue!.Trim().ToLowerInvariant()
            : "unknown";

        var rescue = RescuePartitionPlan.ForChip(chip)
            .Select(entry => new { image = entry.Image, target = entry.Target })
            .ToList();

        var payload = new
        {
            tool = new
            {
                name = ToolInfo.Name,
                version = ToolInfo.Version,
                framework = ToolInfo.Framework,
            },
            fastboot = new
            {
                found = session != null,
                path = session?.ExecutablePath,
            },
            device = new
            {
                connected = session?.Connected ?? false,
                chip,
                psidReadable,
                partitionCount,
                variables = variables.Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value!),
            },
            capabilities = new
            {
                kirin9000 = string.Equals(chip, ChipCapabilities.Kirin9000, StringComparison.OrdinalIgnoreCase),
                readOnly = ChipCapabilities.Allows(chip, "read-info"),
                rescuePlan = rescue,
            },
        };

        if (context.Json)
        {
            context.WriteJson(payload);
        }
        else
        {
            context.Output.WriteLine($"{ToolInfo.Name} {ToolInfo.Version} ({ToolInfo.Framework})");
            context.Output.WriteLine(session == null
                ? "fastboot: not found (set KIRIN_FASTBOOT or add fastboot to PATH)"
                : $"fastboot: {session.ExecutablePath}");
            context.Output.WriteLine($"device: {(session?.Connected == true ? "connected" : "not connected")}");
            foreach (var pair in variables.Where(pair => pair.Value != null))
                context.Output.WriteLine($"  {pair.Key}: {pair.Value}");
            if (session is { Connected: true })
            {
                context.Output.WriteLine($"  psid-readable: {psidReadable}");
                context.Output.WriteLine($"  partition-count: {partitionCount?.ToString() ?? "unknown"}");
            }
            context.Output.WriteLine($"capabilities (chip: {chip}): kirin9000={string.Equals(chip, ChipCapabilities.Kirin9000, StringComparison.OrdinalIgnoreCase)}");
            foreach (var entry in rescue)
                context.Output.WriteLine($"  rescue: {entry.image} -> {entry.target}");
        }

        if (session == null)
            context.Diag("error: fastboot binary not found; set KIRIN_FASTBOOT or install platform-tools");
        else if (!session.Connected)
            context.Diag("note: no fastboot device detected (exit 2)");

        return exit;
    }
}

internal sealed class DoctorCommand : ICliCommand
{
    public string Name => "doctor";
    public string McpName => "doctor";
    public string Summary => "Diagnose toolchain: fastboot/adb paths, device enumeration and loader capability probes.";
    public string Usage => "doctor [--json]";
    public bool IsWrite => false;
    public IReadOnlyList<CliParam> Params => Array.Empty<CliParam>();

    public async Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        string? fastbootPath = BinaryLocator.FindFastboot();
        string? adbPath = BinaryLocator.FindAdb();

        string? fastbootVersion = fastbootPath == null ? null : await ToolProbe.VersionAsync(fastbootPath);
        string? adbVersion = adbPath == null ? null : await ToolProbe.VersionAsync(adbPath);

        var fastbootDevices = fastbootPath == null
            ? Array.Empty<string>()
            : await ToolProbe.FastbootDevicesAsync(fastbootPath);
        var adbDevices = adbPath == null
            ? Array.Empty<string>()
            : await ToolProbe.AdbDevicesAsync(adbPath);

        var session = await FastbootSession.TryOpenAsync();
        bool connected = session?.Connected ?? false;

        var probes = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        bool psidReadable = false;
        int? partitionCount = null;
        if (connected && session != null)
        {
            foreach (string name in new[] { "product", "chipset", "secure", "unlocked", "version" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                probes[name] = await DeviceVars.TryReadAsync(session.Client, name);
            }
            psidReadable = await DeviceVars.PsidReadableAsync(session.Client);
            partitionCount = await DeviceVars.PartitionCountAsync(session.Client);
        }

        string chip = probes.TryGetValue("chipset", out string? chipValue) && !string.IsNullOrWhiteSpace(chipValue)
            ? chipValue!.Trim().ToLowerInvariant()
            : "unknown";

        var loaders = BundledLoaders.Inventory();
        var checks = new List<object>
        {
            new { name = "fastboot-found", ok = fastbootPath != null, detail = fastbootPath ?? "not found (KIRIN_FASTBOOT / PATH)" },
            new { name = "fastboot-runs", ok = fastbootVersion != null, detail = fastbootVersion ?? "fastboot --version failed" },
            new { name = "adb-found", ok = adbPath != null, detail = adbPath ?? "not found (optional, KIRIN_ADB / PATH)" },
            new { name = "loaders-bundled", ok = loaders.Count > 0, detail = $"{loaders.Count} chip directories under {BundledLoaders.Directory}" },
            new { name = "fastboot-device", ok = connected, detail = connected ? (probes.GetValueOrDefault("product") ?? "connected") : "no device in fastboot mode" },
            new { name = "psid-readable", ok = psidReadable, detail = psidReadable ? "oem get-psid answered" : "oem get-psid unavailable/rejected" },
            new { name = "ptable-readable", ok = partitionCount is > 0, detail = partitionCount is > 0 ? $"{partitionCount} partitions" : "getvar ptable unavailable" },
        };

        var payload = new
        {
            tool = new { name = ToolInfo.Name, version = ToolInfo.Version, framework = ToolInfo.Framework, baseDirectory = AppContext.BaseDirectory },
            host = new
            {
                os = Environment.OSVersion.ToString(),
                architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            },
            fastboot = new { found = fastbootPath != null, path = fastbootPath, version = fastbootVersion, devices = fastbootDevices },
            adb = new { found = adbPath != null, path = adbPath, version = adbVersion, devices = adbDevices },
            loaders = new
            {
                directory = BundledLoaders.Directory,
                exists = Directory.Exists(BundledLoaders.Directory),
                chips = loaders.Select(entry => new { chip = entry.Chip, ktlFiles = entry.Files }).ToList(),
            },
            device = new
            {
                connected,
                chip,
                probes = probes.Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value!),
                psidReadable,
                partitionCount,
                dumpCapable = connected && fastbootPath != null,
            },
            checks,
            ok = fastbootPath != null && fastbootVersion != null,
        };

        if (context.Json)
        {
            context.WriteJson(payload);
        }
        else
        {
            context.Output.WriteLine($"{ToolInfo.Name} {ToolInfo.Version} on {payload.host.os} ({payload.host.architecture})");
            context.Output.WriteLine($"fastboot: {fastbootPath ?? "NOT FOUND"}  {(fastbootVersion != null ? "[" + fastbootVersion + "]" : string.Empty)}");
            context.Output.WriteLine($"adb:      {adbPath ?? "not found (optional)"}  {(adbVersion != null ? "[" + adbVersion + "]" : string.Empty)}");
            context.Output.WriteLine($"fastboot devices: {(fastbootDevices.Count == 0 ? "(none)" : string.Join(" | ", fastbootDevices))}");
            context.Output.WriteLine($"adb devices:      {(adbDevices.Count == 0 ? "(none)" : string.Join(" | ", adbDevices))}");
            context.Output.WriteLine($"loaders: {loaders.Count} chips, {loaders.Sum(entry => entry.Files)} .ktl files in {BundledLoaders.Directory}");
            context.Output.WriteLine($"device: {(connected ? "connected" : "not connected")} chip={chip}");
            foreach (var pair in probes.Where(pair => pair.Value != null))
                context.Output.WriteLine($"  {pair.Key}: {pair.Value}");
            if (connected)
            {
                context.Output.WriteLine($"  psid-readable: {psidReadable}");
                context.Output.WriteLine($"  partition-count: {partitionCount?.ToString() ?? "unknown"}");
            }
            context.Output.WriteLine("checks:");
            foreach (var check in checks)
            {
                var type = check.GetType();
                bool ok = (bool)(type.GetProperty("ok")?.GetValue(check) ?? false);
                string detail = (string?)(type.GetProperty("detail")?.GetValue(check) ?? string.Empty) ?? string.Empty;
                string name = (string?)(type.GetProperty("name")?.GetValue(check) ?? string.Empty) ?? string.Empty;
                context.Output.WriteLine($"  [{(ok ? "ok" : "--")}] {name}: {detail}");
            }
        }

        if (fastbootPath == null)
        {
            context.Diag("error: fastboot binary not found; set KIRIN_FASTBOOT or install platform-tools");
            return ExitCodes.FileOrVerify;
        }
        if (fastbootVersion == null)
        {
            context.Diag("error: fastboot is present but does not run; check the binary and its permissions");
            return ExitCodes.FileOrVerify;
        }
        return ExitCodes.Ok;
    }
}

internal sealed class GetVarCommand : ICliCommand
{
    public string Name => "getvar";
    public string McpName => "getvar";
    public string Summary => "Read a single fastboot variable (read-only).";
    public string Usage => "getvar <name> [--json]";
    public bool IsWrite => false;
    public IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("name", "string", "fastboot variable name, e.g. product, version, serialno, nve:...", Required: true, Positional: true),
    };

    public async Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count < 1)
            throw new CliUsageException("getvar requires a variable name");
        string variable = args.Positionals[0];
        if (!IsSafeVariable(variable))
            throw new CliUsageException($"invalid variable name '{variable}'");

        var session = await FastbootSession.TryOpenAsync();
        if (session == null)
        {
            context.Diag("error: fastboot binary not found; set KIRIN_FASTBOOT or install platform-tools");
            return ExitCodes.FileOrVerify;
        }
        if (!session.Connected)
        {
            context.Diag("error: no fastboot device detected");
            return ExitCodes.NoDevice;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ProcessResult result;
        try
        {
            result = await session.Client.QueryVariableAsync(variable);
        }
        catch (TimeoutException)
        {
            context.Diag($"error: getvar {variable} timed out");
            return ExitCodes.DeviceRejected;
        }

        string? value = null;
        try
        {
            value = FastbootReadResponses.Variable(result, variable);
        }
        catch (InvalidOperationException)
        {
            value = null;
        }

        if (value == null)
        {
            int exit = DeviceOutcome.FailureExit(result.Output);
            if (exit == ExitCodes.DeviceRejected)
                context.Diag($"error: device rejected getvar {variable}");
            else
                context.Diag($"error: no unambiguous value for {variable}");
            if (context.Json)
            {
                context.WriteJson(new
                {
                    variable,
                    value = (string?)null,
                    raw = result.Output,
                    exitCode = exit,
                });
            }
            return exit;
        }

        if (context.Json)
        {
            context.WriteJson(new { variable, value, raw = result.Output });
        }
        else
        {
            context.Output.WriteLine($"{variable}: {value}");
        }
        return ExitCodes.Ok;
    }

    private static bool IsSafeVariable(string variable) =>
        variable.Length is > 0 and <= 64 &&
        variable.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or ':' or '.');
}

internal sealed class ExtractCommand : ICliCommand
{
    public string Name => "extract";
    public string McpName => "extract";
    public string Summary => "List and extract partitions from an UPDATE.APP archive (read-only, no device).";
    public string Usage => "extract <UPDATE.APP> [outdir] [--json]";
    public bool IsWrite => false;
    public IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("file", "string", "path to UPDATE.APP", Required: true, Positional: true),
        new CliParam("outdir", "string", "output directory (default: ./<archive>-extracted)", Positional: true),
    };

    public async Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count < 1) throw new CliUsageException("extract requires an UPDATE.APP path");

        string archivePath = Path.GetFullPath(args.Positionals[0], context.WorkingDirectory);
        if (!File.Exists(archivePath))
        {
            context.Diag($"error: file not found: {archivePath}");
            return ExitCodes.FileOrVerify;
        }

        string outputDirectory = args.Positionals.Count > 1
            ? Path.GetFullPath(args.Positionals[1], context.WorkingDirectory)
            : Path.Combine(context.WorkingDirectory, Path.GetFileNameWithoutExtension(archivePath) + "-extracted");

        List<ExtractionEntry> entries;
        try
        {
            entries = PartitionExtractor.Read(archivePath, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or ArgumentException)
        {
            context.Diag($"error: cannot parse UPDATE.APP: {ex.Message}");
            return ExitCodes.FileOrVerify;
        }

        foreach (ExtractionEntry entry in entries) entry.IsSelected = true;

        if (!context.Json)
        {
            context.Output.WriteLine($"archive: {archivePath}");
            context.Output.WriteLine($"output:  {outputDirectory}");
            context.Output.WriteLine(TextFormat.Table(
                entries.Select(entry => new[] { entry.Name, entry.Partition.FormattedSize, entry.FileName }).ToList(),
                "PARTITION", "SIZE", "FILE"));
        }

        var stopwatch = Stopwatch.StartNew();
        long lastReportedBytes = 0;
        int lastIndex = -1;
        int completed;
        try
        {
            completed = await PartitionExtractor.ExtractAsync(archivePath, outputDirectory, entries, cancellationToken, progress =>
            {
                if (progress.State is "Extracted" or "Failed" or "Cancelled")
                {
                    context.Diag($"[{progress.Completed}/{progress.Total}] {progress.Name} {progress.State}");
                    lastReportedBytes = progress.Bytes;
                    return;
                }

                if (progress.Index != lastIndex)
                {
                    lastIndex = progress.Index;
                    lastReportedBytes = progress.Bytes;
                    context.Diag($"[{progress.Completed}/{progress.Total}] {progress.Name} {progress.State} ({TextFormat.Bytes(progress.PartitionSize)})");
                    return;
                }

                if (progress.Bytes - lastReportedBytes >= 64L * 1024 * 1024)
                {
                    lastReportedBytes = progress.Bytes;
                    context.Diag($"[{progress.Completed}/{progress.Total}] {progress.Name} {TextFormat.Bytes(progress.Bytes)}/{TextFormat.Bytes(progress.TotalBytes)}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            context.Diag("error: extraction cancelled");
            return ExitCodes.FileOrVerify;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            context.Diag($"error: extraction failed: {ex.Message}");
            return ExitCodes.FileOrVerify;
        }

        stopwatch.Stop();
        long bytes = entries.Sum(entry => entry.Partition.Size);
        var partitions = entries.Select(entry => new
        {
            name = entry.Name,
            size = entry.Partition.Size,
            formattedSize = entry.Partition.FormattedSize,
            file = Path.Combine(outputDirectory, entry.FileName),
        }).ToList();

        if (context.Json)
        {
            context.WriteJson(new
            {
                archive = archivePath,
                outputDirectory,
                extracted = completed,
                total = entries.Count,
                bytes,
                elapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
                partitions,
            });
        }
        else
        {
            context.Output.WriteLine($"extracted {completed}/{entries.Count} partitions, {TextFormat.Bytes(bytes)} in {stopwatch.Elapsed.TotalSeconds:F1}s");
            foreach (var partition in partitions)
                context.Output.WriteLine($"  {partition.name} -> {partition.file}");
        }

        return completed == entries.Count ? ExitCodes.Ok : ExitCodes.FileOrVerify;
    }
}

internal sealed class DumpCommand : ICliCommand
{
    public string Name => "dump";
    public string McpName => "dump";
    public string Summary => "Dump one device partition to a host file via oem dump-emmc (read-only on the device).";
    public string Usage => "dump <partition> -o <dir> [--json]";
    public bool IsWrite => false;
    public IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("partition", "string", "partition name, e.g. oeminfo, ptable", Required: true, Positional: true),
        new CliParam("output", "string", "output directory for <partition>.img", Default: "."),
    };

    public async Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count < 1) throw new CliUsageException("dump requires a partition name");
        string partition = args.Positionals[0];
        if (!IsSafePartition(partition))
            throw new CliUsageException($"invalid partition name '{partition}'");

        string outputDirectory = Path.GetFullPath(args.Value("output") ?? ".", context.WorkingDirectory);

        var session = await FastbootSession.TryOpenAsync();
        if (session == null)
        {
            context.Diag("error: fastboot binary not found; set KIRIN_FASTBOOT or install platform-tools");
            return ExitCodes.FileOrVerify;
        }
        if (!session.Connected)
        {
            context.Diag("error: no fastboot device detected");
            return ExitCodes.NoDevice;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Diag($"error: cannot create output directory: {ex.Message}");
            return ExitCodes.FileOrVerify;
        }

        string target = Path.Combine(outputDirectory, partition + ".img");
        var service = new FastbootFlasherService(session.Client);
        (bool IsSuccess, string Message) result;
        try
        {
            result = await service.DumpPartitionAsync(partition, target);
        }
        catch (OperationCanceledException)
        {
            context.Diag("error: dump cancelled");
            return ExitCodes.FileOrVerify;
        }

        long size = 0;
        if (File.Exists(target))
        {
            try { size = new FileInfo(target).Length; } catch (Exception) { size = 0; }
        }

        if (result.IsSuccess)
        {
            if (context.Json)
            {
                context.WriteJson(new { partition, output = target, bytes = size, message = result.Message });
            }
            else
            {
                context.Output.WriteLine($"dumped {partition} -> {target} ({TextFormat.Bytes(size)})");
            }
            return ExitCodes.Ok;
        }

        int exit = DeviceOutcome.FailureExit(result.Message);
        context.Diag($"error: dump failed: {result.Message}");
        if (context.Json)
        {
            context.WriteJson(new { partition, output = target, bytes = size, message = result.Message, exitCode = exit });
        }
        return exit;
    }

    private static bool IsSafePartition(string name) =>
        name.Length is > 0 and <= 64 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
}

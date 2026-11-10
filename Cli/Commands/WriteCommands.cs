using System.Text;
using Kirin_Tool.Models;
using Kirin_Tool.Services;

namespace Kirin_Tool.Cli.Commands;

/// <summary>
/// Gate for the second batch: write commands compute and print exactly what would be
/// written, then refuse to touch the device. They require <c>--yes</c> (CLI) or
/// <c>confirm: true</c> (MCP) even to leave "plan" mode, and the actual write path is
/// intentionally not implemented in this build, so they still exit 4.
/// </summary>
internal abstract class GatedWriteCommand : ICliCommand
{
    public abstract string Name { get; }
    public abstract string McpName { get; }
    public abstract string Summary { get; }
    public abstract string Usage { get; }
    public bool IsWrite => true;
    public abstract IReadOnlyList<CliParam> Params { get; }

    protected abstract Task<WritePlan> BuildPlanAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken);

    public async Task<int> ExecuteAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        WritePlan plan;
        try
        {
            plan = await BuildPlanAsync(args, context, cancellationToken);
        }
        catch (CliUsageException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException or ArgumentException or CliFileException)
        {
            context.Diag($"error: {ex.Message}");
            return ExitCodes.FileOrVerify;
        }

        bool confirmed = context.Yes || args.HasFlag("yes");
        string status = confirmed ? "confirmed-but-write-path-not-implemented" : "plan-only";

        var payload = new
        {
            command = Name,
            dryRun = true,
            applied = false,
            confirmed,
            gate = new
            {
                required = "--yes (CLI) or confirm:true (MCP)",
                status,
                reason = "Write paths are gate-only in this build: the plan below was generated from the existing services, but nothing was flashed, erased or written.",
                exitCode = ExitCodes.Gated,
            },
            plan = plan.Details,
            exitCode = ExitCodes.Gated,
        };

        if (context.Json)
        {
            context.WriteJson(payload);
        }
        else
        {
            foreach (string line in plan.Lines) context.Output.WriteLine(line);
            context.Output.WriteLine();
            context.Output.WriteLine($"gate: {status} (exit {ExitCodes.Gated})");
            if (!confirmed)
                context.Output.WriteLine("re-run with --yes (CLI) or confirm:true (MCP) to acknowledge the plan; this build still will not write.");
            else
                context.Output.WriteLine("write path not implemented in this build; no device state was changed.");
        }

        if (!confirmed) context.Diag("gate: plan printed; re-run with --yes to acknowledge");
        else context.Diag("gate: acknowledged, but the write path is not implemented in this build");

        return ExitCodes.Gated;
    }
}

internal sealed class FlashOtaCommand : GatedWriteCommand
{
    public override string Name => "flash-ota";
    public override string McpName => "flash_ota";
    public override string Summary => "Plan flashing every partition of an UPDATE.APP (gate-only: never writes in this build).";
    public override string Usage => "flash-ota <UPDATE.APP> [--yes] [--json]";
    public override IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("file", "string", "path to UPDATE.APP to inspect for the flash plan", Required: true, Positional: true),
    };

    protected override Task<WritePlan> BuildPlanAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count < 1) throw new CliUsageException("flash-ota requires an UPDATE.APP path");
        string archivePath = Path.GetFullPath(args.Positionals[0], context.WorkingDirectory);
        if (!File.Exists(archivePath)) throw new CliFileException($"file not found: {archivePath}");

        List<ExtractionEntry> entries = PartitionExtractor.Read(archivePath, cancellationToken);
        long totalBytes = entries.Sum(entry => entry.Partition.Size);

        var partitions = entries.Select(entry => new
        {
            partition = entry.Name,
            size = entry.Partition.Size,
            formattedSize = entry.Partition.FormattedSize,
            image = Path.Combine(Path.Combine(Path.GetTempPath(), "kirintool-flash", Path.GetFileNameWithoutExtension(archivePath)), entry.FileName),
        }).ToList();

        var details = new
        {
            archive = archivePath,
            implementation = "Services/FastbootFlasherService.FlashPartitionAsync (not invoked in this build)",
            partitionCount = partitions.Count,
            totalBytes,
            partitions,
        };

        var lines = new List<string>
        {
            $"flash plan for {archivePath}",
            $"would write {partitions.Count} partitions ({TextFormat.Bytes(totalBytes)}) via fastboot flash:",
            TextFormat.Table(partitions.Select(p => new[] { p.partition, p.formattedSize }).ToList(), "PARTITION", "SIZE"),
        };

        return Task.FromResult(new WritePlan(details, lines));
    }
}

internal sealed class FlashXmlCommand : GatedWriteCommand
{
    public override string Name => "flash-xml";
    public override string McpName => "flash_xml";
    public override string Summary => "Plan flashing from a Huawei flashing XML (gate-only: never writes in this build).";
    public override string Usage => "flash-xml <flashing.xml|dir> [--yes] [--json]";
    public override IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("path", "string", "flashing XML file, or a directory containing one", Required: true, Positional: true),
    };

    protected override Task<WritePlan> BuildPlanAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count < 1) throw new CliUsageException("flash-xml requires a flashing XML path");
        string path = Path.GetFullPath(args.Positionals[0], context.WorkingDirectory);

        if (Directory.Exists(path))
        {
            var candidates = Directory.GetFiles(path, "*.xml", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count == 0) throw new CliFileException($"no *.xml flashing file found in {path}");
            if (candidates.Count > 1)
            {
                string? preferred = candidates.FirstOrDefault(file =>
                    Path.GetFileName(file).Contains("flash", StringComparison.OrdinalIgnoreCase));
                path = preferred ?? candidates[0];
            }
            else
            {
                path = candidates[0];
            }
        }

        if (!File.Exists(path)) throw new CliFileException($"file not found: {path}");

        string? fastbootPath = BinaryLocator.FindFastboot();
        var service = new FastbootFlasherService(new FastbootClient(fastbootPath ?? "fastboot"));
        List<FastbootPartition> partitions = service.ParseFlashingXml(path);
        if (partitions.Count == 0)
            throw new CliFileException($"no flashable <image> entries parsed from {path}");

        var images = partitions.Select(partition => new
        {
            partition = partition.Identifier,
            name = partition.Name,
            image = partition.DumpPath,
            exists = partition.DumpPath != null && File.Exists(partition.DumpPath),
        }).ToList();

        var details = new
        {
            xml = path,
            implementation = "Services/FastbootFlasherService.ParseFlashingXml + FlashPartitionAsync (not invoked in this build)",
            imageCount = images.Count,
            missingImages = images.Count(image => !image.exists),
            images,
        };

        var lines = new List<string>
        {
            $"flash plan for {path}",
            $"would write {images.Count} images:",
            TextFormat.Table(images.Select(i => new[]
            {
                i.partition, i.name, i.exists ? "present" : "MISSING", i.image ?? "(unresolved)",
            }).ToList(), "PARTITION", "NAME", "STATE", "PATH"),
        };
        if (images.Any(image => !image.exists))
            lines.Add("warning: some images are missing; a real flash would fail or be partial.");

        return Task.FromResult(new WritePlan(details, lines));
    }
}

internal sealed class VcomUnlockCommand : GatedWriteCommand
{
    public override string Name => "vcom-unlock";
    public override string McpName => "vcom_unlock";
    public override string Summary => "Plan a VCOM/BootROM loader unlock for a chip (gate-only: never uploads in this build).";
    public override string Usage => "vcom-unlock --cpu <chip> [--yes] [--json]";
    public override IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("cpu", "string", "chipset loader family, e.g. hisi980, hisi990", Required: true, Enum: BundledLoaders.SupportedChips().ToList()),
    };

    protected override Task<WritePlan> BuildPlanAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        string? cpu = args.Value("cpu");
        if (string.IsNullOrWhiteSpace(cpu))
            throw new CliUsageException("vcom-unlock requires --cpu <chip> (supported: " + string.Join(", ", BundledLoaders.SupportedChips()) + ")");

        cpu = cpu.Trim().ToLowerInvariant();
        if (!FirmwareUnlocker.CpuAddresses.ContainsKey(cpu))
            throw new CliUsageException($"unsupported chip '{cpu}' (supported: {string.Join(", ", BundledLoaders.SupportedChips())})");

        var stages = BundledLoaders.Plan(cpu);
        string loaderDirectory = Path.Combine(BundledLoaders.Directory, cpu);

        var stageList = stages.Select(stage => new
        {
            stage = stage.Stage,
            file = stage.File,
            path = Path.Combine(loaderDirectory, stage.File),
            address = "0x" + stage.Address.ToString("X8"),
            pwn = stage.PwnFlag,
            exists = stage.Exists,
        }).ToList();

        var details = new
        {
            cpu,
            loaderDirectory,
            implementation = "Services/FirmwareUnlocker.UnlockFastboot + Services/VcomFlasher (not invoked in this build)",
            requiresVcomMode = "HUAWEI USB COM 1.0 (12d1:3609)",
            stageCount = stageList.Count,
            missingStages = stageList.Count(stage => !stage.exists),
            stages = stageList,
        };

        var lines = new List<string>
        {
            $"vcom unlock plan for {cpu}",
            $"loader directory: {loaderDirectory}",
            "would upload, in order (device must already be in VCOM mode):",
            TextFormat.Table(stageList.Select(stage => new[]
            {
                stage.stage, stage.address, stage.file, stage.exists ? "present" : "MISSING",
            }).ToList(), "STAGE", "ADDRESS", "LOADER", "FILE"),
        };
        if (stageList.Any(stage => !stage.exists))
            lines.Add("warning: missing loader files would be skipped by the current service.");

        return Task.FromResult(new WritePlan(details, lines));
    }
}

internal sealed class OemInfoCommand : GatedWriteCommand
{
    public override string Name => "oeminfo";
    public override string McpName => "oeminfo";
    public override string Summary => "Plan an OEMINFO model/region rewrite from a file or a device dump (gate-only: never flashes).";
    public override string Usage => "oeminfo --model <model> --region <vendor-country> (--input <oeminfo.img> | --pull) [--yes] [--json]";
    public override IReadOnlyList<CliParam> Params => new[]
    {
        new CliParam("model", "string", "new model string written into the OEMINFO model entry", Required: true),
        new CliParam("region", "string", "new vendor/country string written into the region entry", Required: true),
        new CliParam("input", "string", "local oeminfo.img to convert"),
        new CliParam("pull", "boolean", "dump oeminfo from the connected device instead of --input"),
    };

    protected override async Task<WritePlan> BuildPlanAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        string? model = args.Value("model");
        string? region = args.Value("region");
        if (string.IsNullOrWhiteSpace(model)) throw new CliUsageException("oeminfo requires --model <model>");
        if (string.IsNullOrWhiteSpace(region)) throw new CliUsageException("oeminfo requires --region <vendor-country>");

        bool pull = args.HasFlag("pull");
        string? input = args.Value("input");
        if (pull && input != null) throw new CliUsageException("oeminfo accepts either --pull or --input, not both");
        if (!pull && input == null) throw new CliUsageException("oeminfo requires --input <oeminfo.img> or --pull");

        string? inputPath = null;
        int? detectedVersion = null;
        object? conversion = null;

        if (!pull)
        {
            inputPath = Path.GetFullPath(input!, context.WorkingDirectory);
            if (!File.Exists(inputPath)) throw new CliFileException($"file not found: {inputPath}");
            detectedVersion = TryDetectOemInfoVersion(inputPath);

            var service = new OemInfoService(new FastbootClient(BinaryLocator.FindFastboot() ?? "fastboot"));
            cancellationToken.ThrowIfCancellationRequested();
            OemInfoConversionResult result = await service.ConvertOemInfo(false, inputPath, model!, region!);
            service.CleanupTempFiles();
            conversion = new
            {
                success = result.IsSuccess,
                error = result.ErrorMessage,
                outputBytes = result.ConvertedData?.Length,
                pulledFromDevice = result.PulledFromDevice,
            };
            if (!result.IsSuccess)
                throw new CliFileException($"OEMINFO conversion failed: {result.ErrorMessage}");
        }

        var details = new
        {
            mode = pull ? "device-pull" : "file",
            input = inputPath,
            model,
            region,
            target = "oeminfo",
            oemInfoVersion = detectedVersion,
            conversion,
            implementation = "Services/OemInfoService.ConvertOemInfo + FastbootClient.FlashOemInfo (flash not invoked in this build)",
        };

        var lines = new List<string>
        {
            $"oeminfo plan ({(pull ? "pull from device" : inputPath)})",
            $"model:  {model}",
            $"region: {region}",
            $"target partition: oeminfo",
        };
        if (detectedVersion != null) lines.Add($"detected OEMINFO version: {detectedVersion}");
        if (!pull) lines.Add("conversion: succeeded locally (in-memory, temp files cleaned)");
        else lines.Add("conversion: would dump oeminfo from the device first");

        return new WritePlan(details, lines);
    }

    private static int? TryDetectOemInfoVersion(string path)
    {
        try
        {
            byte[] data = File.ReadAllBytes(path);
            byte[] magic = Encoding.ASCII.GetBytes("OEM_INFO");
            for (int offset = 0; offset + 12 <= data.Length; offset += 0x400)
            {
                if (data.AsSpan(offset, magic.Length).SequenceEqual(magic))
                    return BitConverter.ToInt32(data, offset + 8);
            }
        }
        catch (Exception)
        {
            // best effort only
        }
        return null;
    }
}

internal sealed class DowngradeEnableCommand : GatedWriteCommand
{
    public override string Name => "downgrade-enable";
    public override string McpName => "downgrade_enable";
    public override string Summary => "Plan the OEMINFO version erase that enables downgrade (gate-only: never erases in this build).";
    public override string Usage => "downgrade-enable [--yes] [--json]";
    public override IReadOnlyList<CliParam> Params => Array.Empty<CliParam>();

    protected override Task<WritePlan> BuildPlanAsync(ParsedArgs args, CliContext context, CancellationToken cancellationToken)
    {
        string[] commands =
        {
            "oem oeminfoerase-amssver",
            "oem oeminfoerase-basever",
            "oem oeminfoerase-custver",
            "oem oeminfoerase-preloadver",
        };

        var steps = commands.Select((command, index) => new { step = index + 1, command }).ToList();
        var details = new
        {
            implementation = "Services/FastbootClient.EnableDowngradeWithSteps (not invoked in this build)",
            target = "oeminfo version fields",
            steps,
        };

        var lines = new List<string>
        {
            "downgrade-enable plan",
            "would run these fastboot commands in order (irreversible version-field erase):",
            TextFormat.Table(steps.Select(step => new[] { step.step.ToString(), step.command }).ToList(), "STEP", "COMMAND"),
        };

        return Task.FromResult(new WritePlan(details, lines));
    }
}

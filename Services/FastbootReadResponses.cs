using Kirin_Tool.Models;
using System.Text.RegularExpressions;

namespace Kirin_Tool.Services;

// Read-only response parsing. Never infer partition names from firmware files or
// substitute the USB serial number for the NVE serial-number field.
public static class FastbootReadResponses
{
    private static IEnumerable<string> Lines(string output) => output
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim());

    private static string WithoutPrefix(string line) =>
        line.StartsWith("(bootloader)", StringComparison.Ordinal)
            ? line[12..].TrimStart() : line;

    private static void EnsureSuccess(ProcessResult result, string variable)
    {
        if (!result.IsSuccess || Lines(result.Output).Any(line =>
            line.Contains("FAILED", StringComparison.OrdinalIgnoreCase)))
        {
            string reason = result.Output.Contains("invalid command", StringComparison.OrdinalIgnoreCase)
                ? "The connected fastboot rejects this command (invalid command). Its loaded firmware may not support this feature."
                : "The fastboot query failed. Check the connection and the fastboot log for details.";
            throw new InvalidOperationException($"Cannot read {variable}. {reason}");
        }
    }

    public static string OemVersion(string output)
    {
        if (Lines(output).Any(line => line.Contains("FAILED", StringComparison.OrdinalIgnoreCase))) return "Unknown";
        foreach (var line in Lines(output).Where(line => line.StartsWith("(bootloader)", StringComparison.Ordinal)))
        {
            var value = WithoutPrefix(line).Trim().TrimStart(':').Trim();
            if (value.Length > 0 && !value.StartsWith("Error", StringComparison.OrdinalIgnoreCase)) return value;
        }
        return "Unknown";
    }

    public static IReadOnlyDictionary<string, string> Psid(ProcessResult result)
    {
        EnsureSuccess(result, "get-psid");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Lines(result.Output).Select(WithoutPrefix))
        {
            var match = Regex.Match(line, @"^(SN|IMEI|IMEI1|MEID|PSID)\s*:\s*(\S+)\s*$", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            string key = match.Groups[1].Value.ToUpperInvariant(), value = match.Groups[2].Value;
            if (values.TryGetValue(key, out string? existing) && existing != value)
                throw new InvalidOperationException("Conflicting identifiers in get-psid response.");
            values[key] = value;
        }
        if (values.Count == 0) throw new InvalidOperationException("The phone returned no recognised identifiers for get-psid.");
        return values;
    }

    public static string Variable(ProcessResult result, string variable)
    {
        EnsureSuccess(result, variable);
        string prefix = variable + ":";
        var values = Lines(result.Output).Select(WithoutPrefix)
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..].Trim())
            .Where(value => value.Length > 0).Distinct().ToArray();
        if (values.Length != 1)
            throw new InvalidOperationException($"Fastboot returned no unambiguous value for {variable}.");
        return values[0];
    }

    public static List<string> Partitions(ProcessResult result)
    {
        EnsureSuccess(result, "ptable");
        // Frozen Windows format: INFO ':partition'. The bundled Linux fastboot
        // prints INFO with this same prefix (fastboot-src/src/protocol.c).
        var names = new List<string>();
        foreach (string line in Lines(result.Output))
        {
            if (!line.StartsWith("(bootloader)", StringComparison.Ordinal)) continue;
            string payload = WithoutPrefix(line);
            if (!payload.StartsWith(':')) continue;
            string name = payload[1..].Trim();
            if (!Regex.IsMatch(name, @"\A[A-Za-z0-9_][A-Za-z0-9_.-]*\z"))
                throw new InvalidOperationException("Fastboot returned an unrecognized partition-table entry.");
            if (!names.Contains(name)) names.Add(name);
        }
        if (names.Count == 0)
            throw new InvalidOperationException("Fastboot returned no recognized partition-table entries. This does not mean the device has no partitions.");
        return names;
    }
}

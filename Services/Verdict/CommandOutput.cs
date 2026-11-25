namespace Kirin_Tool.Services.Verdict;

/// <summary>What one loader reply actually said. Unknown is the default, never a success.</summary>
public enum CommandOutputVerdict
{
    Succeeded,
    Failed,
    Unknown,
}

/// <summary>
/// Token-level classifier for fastboot/loader replies (debt D-10: audit F12/P27/S24).
///
/// Why this exists instead of the old <c>Contains("OKAY")</c>: a substring match turns
/// <c>"FAILED (remote: 'not support')"</c> plus a trailing banner into a success, and it also
/// accepts <c>OKAY</c> that arrived on <b>stderr</b> (ProcessRunner used to hand the GUI
/// <c>stdout + stderr</c> as one string). The rules below are deliberately asymmetric:
///
///  * failure tokens are looked for in <b>both</b> streams, anchored at line start or as an
///    explicit phrase, and failure always wins;
///  * success tokens are only honoured in <b>stdout</b>, as a line-leading token
///    (<c>OKAY [  0.005s]</c>, <c>SUCCESS</c>);
///  * anything else is <see cref="CommandOutputVerdict.Unknown"/>.
///
/// The many fastboot lines that are preambles (<c>&lt; waiting for any device &gt;</c> on
/// stderr, <c>(bootloader) product: ...</c> on stdout) therefore can neither create nor
/// destroy a verdict.
/// </summary>
public static class CommandOutput
{
    private static readonly string[] FailureLinePrefixes =
    {
        "FAILED", "FAIL", "ERROR", "FAILURE", "CANNOT", "NOT ALLOWED", "NOT SUPPORT",
    };

    private static readonly string[] SuccessLinePrefixes = { "OKAY", "SUCCESS" };

    private static readonly string[] FailurePhrases =
    {
        "NOT ALLOWED", "NOT SUPPORTED", "NOT SUPPORT", "COMMAND FAILED", "INVALID COMMAND",
        // standard fastboot error shape: "fastboot: error: Command failed"
        ": ERROR:", "ERROR:", "FAILED (",
    };

    /// <summary>Classify a reply when only one merged stream is available (legacy call sites).</summary>
    public static CommandOutputVerdict Classify(string? merged) => Classify(merged, null);

    public static CommandOutputVerdict Classify(string? stdout, string? stderr)
    {
        if (HasFailure(stdout) || HasFailure(stderr)) return CommandOutputVerdict.Failed;
        if (HasSuccess(stdout)) return CommandOutputVerdict.Succeeded;
        return CommandOutputVerdict.Unknown;
    }

    /// <summary>Convenience for a <c>ProcessResult</c>: stdout-only success, both-stream failure.</summary>
    public static CommandOutputVerdict Classify(Kirin_Tool.Models.ProcessResult? result)
    {
        if (result == null) return CommandOutputVerdict.Unknown;
        // Older call paths do not populate StandardOutput; fall back to the merged text so a
        // success can still be seen (the merged stream keeps stdout first).
        string? stdout = string.IsNullOrEmpty(result.StandardOutput) ? result.Output : result.StandardOutput;
        return Classify(stdout, result.ErrorOutput);
    }

    /// <summary>True when the stream contains an explicit failure token.</summary>
    public static bool HasFailure(string? stream)
    {
        foreach (string line in Lines(stream))
        {
            string upper = line.ToUpperInvariant();
            foreach (string prefix in FailureLinePrefixes)
            {
                if (upper.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }

            foreach (string phrase in FailurePhrases)
            {
                if (upper.Contains(phrase, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    /// <summary>True when stdout carries a line-leading success token.</summary>
    public static bool HasSuccess(string? stream)
    {
        foreach (string line in Lines(stream))
        {
            string upper = line.ToUpperInvariant();
            foreach (string prefix in SuccessLinePrefixes)
            {
                if (upper.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Lines(string? stream)
    {
        if (string.IsNullOrWhiteSpace(stream)) yield break;
        foreach (string raw in stream!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length > 0) yield return line;
        }
    }
}

// Licensed under the repository's Business Source License 1.1. See LICENSE.
using System.Text.RegularExpressions;

namespace Kirin_Tool.Services;

public record UdevRuleCoverage(bool Usb, bool Serial, bool ReadError)
{
    public bool Complete => Usb && Serial && !ReadError;
}

/// <summary>Read-only check of common vendor-wide Huawei desktop permission rules.
/// This does not run udevadm, access USB, or assert the current device's permissions.</summary>
public static class UdevRuleNotice
{
    public static bool IsAppImage => OperatingSystem.IsLinux() &&
        Environment.GetEnvironmentVariable("KIRIN_APPIMAGE") == "1";
    public static readonly string[] RuleDirectories =
        ["/etc/udev/rules.d", "/run/udev/rules.d", "/usr/local/lib/udev/rules.d", "/usr/lib/udev/rules.d", "/lib/udev/rules.d"];
    // uaccess does not depend on distro-specific adbusers/uucp group names.
    public const string DesktopRules = """
        ACTION=="add", SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", ATTR{idVendor}=="12d1", MODE="0660", TAG+="uaccess"
        ACTION=="add", SUBSYSTEM=="tty", SUBSYSTEMS=="usb", ATTRS{idVendor}=="12d1", MODE="0660", TAG+="uaccess"
        """;
    public static string ApplyCommands =>
        "sudo install -d -m 0755 /etc/udev/rules.d\n" +
        "sudo tee /etc/udev/rules.d/51-kirin-tool-fastboot.rules >/dev/null <<'KIRIN_UDEV_RULES'\n" +
        DesktopRules + "\nKIRIN_UDEV_RULES\nsudo udevadm control --reload-rules";
    private static readonly Regex Assignments = new(
        "(?<key>[A-Za-z_]+(?:\\{[^}]+\\})?)\\s*(?<op>==|!=|\\+=|:=|=|-=)\\s*\"(?<value>[^\"]*)\"",
        RegexOptions.CultureInvariant);

    public static UdevRuleCoverage Inspect(IEnumerable<string>? directories = null)
    {
        bool usb = false, serial = false, error = false;
        var effective = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string directory in directories ?? RuleDirectories)
        {
            try
            {
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.EnumerateFiles(directory, "*.rules"))
                    effective.TryAdd(Path.GetFileName(file), file); // Earlier directories override by filename.
            }
            catch (IOException) { error = true; }
            catch (UnauthorizedAccessException) { error = true; }
        }
        foreach (var file in effective.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            try
            {
                var info = new FileInfo(file.Value);
                if (info.LinkTarget != null && info.ResolveLinkTarget(true)?.FullName == "/dev/null") continue;
                foreach (string line in File.ReadAllText(file.Value).Replace("\\\r\n", "").Replace("\\\n", "").Split('\n'))
                {
                    string rule = StripComment(line);
                    var terms = Assignments.Matches(rule).Select(m =>
                        (Key: m.Groups["key"].Value, Op: m.Groups["op"].Value, Value: m.Groups["value"].Value)).ToArray();
                    foreach (string subsystem in new[] { "usb", "tty" })
                    {
                        bool vendor = terms.Any(t => t.Op == "==" && (t.Key == "ATTR{idVendor}" || t.Key == "ATTRS{idVendor}") && t.Value == "12d1");
                        bool target = terms.Any(t => t.Key == "SUBSYSTEM" && t.Op == "==" && t.Value == subsystem);
                        bool uaccess = false, worldReadWrite = false, tagFinal = false, modeFinal = false;
                        foreach (var term in terms)
                        {
                            if (term.Key == "TAG" && !tagFinal)
                            {
                                if (term.Op == "=" || term.Op == ":=") uaccess = term.Value == "uaccess";
                                else if (term.Value == "uaccess" && term.Op == "+=") uaccess = true;
                                else if (term.Value == "uaccess" && term.Op == "-=") uaccess = false;
                                tagFinal = term.Op == ":=";
                            }
                            if (term.Key == "MODE" && !modeFinal && (term.Op == "=" || term.Op == ":="))
                            {
                                worldReadWrite = Regex.IsMatch(term.Value, "^[0-7]{3,4}$") && (Convert.ToInt32(term.Value, 8) & 6) == 6;
                                modeFinal = term.Op == ":=";
                            }
                        }
                        bool accessible = uaccess || worldReadWrite;
                        // Product/interface/kernel selectors cannot establish coverage for all modes.
                        bool restricted = terms.Where(t => t.Op == "==" || t.Op == "!=").Any(t =>
                            t.Op == "!=" || !(t.Key == "SUBSYSTEM" || t.Key == "ATTR{idVendor}" || t.Key == "ATTRS{idVendor}" ||
                            (t.Key == "ACTION" && t.Value.Split('|').Contains("add")) ||
                            (t.Key == "ENV{DEVTYPE}" && subsystem == "usb" && t.Value == "usb_device") ||
                            (t.Key == "SUBSYSTEMS" && subsystem == "tty" && t.Value == "usb")));
                        if (vendor && target && accessible && !restricted)
                        {
                            if (subsystem == "usb") usb = true; else serial = true;
                        }
                    }
                }
            }
            catch (IOException) { error = true; }
            catch (UnauthorizedAccessException) { error = true; }
        }
        return new(usb, serial, error);
    }
    private static string StripComment(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            if (line[i] == '#' && !quoted) return line[..i];
        }
        return line;
    }
}

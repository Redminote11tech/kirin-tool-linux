// Beta track (Kirin Tool for Linux): read-only Linux environment diagnostics.
// Detects conditions that commonly break fastboot/VCOM operations and returns
// human-readable warnings. Never modifies the system — guidance only.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kirin_Tool.Utils
{
    public static class LinuxEnvironmentCheck
    {
        public static bool IsLinux => OperatingSystem.IsLinux();

        /// <summary>
        /// Returns warnings for environment conditions known to break device
        /// operations on Linux (empty list = nothing to report).
        /// </summary>
        public static List<string> GetIssues()
        {
            var issues = new List<string>();
            if (!IsLinux)
            {
                return issues;
            }

            // 1. udev rules (fastboot USB access + VCOM serial access)
            bool rulesInstalled =
                File.Exists("/usr/lib/udev/rules.d/51-kirin-tool-fastboot.rules") ||
                File.Exists("/etc/udev/rules.d/51-kirin-tool-fastboot.rules");
            if (!rulesInstalled)
            {
                issues.Add(
                    "The Kirin Tool udev rules are not installed. Without them fastboot/VCOM access " +
                    "fails with 'no permissions'.\nFix (pacman installs them automatically):\n" +
                    "  sudo cp packaging/51-kirin-tool-fastboot.rules /etc/udev/rules.d/\n" +
                    "  sudo udevadm control --reload-rules && sudo udevadm trigger");
            }

            // 2. group membership (adbusers = fastboot nodes, uucp = VCOM serial)
            var missingGroups = GetMissingGroups("adbusers", "uucp");
            if (missingGroups.Count > 0)
            {
                issues.Add(
                    $"You are not in the following group(s): {string.Join(", ", missingGroups)}.\n" +
                    "fastboot USB nodes are gated by 'adbusers', VCOM/DBAdapter serial nodes by 'uucp'.\n" +
                    "Fix:\n" +
                    $"  sudo usermod -aG {string.Join(",", missingGroups)} $USER\n" +
                    "  (log out and back in afterwards)");
            }

            // 3. usbfs buffer limit (default 16 MB throttles/stalls large fastboot transfers)
            try
            {
                var path = "/sys/kernel/usbfs_memory_mb";
                if (File.Exists(path) &&
                    long.TryParse(File.ReadAllText(path).Trim(), out var mb) &&
                    mb <= 16)
                {
                    issues.Add(
                        $"usbfs_memory_mb is {mb} (the kernel default). Large fastboot transfers " +
                        "(super images, full OTA) can stall or run very slowly.\n" +
                        "Fix (until reboot):\n" +
                        "  echo 0 | sudo tee /sys/module/usbcore/parameters/usbfs_memory_mb\n" +
                        "Fix (persistent): add 'usbcore.usbfs_memory_mb=0' to your kernel command line.");
                }
            }
            catch
            {
                // sysfs not available (non-Linux or restricted container) — not an issue worth reporting
            }

            return issues;
        }

        private static List<string> GetMissingGroups(params string[] requiredGroups)
        {
            var missing = new List<string>();
            try
            {
                var myGids = ReadSupplementaryGids();
                foreach (var group in requiredGroups)
                {
                    var gid = FindGroupGid(group);
                    if (gid.HasValue && !myGids.Contains(gid.Value))
                    {
                        missing.Add(group);
                    }
                }
            }
            catch
            {
                // /etc/group or /proc not readable in this environment — stay silent
            }
            return missing;
        }

        private static HashSet<long> ReadSupplementaryGids()
        {
            var result = new HashSet<long>();
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                const string prefix = "Groups:";
                if (line.StartsWith(prefix))
                {
                    foreach (var token in line.Substring(prefix.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (long.TryParse(token, out var gid))
                        {
                            result.Add(gid);
                        }
                    }
                    break;
                }
            }
            return result;
        }

        private static long? FindGroupGid(string groupName)
        {
            foreach (var line in File.ReadLines("/etc/group"))
            {
                var fields = line.Split(':');
                if (fields.Length >= 3 && fields[0] == groupName && long.TryParse(fields[2], out var gid))
                {
                    return gid;
                }
            }
            return null;
        }
    }
}

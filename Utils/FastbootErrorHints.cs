// Beta track (Kirin Tool for Linux): translate common fastboot failure
// strings into human-actionable hints. Purely additive — the original
// output is always shown alongside the hint.

using System;

namespace Kirin_Tool.Utils
{
    public static class FastbootErrorHints
    {
        /// <summary>
        /// Returns a short human explanation for a known failure pattern in a
        /// fastboot output, or null when nothing is recognized.
        /// </summary>
        public static string Explain(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var o = output.ToLowerInvariant();

            if (o.Contains("command not allowed"))
            {
                return "The bootloader refused this command. On modern Huawei bootloaders " +
                       "(e.g. Kirin 9000 generation) the stock fastboot whitelists only a few " +
                       "identification queries while the device is locked — flashing, dumping " +
                       "and most oem commands are blocked until an unlocked fastboot is loaded.";
            }

            if (o.Contains("no permission"))
            {
                return "The device's factory permission gate rejected the command. Sensitive " +
                       "factory operations on modern Huawei devices require cryptographic " +
                       "factory authorization; this is not something the tool can bypass.";
            }

            if (o.Contains("too large") || o.Contains("not enough space") || o.Contains("size too large"))
            {
                return "The bootloader reported the image or transfer as too large. Check that " +
                       "the image matches the target partition; for merged super images make sure " +
                       "the merge completed fully.";
            }

            if (o.Contains("timed out") || o.Contains("timeout"))
            {
                return "The operation timed out. Large transfers can stall behind the kernel's " +
                       "usbfs buffer limit (usbfs_memory_mb, default 16 MB) — raising it helps. " +
                       "Also try a different USB port (prefer USB 2.0) and replug the device.";
            }

            if (o.Contains("no devices") || o.Contains("no such device") || o.Contains("waiting for device"))
            {
                return "The device was not visible to fastboot. Check the USB cable/port, the " +
                       "udev rules and your group memberships (adbusers), and confirm the device " +
                       "is in fastboot mode.";
            }

            if (o.Contains("partition") && (o.Contains("not exist") || o.Contains("no such") || o.Contains("not found")))
            {
                return "The bootloader does not expose a partition with this name on this device. " +
                       "Partition layouts differ between models and firmware versions.";
            }

            if (o.Contains("secure boot") || o.Contains("verity") || o.Contains("anti-rollback"))
            {
                return "The bootloader refused the operation for security/rollback reasons. " +
                       "Anti-rollback (ARB) increments are permanent — older firmware may no " +
                       "longer be installable.";
            }

            return null;
        }

        /// <summary>
        /// Appends the hint (if any) to an error message block.
        /// </summary>
        public static string AppendHint(string message, string output)
        {
            var hint = Explain(output);
            return string.IsNullOrEmpty(hint) ? message : $"{message}\n\nHint: {hint}";
        }
    }
}

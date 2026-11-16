// Licensed under the repository's Business Source License 1.1. See LICENSE.
namespace Kirin_Tool.Services;

public static class ChipCapabilities
{
    public const string Kirin9000 = "hisi9000";
    public const string UnsupportedNotice = "Unsupported for kirin 9000";
    public const string UnknownChipNotice = "Unknown chipset: no capability is granted";

    /// <summary>
    /// Debt D-17 / audit H45: the matrix used to be "anything that is not kirin9000 may do
    /// everything", so a typo (or a chip the loader table does not know) silently unlocked every
    /// write path. The allow-list is now explicit and shares its single source of truth with the
    /// BootROM loader table (<see cref="FirmwareUnlocker.CpuAddresses"/>) - a chip with no loader
    /// entry cannot have a working write path anyway.
    /// </summary>
    public static bool IsKnownChip(string? chip)
    {
        if (string.IsNullOrWhiteSpace(chip)) return false;
        string name = chip!.Trim();
        if (string.Equals(name, Kirin9000, System.StringComparison.OrdinalIgnoreCase)) return true;
        foreach (string known in FirmwareUnlocker.CpuAddresses.Keys)
        {
            if (string.Equals(name, known, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public static bool Allows(string chip, string operation)
    {
        // Unknown chip: nothing is granted (previously: everything was).
        if (!IsKnownChip(chip)) return false;

        return chip != Kirin9000 ||
            operation is "read-info" or "read-sn" or "read-psid" or "reboot-usb-update";
    }

    /// <summary>The notice text a surface should show for the given chip (null when the chip is known).</summary>
    public static string? NoticeFor(string? chip) => IsKnownChip(chip) ? null : UnknownChipNotice;
}

public record RescueImageTarget(string Image, string Target);
public static class RescuePartitionPlan
{
    public static IReadOnlyList<RescueImageTarget> ForChip(string chip) => chip == ChipCapabilities.Kirin9000
        ? [new("ERECOVERY", "rescue_recovery"), new("ERECOVERY_RAMDISK", "rescue_recovery_ramdisk"), new("ERECOVERY_VENDOR", "rescue_recovery_vendor")]
        : [new("ERECOVERY_KERNEL", "rescue_recovery_kernel"), new("ERECOVERY_RAMDISK", "rescue_recovery_ramdisk"), new("ERECOVERY_VENDOR", "rescue_recovery_vendor")];
}

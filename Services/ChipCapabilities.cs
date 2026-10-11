// Licensed under the repository's Business Source License 1.1. See LICENSE.
namespace Kirin_Tool.Services;

public static class ChipCapabilities
{
    public const string Kirin9000 = "hisi9000";
    public const string UnsupportedNotice = "Unsupported for kirin 9000";
    public static bool Allows(string chip, string operation) => chip != Kirin9000 ||
        operation is "read-info" or "read-sn" or "read-psid" or "reboot-usb-update";
}

public record RescueImageTarget(string Image, string Target);
public static class RescuePartitionPlan
{
    public static IReadOnlyList<RescueImageTarget> ForChip(string chip) => chip == ChipCapabilities.Kirin9000
        ? [new("ERECOVERY", "rescue_recovery"), new("ERECOVERY_RAMDISK", "rescue_recovery_ramdisk"), new("ERECOVERY_VENDOR", "rescue_recovery_vendor")]
        : [new("ERECOVERY_KERNEL", "rescue_recovery_kernel"), new("ERECOVERY_RAMDISK", "rescue_recovery_ramdisk"), new("ERECOVERY_VENDOR", "rescue_recovery_vendor")];
}

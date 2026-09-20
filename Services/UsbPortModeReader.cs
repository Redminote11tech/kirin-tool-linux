// Beta track (Kirin Tool for Linux): read-only USB port-mode reader over ADB.
//
// Research background (docs/usb-port-manufacture-mode.md §8): the USB port
// mode on EMUI/HarmonyOS devices is the Android USB config property pair,
// managed by the vendor usb_port daemon:
//   sys.usb.config            — active function list (e.g. "manufacture,adb")
//   persist.sys.usb.config    — persisted default (e.g. "hisuite,mtp,mass_storage,adb")
// This reader displays both. It never writes: switching modes programmatically
// requires root (init property bridge) or Huawei factory authorization.

using Kirin_Tool.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Kirin_Tool.Services
{
    public class UsbPortModeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public static class UsbPortModeReader
    {
        private const int TimeoutSeconds = 10;

        public static async Task<UsbPortModeResult> ReadAsync()
        {
            if (!FileExistsOnPath("adb"))
            {
                return new UsbPortModeResult
                {
                    Success = false,
                    Message = "adb was not found on PATH.\nInstall 'android-tools' (it also provides the fallback fastboot) and make sure the device is connected and authorized."
                };
            }

            try
            {
                var devices = await ProcessRunner.RunAsync("adb", "devices", timeoutSeconds: TimeoutSeconds);
                if (!devices.Output.Contains("\tdevice"))
                {
                    return new UsbPortModeResult
                    {
                        Success = false,
                        Message = "No authorized ADB device found.\nConnect the device, unlock the screen and accept the debugging prompt if asked.\n\nadb devices output:\n" + devices.Output.Trim()
                    };
                }

                var active = await ProcessRunner.RunAsync("adb", "shell getprop sys.usb.config", timeoutSeconds: TimeoutSeconds);
                var persisted = await ProcessRunner.RunAsync("adb", "shell getprop persist.sys.usb.config", timeoutSeconds: TimeoutSeconds);

                string activeValue = CleanPropOutput(active.Output);
                string persistedValue = CleanPropOutput(persisted.Output);

                if (string.IsNullOrEmpty(activeValue) && string.IsNullOrEmpty(persistedValue))
                {
                    return new UsbPortModeResult
                    {
                        Success = false,
                        Message = "The device did not report its USB config properties.\nThis may not be an EMUI/HarmonyOS device, or the shell is restricted."
                    };
                }

                string message =
                    $"Active USB config (sys.usb.config):\n  {activeValue}\n\n" +
                    $"Persisted default (persist.sys.usb.config):\n  {persistedValue}\n\n" +
                    ModeExplanation(activeValue) +
                    "\nNote: the port mode is switched on the device via ProjectMenu → " +
                    "Background settings → USB port settings. Changing it programmatically " +
                    "requires root and is intentionally not performed by this tool.";

                return new UsbPortModeResult { Success = true, Message = message };
            }
            catch (TimeoutException)
            {
                return new UsbPortModeResult { Success = false, Message = "adb timed out. The device may be in an unusable USB state — replug it." };
            }
            catch (Exception ex)
            {
                return new UsbPortModeResult { Success = false, Message = $"Failed to query the device: {ex.Message}" };
            }
        }

        private static string CleanPropOutput(string raw)
        {
            return (raw ?? string.Empty).Trim().Trim('\r', '\n');
        }

        private static string ModeExplanation(string config)
        {
            if (string.IsNullOrEmpty(config))
            {
                return string.Empty;
            }
            var c = config.ToLowerInvariant();
            if (c.Contains("manufacture"))
            {
                return "\nInterpretation: MANUFACTURE mode (factory/engineering USB enumeration).";
            }
            if (c.Contains("hisuite"))
            {
                return "\nInterpretation: HISUITE mode (standard HiSuite-facing enumeration).";
            }
            return "\nInterpretation: a custom/default function list (no manufacture or hisuite function active).";
        }

        private static bool FileExistsOnPath(string fileName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                if (File.Exists(Path.Combine(dir, fileName)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

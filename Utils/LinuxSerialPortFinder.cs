// Licensed to Kethily Daniel & NDXCode under one or more agreements.
// Kethily Daniel & NDXCode licenses this file to you under the Business Source License 1.1.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace Kirin_Tool.Utils
{
    /// <summary>
    /// Locates USB serial ports by their parent USB device rather than choosing
    /// an arbitrary tty device. This is the Linux equivalent of the Windows WMI
    /// VID/PID lookup used by the original application.
    /// </summary>
    public static class LinuxSerialPortFinder
    {
        public static string? FindPort(int vendorId, int? productId = null, Func<string, bool>? descriptorMatches = null,
            Func<DirectoryInfo, bool>? interfaceMatches = null)
        {
            if (!OperatingSystem.IsLinux())
            {
                return null;
            }

            foreach (string port in SerialPort.GetPortNames().OrderBy(port => port, StringComparer.Ordinal))
            {
                if (!IsUsbSerialPort(port) || !TryGetUsbDevice(port, out DirectoryInfo? usbDevice, out DirectoryInfo? serialDevice))
                {
                    continue;
                }

                if (!MatchesUsbIdentity(usbDevice, vendorId, productId))
                {
                    continue;
                }

                string descriptor = GetUsbDescriptor(serialDevice);
                if ((descriptorMatches == null && interfaceMatches == null) ||
                    (descriptorMatches?.Invoke(descriptor) ?? false) ||
                    (interfaceMatches?.Invoke(serialDevice!) ?? false))
                {
                    return port;
                }
            }

            return null;
        }

        /// <summary>
        /// Huawei hw_cdcacm.inf maps subclass 13/protocol 23 to DBAdapter
        /// Reserved Interface. Read the nearest USB interface, never a sibling.
        /// This only recognizes a port already provided by a kernel driver.
        /// </summary>
        public static bool IsHuaweiUsbUpdateInterface(DirectoryInfo serialDevice)
        {
            for (DirectoryInfo? directory = serialDevice; directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "idVendor")))
                    return false;
                if (File.Exists(Path.Combine(directory.FullName, "bInterfaceClass")))
                    return string.Equals(ReadAttribute(directory, "bInterfaceClass"), "ff", StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(ReadAttribute(directory, "bInterfaceSubClass"), "13", StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(ReadAttribute(directory, "bInterfaceProtocol"), "23", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static bool IsUsbSerialPort(string port)
        {
            return port.StartsWith("/dev/ttyUSB", StringComparison.Ordinal) ||
                   port.StartsWith("/dev/ttyACM", StringComparison.Ordinal) ||
                   port.StartsWith("/dev/ttyGS", StringComparison.Ordinal);
        }

        private static bool TryGetUsbDevice(string port, out DirectoryInfo? usbDevice, out DirectoryInfo? serialDevice)
        {
            serialDevice = ResolveSerialDevice(port);
            for (DirectoryInfo? directory = serialDevice; directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "idVendor")) &&
                    File.Exists(Path.Combine(directory.FullName, "idProduct")))
                {
                    usbDevice = directory;
                    return true;
                }
            }

            usbDevice = null;
            return false;
        }

        public static DirectoryInfo? ResolveSerialDevice(string port, string ttyClassRoot = "/sys/class/tty")
        {
            // Resolve the class symlink FIRST. The relative device link is
            // relative to its real parent, not /sys/class/tty/<name>.
            var entry = new DirectoryInfo(Path.Combine(ttyClassRoot, Path.GetFileName(port)));
            var realEntry = entry.ResolveLinkTarget(true) as DirectoryInfo ?? entry;
            return new DirectoryInfo(Path.Combine(realEntry.FullName, "device"))
                .ResolveLinkTarget(true) as DirectoryInfo;
        }

        private static bool MatchesUsbIdentity(DirectoryInfo usbDevice, int vendorId, int? productId)
        {
            string? vendor = ReadAttribute(usbDevice, "idVendor");
            string? product = ReadAttribute(usbDevice, "idProduct");

            return string.Equals(vendor, $"{vendorId:X4}", StringComparison.OrdinalIgnoreCase) &&
                   (!productId.HasValue ||
                    string.Equals(product, $"{productId.Value:X4}", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetUsbDescriptor(DirectoryInfo usbDevice)
        {
            var values = new List<string>();
            for (DirectoryInfo? directory = usbDevice; directory != null; directory = directory.Parent)
            {
                foreach (string attribute in new[] { "manufacturer", "product", "interface" })
                {
                    string? value = ReadAttribute(directory, attribute);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }

            return string.Join(" ", values);
        }

        private static string? ReadAttribute(DirectoryInfo directory, string attribute)
        {
            try
            {
                string path = Path.Combine(directory.FullName, attribute);
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}

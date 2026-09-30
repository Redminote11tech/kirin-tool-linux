// Licensed to Kethily Daniel & NDXCode under one or more agreements.
// Kethily Daniel & NDXCode licenses this file to you under the Business Source License 1.1.
// See the LICENSE file in the project root for more information.

/*
 * Copyright (c) 2026 Kethily Daniel & NDXCode. All rights reserved.
 * 
 * Use of this software is governed by the Business Source License included 
 * in the LICENSE file and at www.mariadb.com/bsl11.
 * 
 * Change Date: Four years from the date each version of the Licensed Work 
 * is first publicly distributed.
 * 
 * On the Change Date, in accordance with the Business Source License, 
 * use of this software will be governed by the GNU General Public License v3.0 
 * or later (GPL-3.0-or-later).
 * 
 * Contact Information: https://kirintool.cfd
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;
using Kirin_Tool.Models;
using Kirin_Tool.Utils;

namespace Kirin_Tool.Services.USBUpdate
{
    public class UsbDownloadService
    {
        private readonly Action<string> _log;
        private readonly string _dloadDirectory;

        public Func<string, string, bool>? OnRetryRequired;
        public Action<int>? OnProgressUpdate;
        public Action<int, string>? OnPartitionStarted;
        public Action<int, string, int>? OnPartitionProgressUpdate;
        public Action<int, string, bool, string>? OnPartitionCompleted;

        public UsbDownloadService(Action<string> log, string dloadDirectory)
        {
            _log = log;
            _dloadDirectory = dloadDirectory;
        }

        public bool FlashImages(CancellationToken cancellationToken = default)
        {
            // Keep inputs open for the entire operation; do not flash from copied/stale destinations.
            cancellationToken.ThrowIfCancellationRequested();
            using var plan = PrepareFlashPlan();
            byte[] unlockCode = File.ReadAllBytes(Path.Combine(_dloadDirectory, "unlockcode"));
            if (unlockCode.Length != 8) throw new InvalidDataException("Invalid USB Update unlock code.");
            SerialPort port = null;
            
            while (port == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                port = BuildConnection();
                if (port == null)
                {
                    if (OnRetryRequired == null)
                    {
                        throw new OperationCanceledException("USB Update aborted: no HiSilicon device found and no retry handler registered.");
                    }

                    var result = OnRetryRequired.Invoke("Device Not Found",
                        "No HiSilicon device found in USB Update Mode.\n\nConnect the device and click Done! to retry, or Cancel to abort.");

                    if (result == false)
                    {
                        throw new OperationCanceledException("USB Update cancelled by user.");
                    }
                }
            }

            bool success = true;

            try
            {
                DoHandshake(port);
                
                SendUnlockCommand(port, unlockCode);
                
                success = FlashPreparedPartitions(port, plan, cancellationToken);

                if (success)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SendCommand(port, CreateRebootCommand(), 0.3);
                    Thread.Sleep(50);
                    SendCommand(port, CreateForceRebootCommand(), 0.3);
                }
                else
                {
                }
            }
            finally
            {
                port?.Close();
                port?.Dispose();
            }
            return success;
        }

        private SerialPort BuildConnection()
        {
            try
            {
                string portName = FindHiSiliconUsbUpdatePort();
                if (portName == null) return null;

                SerialPort port = new SerialPort(portName, 9600)
                {
                    ReadTimeout = 5000,
                    WriteTimeout = 5000,
                    WriteBufferSize = 8 * 1024 * 1024,
                    ReadBufferSize = 1024 * 1024
                };

                try { port.Open(); return port; }
                catch { port.Dispose(); throw; }
            }
            catch { return null; }
        }

        private static string FindHiSiliconUsbUpdatePort()
        {
            if (OperatingSystem.IsLinux())
            {
                return LinuxSerialPortFinder.FindPort(
                    vendorId: 0x12D1,
                    descriptorMatches: descriptor =>
                        descriptor.Contains("DBAdapter", StringComparison.OrdinalIgnoreCase) ||
                        descriptor.Contains("Reserved Interface", StringComparison.OrdinalIgnoreCase));
            }

            if (OperatingSystem.IsWindows())
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

                foreach (ManagementObject device in searcher.Get())
                {
                    string caption = device["Caption"]?.ToString() ?? string.Empty;
                    string deviceId = device["DeviceID"]?.ToString() ?? string.Empty;
                    if (!deviceId.Contains("VID_12D1", StringComparison.OrdinalIgnoreCase) ||
                        !caption.Contains("DBAdapter Reserved Interface", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int start = caption.IndexOf("(COM", StringComparison.Ordinal);
                    int end = caption.IndexOf(')', start);
                    if (start >= 0 && end > start)
                    {
                        return caption.Substring(start + 1, end - start - 1);
                    }
                }
            }

            return null;
        }

        private void DoHandshake(SerialPort port)
        {
            const int maxRetries = 3;
            byte[] expectedPrefix = new byte[] { 0x7E, 0x26, 0x00, 0x00, 0x25, 0xA7 };

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                byte[] cmd = CreateHandshakeCommand();
                port.Write(cmd, 0, cmd.Length);
                Thread.Sleep(50);

                byte[] response = ReadResponse(port);
                
                if (ContainsSequence(response, expectedPrefix))
                {
                    return;
                }

                if (attempt < maxRetries)
                {
                    Thread.Sleep(200);
                }
            }
            throw new IOException("USB Update handshake failed; no partition writes were started.");
        }

        private void SendUnlockCommand(SerialPort port, byte[] unlockCode)
        {

            List<byte> cmd = new List<byte> { 0x0B };
            cmd.AddRange(unlockCode);

            byte[] crc = Crc16X25.CalculateBytes(cmd.ToArray());
            cmd.AddRange(crc);

            byte[] converted = ConvertData(cmd.ToArray());

            List<byte> finalCmd = new List<byte> { 0x7E };
            finalCmd.AddRange(converted);
            finalCmd.Add(0x7E);

            string? error = SendCommandInternal(port, finalCmd.ToArray(), 0.1);
            if (error != null) throw new IOException($"USB Update unlock failed: {error}");
        }

        private sealed record FlashImage(int Index, string Name, FileStream Image, byte[] Header);
        private sealed class FlashPlan : IDisposable
        {
            public List<FlashImage> Images { get; } = new();
            public void Dispose() { foreach (var image in Images) image.Image.Dispose(); }
        }

        private FlashPlan PrepareFlashPlan()
        {
            var plan = new FlashPlan();
            try
            {
                var lines = File.ReadAllLines(Path.Combine(_dloadDirectory, "list.txt"));
                string mappingPath = Path.Combine(_dloadDirectory, "partition_mapping.txt");
                string[]? mapping = File.Exists(mappingPath) ? File.ReadAllLines(mappingPath) : null;
                if (mapping != null && mapping.Length != lines.Length)
                    throw new InvalidDataException("Partition mapping does not match the flash list.");
                for (int i = 0; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2 || (parts[1] != "0" && parts[1] != "1"))
                        throw new InvalidDataException("Malformed flash list entry.");
                    string name = parts[0];
                    if (name == "." || name == ".." || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-' && c != '.'))
                        throw new InvalidDataException("Invalid partition name.");
                    if (parts[1] == "0") continue;
                    string dir = _dloadDirectory;
                    if (mapping != null)
                    {
                        var entry = mapping[i].Split('|');
                        if (entry.Length != 2 || entry[0] != name || string.IsNullOrWhiteSpace(entry[1]))
                            throw new InvalidDataException("Malformed partition source mapping.");
                        dir = entry[1];
                    }
                    byte[] header = File.ReadAllBytes(Path.Combine(dir, name + ".img.header"));
                    if (header.Length < 98 || BitConverter.ToUInt32(header, 0) != 0xA55AAA55 ||
                        BitConverter.ToUInt32(header, 4) != header.Length)
                        throw new InvalidDataException($"Invalid header for {name}.");
                    string headerName = System.Text.Encoding.ASCII.GetString(header, 60, 32).TrimEnd('\0');
                    // Software testpoint deliberately sends patched XLOADER with the PRELOADER header.
                    if (!headerName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                        !(name.Equals("XLOADER", StringComparison.OrdinalIgnoreCase) && headerName == "PRELOADER"))
                        throw new InvalidDataException($"Header/partition mismatch for {name}.");
                    var image = new FileStream(Path.Combine(dir, name + ".img"), FileMode.Open, FileAccess.Read, FileShare.Read);
                    plan.Images.Add(new FlashImage(i, name, image, header));
                    if (image.Length == 0 || image.Length != BitConverter.ToUInt32(header, 24))
                        throw new InvalidDataException($"Image/header length mismatch for {name}.");
                }
                if (plan.Images.Count == 0) throw new InvalidDataException("No enabled images to flash.");
                return plan;
            }
            catch { plan.Dispose(); throw; }
        }

        private bool FlashPartitions(SerialPort port)
        {
            using var plan = PrepareFlashPlan();
            return FlashPreparedPartitions(port, plan);
        }

        private bool FlashPreparedPartitions(SerialPort port, FlashPlan plan, CancellationToken cancellationToken = default)
        {
            foreach (var image in plan.Images)
            {
                // Finish the current partition before honoring cancellation. Never interrupt its data/tail.
                cancellationToken.ThrowIfCancellationRequested();
                int index = image.Index;
                string name = image.Name;
                OnPartitionStarted?.Invoke(index, name);
                OnPartitionProgressUpdate?.Invoke(index, name, 0);
                byte[] header = new byte[image.Header.Length + 1];
                image.Header.CopyTo(header, 0);
                header[92] = header[93] = 0;
                try
                {
                    string? error = SendCommandInternal(port, CreateHeadCommand(header), 2.0);
                    if (error != null) throw new IOException($"Header rejected for {name}: {error}");
                    error = SendImage(port, image, header, 0x20000);
                    if (error != null) throw new IOException($"Data rejected for {name}: {error}");
                    double tailTimeout = Math.Max(35, Math.Min(180, 15 + image.Image.Length / 1024.0 / 1024 / 10));
                    error = SendCommandInternal(port, CreateTailCommand(header), tailTimeout);
                    if (error != null) throw new IOException($"Finalization failed for {name}: {error}");
                    OnPartitionCompleted?.Invoke(index, name, true, "Success");
                }
                catch (Exception ex)
                {
                    OnPartitionCompleted?.Invoke(index, name, false, ex.Message);
                    _log(ex.Message);
                    return false;
                }
            }
            return true;
        }

        private string? SendImage(SerialPort port, FlashImage image, byte[] header, int blockSize)
        {
            var fs = image.Image;
            fs.Position = 0;
            long fileSize = BitConverter.ToUInt32(header, 24);
            byte[] fileSeq = header.Skip(20).Take(4).Reverse().ToArray();
            byte[] buffer = new byte[blockSize];
            long sent = 0;
            int lastProgress = -1;
            while (sent < fileSize)
            {
                int count = (int)Math.Min(blockSize, fileSize - sent);
                fs.ReadExactly(buffer.AsSpan(0, count));
                byte[] compressed = Compression.ZlibCompress(buffer, 0, count);
                byte[] command = CreateDataCommand(compressed, count, fileSeq, checked((uint)sent));
                double timeout = Math.Max(1, Math.Min(8, compressed.Length / 1024.0 / 1024 * 1.5));
                string? error = SendCommandInternal(port, command, timeout);
                if (error != null) return error;
                sent += count;
                int progress = (int)(sent * 100 / fileSize);
                if (progress != lastProgress)
                {
                    lastProgress = progress;
                    OnProgressUpdate?.Invoke(progress);
                    OnPartitionProgressUpdate?.Invoke(image.Index, image.Name, progress);
                }
            }
            return null;
        }

        private bool SendCommand(SerialPort port, byte[] cmd, double timeout)
        {
            string? error = SendCommandInternal(port, cmd, timeout);
            if (error != null)
            {
                return false;
            }
            return true;
        }

        private string? SendCommandInternal(SerialPort port, byte[] cmd, double timeout)
        {
            int timeoutMs = (int)(timeout * 1000);

            int offset = 0;
            port.DiscardInBuffer();
            while (offset < cmd.Length)
            {
                int toSend = Math.Min(0x10000, cmd.Length - offset);
                port.Write(cmd, offset, toSend);
                offset += toSend;
            }

            List<byte> responseList = new List<byte>();
            int originalTimeout = port.ReadTimeout;
            port.ReadTimeout = Math.Max(1, timeoutMs);
            
            try
            {
                while (true)
                {
                    int val = port.ReadByte();
                    if (val == -1) break;
                    
                    if (responseList.Count == 0 && val != 0x7E) continue;
                    
                    responseList.Add((byte)val);
                    if (responseList.Count >= 2 && responseList[0] == 0x7E && val == 0x7E)
                    {
                        break;
                    }
                }
            }
            catch (TimeoutException)
            {
            }
            finally
            {
                port.ReadTimeout = originalTimeout;
            }
            
            byte[] response = responseList.ToArray();

            byte[] expectedResponse = new byte[] { 0x7E, 0x02, 0x6A, 0xD3, 0x7E };
            
            if (response.SequenceEqual(expectedResponse))
            {
            }
            else if (response.Length >= expectedResponse.Length && ContainsSequence(response, expectedResponse))
            {
            }
            else
            {
                if (response.Length > 0)
                {
                    string responseHex = string.Join(" ", response.Select(b => b.ToString("X2")));
                    if (response.Length >= 2 && response[0] == 0x7E && response[1] == 0x03)
                    {
                        return $"Device error (0x03). Response: {responseHex}";
                    }
                    return $"Unexpected respond: {responseHex}";
                }
                else
                {
                    return $"No respond within {timeoutMs}ms";
                }
            }

            Thread.Sleep(10);
            return null;
        }

        private byte[] ReadResponse(SerialPort port)
        {
            int bytesToRead = port.BytesToRead;
            if (bytesToRead > 0)
            {
                byte[] buffer = new byte[bytesToRead];
                int read = port.Read(buffer, 0, bytesToRead);
                return buffer.Take(read).ToArray();
            }
            return new byte[0];
        }

        private bool ContainsSequence(byte[] source, byte[] pattern)
        {
            if (source == null || pattern == null || source.Length < pattern.Length)
                return false;

            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return true;
            }
            return false;
        }

        private byte[] ConvertData(byte[] data)
        {
            byte[] result = new byte[data.Length * 2];
            int idx = 0;
            
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b == 0x7E)
                {
                    result[idx++] = 0x7D;
                    result[idx++] = 0x5E;
                }
                else if (b == 0x7D)
                {
                    result[idx++] = 0x7D;
                    result[idx++] = 0x5D;
                }
                else
                {
                    result[idx++] = b;
                }
            }

            byte[] finalResult = new byte[idx];
            Array.Copy(result, finalResult, idx);
            return finalResult;
        }

        private byte[] CreateHandshakeCommand()
        {
            byte[] cmd = new byte[] { 0x26, 0x00, 0x00, 0x25, 0xA7, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00 };

            List<byte> payload = new List<byte>(cmd);
            payload.AddRange(Crc16X25.CalculateBytes(cmd));

            List<byte> result = new List<byte>(ConvertData(payload.ToArray()));
            result.Add(0x7E);

            return result.ToArray();
        }

        private byte[] CreateHeadCommand(byte[] headerData)
        {
            List<byte> cmd = new List<byte> { 0x41 };
            cmd.AddRange(headerData);

            byte[] crc = Crc16X25.CalculateBytes(cmd.ToArray());
            cmd.AddRange(crc);

            byte[] converted = ConvertData(cmd.ToArray());

            List<byte> result = new List<byte> { 0x7E };
            result.AddRange(converted);
            result.Add(0x7E);

            return result.ToArray();
        }

        private byte[] CreateTailCommand(byte[] headerData)
        {
            List<byte> cmd = new List<byte> { 0x43 };
            cmd.AddRange(headerData);

            byte[] crc = Crc16X25.CalculateBytes(cmd.ToArray());
            cmd.AddRange(crc);

            byte[] converted = ConvertData(cmd.ToArray());

            List<byte> result = new List<byte> { 0x7E };
            result.AddRange(converted);
            result.Add(0x7E);

            return result.ToArray();
        }

        private byte[] CreateDataCommand(byte[] compressedData, int originalLength, byte[] fileSeq, uint addr)
        {
            List<byte> cmd = new List<byte> { 0x0F };

            uint fileSeqInt = (uint)((fileSeq[0] << 24) | (fileSeq[1] << 16) | (fileSeq[2] << 8) | fileSeq[3]);
            uint combined = fileSeqInt + addr;
            byte[] combinedBytes = new byte[4];
            combinedBytes[0] = (byte)(combined >> 24);
            combinedBytes[1] = (byte)(combined >> 16);
            combinedBytes[2] = (byte)(combined >> 8);
            combinedBytes[3] = (byte)combined;
            cmd.AddRange(combinedBytes);

            byte[] lenBytes = new byte[4];
            lenBytes[0] = (byte)(originalLength >> 24);
            lenBytes[1] = (byte)(originalLength >> 16);
            lenBytes[2] = (byte)(originalLength >> 8);
            lenBytes[3] = (byte)originalLength;
            cmd.AddRange(lenBytes);

            cmd.AddRange(compressedData);

            byte[] crc = Crc16X25.CalculateBytes(cmd.ToArray());
            cmd.AddRange(crc);

            byte[] converted = ConvertData(cmd.ToArray());

            List<byte> result = new List<byte> { 0x7E };
            result.AddRange(converted);
            result.Add(0x7E);

            return result.ToArray();
        }

        private byte[] CreateSingleByteCommand(byte opcode)
        {
            List<byte> payload = new List<byte> { opcode };
            payload.AddRange(Crc16X25.CalculateBytes(new[] { opcode }));

            List<byte> result = new List<byte> { 0x7E };
            result.AddRange(ConvertData(payload.ToArray()));
            result.Add(0x7E);

            return result.ToArray();
        }

        private byte[] CreateRebootCommand()
        {
            return CreateSingleByteCommand(0x0A);
        }

        private byte[] CreateForceRebootCommand()
        {
            return CreateSingleByteCommand(0x32);
        }

        public void SendRebootCommands()
        {

            SerialPort port = null;
            
            while (port == null)
            {
                port = BuildConnection();
                if (port == null)
                {
                    if (OnRetryRequired == null)
                    {
                        _log("SendRebootCommands: device not found and no retry handler registered; aborting.");
                        return;
                    }

                    var result = OnRetryRequired.Invoke("Device Not Found",
                        "No HiSilicon device found in USB Update Mode.\n\nConnect the device and click Done! to retry, or Cancel to abort.");

                    if (result == false)
                    {
                        return;
                    }
                }
            }

            try
            {
                SendCommand(port, CreateRebootCommand(), 0.3);
                Thread.Sleep(50);
                SendCommand(port, CreateForceRebootCommand(), 0.3);
            }
            finally
            {
                port?.Close();
                port?.Dispose();
            }
        }
    }
}

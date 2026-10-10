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

using Kirin_Tool.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kirin_Tool.Utils
{
    public class UpdateApp : IDisposable
    {
        private readonly string _filePath;
        private BinaryReader _binaryReader;
        private FileStream _fileStream;
        private bool _isUsbMode;
        private readonly bool _strict;
        private readonly CancellationToken _scanCancellation;
        private bool _disposed = false;

        public List<PartitionInfo> Partitions { get; private set; }

        public UpdateApp(string filePath, bool isUsbMode = false)
            : this(filePath, isUsbMode, strict: false) { }

        // Strict validation is opt-in for offline extraction; firmware paths retain their behavior.
        public UpdateApp(string filePath, bool isUsbMode, bool strict, CancellationToken cancellationToken = default)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _isUsbMode = isUsbMode;
            _strict = strict;
            _scanCancellation = cancellationToken;

            if (!File.Exists(_filePath))
                throw new FileNotFoundException($"File not found: {_filePath}");

            Partitions = new List<PartitionInfo>();

            try
            {
                _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024);
                _binaryReader = new BinaryReader(_fileStream);
                ParseFile();
            }
            catch (OperationCanceledException)
            {
                _binaryReader?.Dispose();
                _fileStream?.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                _binaryReader?.Dispose();
                _fileStream?.Dispose();
                throw new InvalidOperationException($"Failed to parse UPDATE.APP file: {ex.Message}", ex);
            }
        }

        private void ParseFile()
        {

            long fileLength = _fileStream.Length;

            long currentPosition = 0;

            const byte magicByte0 = 0x55;
            const byte magicByte1 = 0xAA;
            const byte magicByte2 = 0x5A;
            const byte magicByte3 = 0xA5;
            const int scanBufferSize = 1024 * 1024;
            var scanBuffer = new byte[scanBufferSize + 4];

            while (currentPosition < fileLength)
            {
                _scanCancellation.ThrowIfCancellationRequested();
                if (fileLength - currentPosition < 4) break;

                _fileStream.Seek(currentPosition, SeekOrigin.Begin);
                int bytesRead = _fileStream.Read(scanBuffer, 0, scanBuffer.Length);
                if (bytesRead < 4) break;

                bool advanced = false;

                for (int i = 0; i <= bytesRead - 4; i++)
                {
                    if (scanBuffer[i] != magicByte0 || scanBuffer[i + 1] != magicByte1 ||
                        scanBuffer[i + 2] != magicByte2 || scanBuffer[i + 3] != magicByte3)
                    {
                        continue;
                    }

                    var partition = ReadPartition(currentPosition + i);
                    if (partition != null)
                    {
                        Partitions.Add(partition);
                        currentPosition = _fileStream.Position;
                        advanced = true;
                        break;
                    }
                }

                if (!advanced)
                {
                    currentPosition += Math.Max(1, bytesRead - 3);
                }
            }
            if (_strict && Partitions.Count == 0)
                throw new InvalidDataException("No partitions found in UPDATE.APP.");

        }

        private PartitionInfo ReadPartition(long startPosition)
        {
            try
            {
                _fileStream.Seek(startPosition + 4, SeekOrigin.Begin);

                if (_fileStream.Length - _fileStream.Position < (4 + 4 + 8 + 4 + 4 + 16 + 16 + 16))
                    return InvalidEntry("Truncated partition header.");

                var headerSize = _binaryReader.ReadUInt32();
                if (_strict && (headerSize < 98 || headerSize > FileLengthRemaining(startPosition)))
                    throw new InvalidDataException("Invalid partition header length.");
                var unknown1 = _binaryReader.ReadUInt32();
                var hardwareId = _binaryReader.ReadUInt64();
                var sequence = _binaryReader.ReadUInt32();
                var size = _binaryReader.ReadUInt32();

                var date = ReadNullTerminatedString(16);
                var time = ReadNullTerminatedString(16);
                var type = ReadNullTerminatedString(32).Trim();

                
                long currentPosRelative = _fileStream.Position - startPosition;
                long remainingHeaderBytesToSkip = (long)headerSize - currentPosRelative;

                if (remainingHeaderBytesToSkip < 0) return InvalidEntry("Invalid partition header length.");

                if (_fileStream.Length - _fileStream.Position < remainingHeaderBytesToSkip + size)
                    return InvalidEntry("Truncated partition data.");

                _fileStream.Seek(remainingHeaderBytesToSkip, SeekOrigin.Current);

                long dataOffset = _fileStream.Position;

                _fileStream.Seek(size, SeekOrigin.Current);

                var alignment = (4 - _fileStream.Position % 4) % 4;
                if (_fileStream.Length - _fileStream.Position < alignment)
                {
                    if (!_strict || _fileStream.Position != _fileStream.Length)
                        return InvalidEntry("Truncated partition alignment.");
                }
                else _fileStream.Seek(alignment, SeekOrigin.Current);

                if (_strict && (string.IsNullOrWhiteSpace(type) || type.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-' && c != '.') || type == "." || type == ".."))
                    throw new InvalidDataException("Invalid partition name in UPDATE.APP.");
                string partitionName = _isUsbMode ? type : GetPartitionName(type);
                if (string.IsNullOrWhiteSpace(partitionName)) partitionName = "UNKNOWN";

                return new PartitionInfo
                {
                    Name = partitionName,
                    Size = size,
                    FormattedSize = FormatBytes(size),
                    UpdateAppFilePath = _filePath,
                    HeaderSize = headerSize,
                    DataOffset = dataOffset,
                    EntryOffset = startPosition,
                    IsSelected = true 
                };
            }
            catch (EndOfStreamException)
            {
                return InvalidEntry("Truncated partition header.");
            }
            catch (Exception) when (!_strict)
            {
                return null;
            }
        }

        private long FileLengthRemaining(long offset) => _fileStream.Length - offset;

        private PartitionInfo InvalidEntry(string message)
        {
            if (_strict) throw new InvalidDataException(message);
            return null;
        }

        private string ReadNullTerminatedString(int maxLength)
        {
            var bytes = new List<byte>();
            int count = 0;
            byte b;

            while (count < maxLength && (b = _binaryReader.ReadByte()) != 0)
            {
                bytes.Add(b);
                count++;
            }

            if (count < maxLength)
            {
                _fileStream.Seek(maxLength - 1 - count, SeekOrigin.Current);
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private string GetPartitionName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "UNKNOWN";

            string name = rawName.ToLowerInvariant().Trim();

            return name switch
            {
                "hisiufs_gpt" => "ptable",
                "efi" => "ptable",
                "ufsfw" => "ufs_fw",
                "erecovery_ramdis" => "erecovery_ramdisk",
                "recovery_ramdis" => "recovery_ramdisk",
                "vbmeta_hw_produc" => "vbmeta_hw_product",
                _ => name
            };
        }

        public async Task ExtractPartition(PartitionInfo partition, string outputPath)
        {
            if (partition == null) throw new ArgumentNullException(nameof(partition));
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            _fileStream.Seek(partition.DataOffset, SeekOrigin.Begin);

            const int bufferSize = 1024 * 1024;
            var buffer = new byte[bufferSize];
            long remaining = partition.Size;

            using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
            {
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(bufferSize, remaining);
                    int read = await _fileStream.ReadAsync(buffer, 0, toRead);
                    if (read == 0) break;

                    await outputStream.WriteAsync(buffer, 0, read);
                    remaining -= read;
                }
            }
        }

        public async Task ExtractPartitionSafelyAsync(PartitionInfo partition, string outputPath,
            CancellationToken cancellationToken, Action<long>? onBytes = null)
        {
            if (!Partitions.Contains(partition)) throw new ArgumentException("Partition is not in this archive.");
            if (partition.DataOffset < 0 || partition.Size < 0 || partition.Size > _fileStream.Length - partition.DataOffset)
                throw new InvalidDataException("Partition extends beyond UPDATE.APP.");
            string temporaryPath = outputPath + ".partial." + Guid.NewGuid().ToString("N");
            try
            {
                _fileStream.Position = partition.DataOffset;
                await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                {
                    var buffer = new byte[1024 * 1024];
                    long remaining = partition.Size;
                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int read = await _fileStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
                        if (read == 0) throw new EndOfStreamException("UPDATE.APP ended before the partition was complete.");
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        remaining -= read;
                        onBytes?.Invoke(read);
                    }
                    await output.FlushAsync(cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, outputPath, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            double size = bytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:F1} {units[unitIndex]}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _binaryReader?.Dispose();
                _fileStream?.Dispose();
                _disposed = true;
            }
        }
    }
}

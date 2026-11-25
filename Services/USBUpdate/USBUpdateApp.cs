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
using System.IO;
using System.Text;
using Kirin_Tool.Models;
using Kirin_Tool.Utils;

namespace Kirin_Tool.Services.USBUpdate
{
    public class USBUpdateApp
    {
        private readonly Action<string> _log;
        private readonly string _dloadDirectory;

        public USBUpdateApp(Action<string> log, string dloadDirectory)
        {
            _log = log;
            _dloadDirectory = dloadDirectory;
        }

        /// <summary>
        /// Target path for one extracted entry. The name comes from the package header (32 raw
        /// bytes) and is untrusted: it goes through the shared whitelist
        /// (<see cref="PartitionNames"/>) and the resolved path is re-checked against the transfer
        /// directory, so a name like "../../evil" cannot write outside it (audit 2026-11-15 §1.1).
        /// </summary>
        private string ImagePath(string partitionName, string suffix) =>
            PartitionNames.ChildPathWithin(_dloadDirectory, partitionName, suffix);

        /// <summary>
        /// Creates the extraction target exclusively (CreateNew) with 0600 on Unix. Any existing
        /// entry at the validated path is removed first: that deletes a planted symlink instead of
        /// following it, and keeps a re-extraction into the same directory working exactly as before.
        /// </summary>
        private static FileStream CreateExtractionFile(string path)
        {
            if (File.Exists(path) || IsSymlink(path)) File.Delete(path);

            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            return new FileStream(path, options);
        }

        /// <summary>File.Exists is false for a dangling symlink; File.Delete still removes the link.</summary>
        private static bool IsSymlink(string path)
        {
            try { return new FileInfo(path).LinkTarget != null; }
            catch (Exception) { return false; }
        }

        private static void WriteBytesSafely(string path, byte[] bytes)
        {
            using FileStream file = CreateExtractionFile(path);
            file.Write(bytes, 0, bytes.Length);
        }

        public void ExtractUpToXloader(string updateAppPath)
        {
            Directory.CreateDirectory(_dloadDirectory);

            using (FileStream fs = new FileStream(updateAppPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                long startAddr = 0;
                byte[] unlockCmd = FindUnlockCode(reader, ref startAddr);

                // 0600: the unlock code is a package-authorization secret (OperationLogger already
                // masks it in the logs); it must not be world-readable on disk (audit 2026-11-15 §4.1).
                WriteBytesSafely(PartitionNames.ChildPathWithin(_dloadDirectory, "unlockcode", ""), unlockCmd);

                fs.Seek(startAddr, SeekOrigin.Begin);

                List<string> imageList = new List<string>();
                bool foundXloader = false;

                while (true)
                {
                    if (fs.Position + 4 > fs.Length)
                        break;

                    var (dataLength, partitionName, headerData) = ParseImageHeader(reader);

                    if (dataLength == 0 || string.IsNullOrEmpty(partitionName))
                        break;


                    string headerPath = ImagePath(partitionName, ".img.header");
                    WriteBytesSafely(headerPath, headerData);

                    string imgPath = ImagePath(partitionName, ".img");
                    ExtractImageData(reader, imgPath, dataLength);

                    if (File.Exists(imgPath))
                    {
                        long fileSize = new FileInfo(imgPath).Length;
                        if (fileSize == 0)
                        {
                            try
                            {
                                File.Delete(imgPath);
                                File.Delete(headerPath);
                            }
                            catch { }
                            continue;
                        }
                    }

                    imageList.Add($"{partitionName} 1");

                    if (partitionName.Equals("XLOADER", StringComparison.OrdinalIgnoreCase))
                    {
                        foundXloader = true;
                        break;
                    }

                    long currentPos = fs.Position;
                    int padding = (int)(4 - (currentPos % 4)) % 4;
                    if (padding > 0)
                        fs.Seek(padding, SeekOrigin.Current);
                }

                if (!foundXloader)
                {
                    throw new Exception("XLOADER partition not found in UPDATE.APP");
                }

                string listPath = Path.Combine(_dloadDirectory, "list.txt");
                File.WriteAllLines(listPath, imageList);
            }
        }

        public void ExtractSinglePartition(string updateAppPath, string targetPartition)
        {
            Directory.CreateDirectory(_dloadDirectory);

            using (FileStream fs = new FileStream(updateAppPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                long startAddr = 0;
                FindUnlockCode(reader, ref startAddr);
                fs.Seek(startAddr, SeekOrigin.Begin);

                while (true)
                {
                    if (fs.Position + 4 > fs.Length)
                        break;

                    var (dataLength, partitionName, headerData) = ParseImageHeader(reader);

                    if (dataLength == 0)
                        break;

                    if (partitionName.Equals(targetPartition, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteBytesSafely(ImagePath(partitionName, ".img.header"), headerData);

                        string imgPath = ImagePath(partitionName, ".img");
                        ExtractImageData(reader, imgPath, dataLength);
                        return;
                    }
                    else
                    {
                        long remaining = dataLength;
                        while (remaining > 0)
                        {
                            long toSkip = Math.Min(int.MaxValue, remaining);
                            fs.Seek(toSkip, SeekOrigin.Current);
                            remaining -= toSkip;
                        }
                    }

                    long currentPos = fs.Position;
                    int padding = (int)(4 - (currentPos % 4)) % 4;
                    if (padding > 0)
                        fs.Seek(padding, SeekOrigin.Current);
                }

                throw new Exception($"Partition {targetPartition} not found in package.");
            }
        }

        public (List<string> imageList, long startAddr) GetPartitionNames(string updateAppPath)
        {
            List<string> imageList = new List<string>();
            long foundAddr = 0;

            using (FileStream fs = new FileStream(updateAppPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                FindUnlockCode(reader, ref foundAddr);
                fs.Seek(foundAddr, SeekOrigin.Begin);

                while (true)
                {
                    if (fs.Position + 4 > fs.Length)
                        break;

                    var (dataLength, partitionName, _) = ParseImageHeader(reader);

                    if (dataLength == 0 || string.IsNullOrEmpty(partitionName))
                        break;

                    imageList.Add(partitionName);

                    long remaining = dataLength;
                    while (remaining > 0)
                    {
                        long toSkip = Math.Min(int.MaxValue, remaining);
                        fs.Seek(toSkip, SeekOrigin.Current);
                        remaining -= toSkip;
                    }

                    long currentPos = fs.Position;
                    int padding = (int)(4 - (currentPos % 4)) % 4;
                    if (padding > 0)
                        fs.Seek(padding, SeekOrigin.Current);
                }
            }

            return (imageList, foundAddr);
        }

        public List<string> ExtractAllPartitions(string updateAppPath, bool extractUnlockCode = false, long startAddr = -1, Action<int>? onProgress = null, List<string>? includePartitions = null)
        {
            Directory.CreateDirectory(_dloadDirectory);

            List<string> imageList = new List<string>();

            using (FileStream fs = new FileStream(updateAppPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                if (startAddr == -1)
                {
                    long foundAddr = 0;
                    byte[] unlockCmd = FindUnlockCode(reader, ref foundAddr);
                    startAddr = foundAddr;

                    if (extractUnlockCode)
                    {
                        string unlockPath = PartitionNames.ChildPathWithin(_dloadDirectory, "unlockcode", "");
                        if (!File.Exists(unlockPath))
                        {
                            WriteBytesSafely(unlockPath, unlockCmd);
                        }
                    }
                }

                fs.Seek(startAddr, SeekOrigin.Begin);

                while (true)
                {
                    if (fs.Position + 4 > fs.Length)
                        break;

                    var (dataLength, partitionName, headerData) = ParseImageHeader(reader);

                    if (dataLength == 0 || string.IsNullOrEmpty(partitionName))
                        break;

                    if (includePartitions != null && !includePartitions.Contains(partitionName, StringComparer.OrdinalIgnoreCase))
                    {
                        long remaining = dataLength;
                        while (remaining > 0)
                        {
                            long toSkip = Math.Min(int.MaxValue, remaining);
                            fs.Seek(toSkip, SeekOrigin.Current);
                            remaining -= toSkip;
                        }

                        long curr = fs.Position;
                        int pad = (int)(4 - (curr % 4)) % 4;
                        if (pad > 0)
                            fs.Seek(pad, SeekOrigin.Current);

                        continue;
                    }


                    string headerPath = ImagePath(partitionName, ".img.header");
                    WriteBytesSafely(headerPath, headerData);

                    string imgPath = ImagePath(partitionName, ".img");
                    ExtractImageData(reader, imgPath, dataLength);

                    if (File.Exists(imgPath))
                    {
                        long fileSize = new FileInfo(imgPath).Length;
                        if (fileSize == 0)
                        {
                            try
                            {
                                File.Delete(imgPath);
                                File.Delete(headerPath);
                            }
                            catch { }
                            continue;
                        }
                    }

                    imageList.Add($"{partitionName} 1");
                    onProgress?.Invoke((int)((double)fs.Position / fs.Length * 100));

                    long currentPos = fs.Position;
                    int padding = (int)(4 - (currentPos % 4)) % 4;
                    if (padding > 0)
                        fs.Seek(padding, SeekOrigin.Current);
                }
            }

            return imageList;
        }

        private byte[] FindUnlockCode(BinaryReader reader, ref long startAddr)
        {
            long length = reader.BaseStream.Length;
            int bufferSize = 64 * 1024;
            byte[] buffer = new byte[bufferSize + 4];
            long currentPos = reader.BaseStream.Position;

            while (currentPos < length - 4)
            {
                reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                int bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length);
                if (bytesRead < 4) break;

                for (int i = 0; i <= bytesRead - 4; i++)
                {
                    if (buffer[i] == 0x55 && buffer[i + 1] == 0xAA && buffer[i + 2] == 0x5A && buffer[i + 3] == 0xA5)
                    {
                        // long: a >2 GiB package used to truncate the offset into a negative int
                        // and the following Seek threw (audit 2026-11-15 round3 §1.3).
                        startAddr = currentPos + i;
                        reader.BaseStream.Seek(startAddr + 12, SeekOrigin.Begin);
                        byte[] unlockCode = reader.ReadBytes(8);
                        
                        string unlockStr = Encoding.ASCII.GetString(unlockCode).ToLower();
                        if (unlockStr.Contains("hw"))
                        {
                            return unlockCode;
                        }
                        
                        reader.BaseStream.Seek(startAddr + 1, SeekOrigin.Begin);
                    }
                }
                currentPos += (bytesRead - 3);
            }

            throw new Exception("Invalid UPDATE.APP file format: Magic not found");
        }

        private (long dataLength, string partitionName, byte[] headerData) ParseImageHeader(BinaryReader reader)
        {
            MemoryStream headerStream = new MemoryStream();
            BinaryWriter headerWriter = new BinaryWriter(headerStream);

            byte[] magic = reader.ReadBytes(4);

            if (magic.Length < 4)
                return (0, "", new byte[0]);

            headerWriter.Write(magic);

            if (magic[0] != 0x55 || magic[1] != 0xAA || magic[2] != 0x5A || magic[3] != 0xA5)
                return (0, "", new byte[0]);

            int headerLength = reader.ReadInt32();
            headerWriter.Write(headerLength);

            headerWriter.Write(reader.ReadBytes(4));
            headerWriter.Write(reader.ReadBytes(8));
            headerWriter.Write(reader.ReadBytes(4));

            uint dataLengthUint = reader.ReadUInt32();
            long dataLength = dataLengthUint;
            headerWriter.Write(dataLengthUint);

            headerWriter.Write(reader.ReadBytes(16));
            headerWriter.Write(reader.ReadBytes(16));

            byte[] nameBytes = reader.ReadBytes(32);
            headerWriter.Write(nameBytes);

            string partitionName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

            headerWriter.Write(reader.ReadBytes(6));

            // The header length is an attacker-controlled int32 from the package. A value below
            // the 98-byte fixed part would seek backwards, and an unbounded one would be read
            // whole into memory (audit 2026-11-15 round3 §1.3).
            if (headerLength < 98 || headerLength > 4096)
                throw new InvalidDataException("UPDATE.APP entry declares an implausible header length (" + headerLength + ")");

            int remainingHeaderLen = headerLength - 98;

            if (remainingHeaderLen > 0)
            {
                headerWriter.Write(reader.ReadBytes(remainingHeaderLen));
            }

            return (dataLength, partitionName, headerStream.ToArray());
        }

        private void ExtractImageData(BinaryReader reader, string outputPath, long dataLength)
        {
            // Validated path + CreateNew + 0600: never FileMode.Create (it follows symlinks and
            // truncates whatever is already there).
            using (FileStream outFile = CreateExtractionFile(outputPath))
            {
                long remaining = dataLength;
                int bufferSize = 1024 * 1024;
                byte[] buffer = new byte[bufferSize];

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(bufferSize, remaining);
                    int bytesRead = reader.Read(buffer, 0, toRead);
                    if (bytesRead == 0) break;
                    outFile.Write(buffer, 0, bytesRead);
                    remaining -= bytesRead;
                }
            }
        }
    }
}

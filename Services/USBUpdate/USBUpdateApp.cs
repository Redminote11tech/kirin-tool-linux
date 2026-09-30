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
using System.Linq;
using Kirin_Tool.Models;

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

        public void ExtractUpToXloader(string updateAppPath)
        {
            Directory.CreateDirectory(_dloadDirectory);

            using (FileStream fs = new FileStream(updateAppPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                int startAddr = 0;
                byte[] unlockCmd = FindUnlockCode(reader, ref startAddr);

                File.WriteAllBytes(Path.Combine(_dloadDirectory, "unlockcode"), unlockCmd);

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


                    if (Directory.EnumerateFiles(_dloadDirectory).Any(p =>
                        Path.GetFileName(p).Equals($"{partitionName}.img", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException($"Duplicate partition image: {partitionName}");
                    string headerPath = Path.Combine(_dloadDirectory, $"{partitionName}.img.header");
                    File.WriteAllBytes(headerPath, headerData);

                    string imgPath = Path.Combine(_dloadDirectory, $"{partitionName}.img");
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
                int startAddr = 0;
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
                        File.WriteAllBytes(Path.Combine(_dloadDirectory, $"{partitionName}.img.header"), headerData);

                        string imgPath = Path.Combine(_dloadDirectory, $"{partitionName}.img");
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

        public (List<string> imageList, int startAddr) GetPartitionNames(string updateAppPath)
        {
            List<string> imageList = new List<string>();
            int foundAddr = 0;

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

        public List<string> ExtractAllPartitions(string updateAppPath, bool extractUnlockCode = false, int startAddr = -1, Action<int>? onProgress = null, List<string>? includePartitions = null)
        {
            Directory.CreateDirectory(_dloadDirectory);

            List<string> imageList = new List<string>();

            using (FileStream fs = new FileStream(updateAppPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                if (startAddr == -1)
                {
                    int foundAddr = 0;
                    byte[] unlockCmd = FindUnlockCode(reader, ref foundAddr);
                    startAddr = foundAddr;

                    if (extractUnlockCode)
                    {
                        string unlockPath = Path.Combine(_dloadDirectory, "unlockcode");
                        if (!File.Exists(unlockPath))
                        {
                            File.WriteAllBytes(unlockPath, unlockCmd);
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


                    if (Directory.EnumerateFiles(_dloadDirectory).Any(p =>
                        Path.GetFileName(p).Equals($"{partitionName}.img", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException($"Duplicate partition image: {partitionName}");
                    string headerPath = Path.Combine(_dloadDirectory, $"{partitionName}.img.header");
                    File.WriteAllBytes(headerPath, headerData);

                    string imgPath = Path.Combine(_dloadDirectory, $"{partitionName}.img");
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

            if (includePartitions != null && includePartitions.Any(name =>
                !imageList.Any(line => line.Split(' ')[0].Equals(name, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("One or more selected partitions are missing from UPDATE.APP.");
            return imageList;
        }

        private byte[] FindUnlockCode(BinaryReader reader, ref int startAddr)
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
                        startAddr = checked((int)(currentPos + i));
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
            long start = reader.BaseStream.Position;
            byte[] prefix = reader.ReadBytes(8);
            if (prefix.Length != 8 || BitConverter.ToUInt32(prefix, 0) != 0xA55AAA55)
                throw new InvalidDataException("Invalid or truncated UPDATE.APP record.");
            int headerLength = BitConverter.ToInt32(prefix, 4);
            if (headerLength < 98 || headerLength > 16 * 1024 * 1024 || headerLength > reader.BaseStream.Length - start)
                throw new InvalidDataException("Invalid UPDATE.APP header length.");
            byte[] header = new byte[headerLength];
            prefix.CopyTo(header, 0);
            reader.BaseStream.ReadExactly(header.AsSpan(8));
            long length = BitConverter.ToUInt32(header, 24);
            string name = Encoding.ASCII.GetString(header, 60, 32).TrimEnd('\0');
            if (string.IsNullOrEmpty(name) || name == "." || name == ".." ||
                name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-' && c != '.'))
                throw new InvalidDataException("Invalid partition name in UPDATE.APP.");
            if (length == 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException($"Missing or truncated payload for {name} (declared {length} bytes).");
            return (length, name, header);
        }

        private void ExtractImageData(BinaryReader reader, string outputPath, long dataLength)
        {
            bool created = false;
            try
            {
                using var outFile = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
                created = true;
                long remaining = dataLength;
                byte[] buffer = new byte[1024 * 1024];
                while (remaining > 0)
                {
                    int count = (int)Math.Min(buffer.Length, remaining);
                    reader.BaseStream.ReadExactly(buffer.AsSpan(0, count));
                    outFile.Write(buffer, 0, count);
                    remaining -= count;
                }
                outFile.Flush(true);
            }
            catch
            {
                // The operation directory is private; never leave a partial image eligible for flashing.
                if (created && File.Exists(outputPath)) File.Delete(outputPath);
                throw;
            }
        }
    }
}

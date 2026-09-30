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
using System.Linq;
using System.Threading.Tasks;

namespace Kirin_Tool.Utils
{
    // Merge sparse extents in partition coordinates, never in compressed-file order.
    public static class SuperMerger
    {
        private const ushort Raw = 0xCAC1, Fill = 0xCAC2, Skip = 0xCAC3;
        private sealed record Extent(uint Start, uint Blocks, ushort Type, long Offset, byte[] Pattern);
        private sealed record Image(uint BlockSize, uint Blocks, List<Extent> Extents);

        public static Task MergeSuperImages(string path1, string path2, string outputPath, Action<double> progressCallback = null)
            => Task.Run(() => Merge(path1, path2, outputPath, progressCallback));

        private static Image Parse(FileStream stream)
        {
            using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (stream.Length < 28 || r.ReadUInt32() != 0xED26FF3A || r.ReadUInt16() != 1 ||
                r.ReadUInt16() != 0 || r.ReadUInt16() != 28 || r.ReadUInt16() != 12)
                throw new InvalidDataException("Unsupported or truncated sparse header.");
            uint blockSize = r.ReadUInt32(), blocks = r.ReadUInt32(), count = r.ReadUInt32();
            uint checksum = r.ReadUInt32();
            if (blockSize == 0 || blockSize % 4 != 0 || blocks == 0 || count > (stream.Length - 28) / 12)
                throw new InvalidDataException("Invalid sparse geometry.");
            // Do not silently discard an integrity check we have not verified.
            if (checksum != 0) throw new InvalidDataException("Checksummed sparse images require verification before merging.");
            var extents = new List<Extent>();
            ulong at = 0;
            for (uint i = 0; i < count; i++)
            {
                if (stream.Length - stream.Position < 12) throw new EndOfStreamException("Truncated sparse chunk.");
                ushort type = r.ReadUInt16(); r.ReadUInt16();
                uint n = r.ReadUInt32(), size = r.ReadUInt32();
                ulong payload = type switch
                {
                    Raw => (ulong)n * blockSize,
                    Fill => 4,
                    Skip => 0,
                    _ => throw new InvalidDataException("Unsupported sparse chunk (including unverified CRC32 chunks).")
                };
                if (n == 0 || (ulong)size != payload + 12 || payload > (ulong)(stream.Length - stream.Position) || at + n > blocks)
                    throw new InvalidDataException("Invalid sparse chunk length or block range.");
                long offset = stream.Position;
                byte[] pattern = type == Fill ? r.ReadBytes(4) : Array.Empty<byte>();
                if (type != Skip) extents.Add(new Extent((uint)at, n, type, offset, pattern));
                stream.Position = checked(offset + (long)payload);
                at += n;
            }
            if (at != blocks || stream.Position != stream.Length)
                throw new InvalidDataException("Sparse block count or file length does not match its header.");
            return new Image(blockSize, blocks, extents);
        }

        private static void ReadExtent(FileStream stream, Extent extent, uint blockSize, uint start, long offset, Span<byte> buffer)
        {
            if (extent.Type == Fill)
            {
                for (int i = 0; i < buffer.Length; i++) buffer[i] = extent.Pattern[(int)((offset + i) % 4)];
            }
            else
            {
                stream.Position = checked(extent.Offset + (long)(start - extent.Start) * blockSize + offset);
                stream.ReadExactly(buffer);
            }
        }

        private static void Merge(string path1, string path2, string outputPath, Action<double> progress)
        {
            string output = Path.GetFullPath(outputPath);
            if (output == Path.GetFullPath(path1) || output == Path.GetFullPath(path2))
                throw new ArgumentException("Merge output must be different from both inputs.");
            using var a = new FileStream(path1, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var b = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.Read);
            var ia = Parse(a); var ib = Parse(b);
            if (ia.BlockSize != ib.BlockSize || ia.Blocks != ib.Blocks)
                throw new InvalidDataException("Sparse images describe different partition geometry.");
            var boundaries = new SortedSet<uint> { 0, ia.Blocks };
            foreach (var e in ia.Extents.Concat(ib.Extents)) { boundaries.Add(e.Start); boundaries.Add(e.Start + e.Blocks); }
            var points = boundaries.ToArray();
            string temp = output + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var dst = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var w = new BinaryWriter(dst))
                {
                    w.Write(0xED26FF3Au); w.Write((ushort)1); w.Write((ushort)0);
                    w.Write((ushort)28); w.Write((ushort)12); w.Write(ia.BlockSize); w.Write(ia.Blocks);
                    w.Write(0u); w.Write(0u);
                    uint chunkCount = 0;
                    int ai = 0, bi = 0;
                    byte[] buf = new byte[1024 * 1024], other = new byte[1024 * 1024];
                    for (int i = 0; i + 1 < points.Length; i++)
                    {
                        uint start = points[i], blocks = points[i + 1] - start;
                        while (ai < ia.Extents.Count && ia.Extents[ai].Start + ia.Extents[ai].Blocks <= start) ai++;
                        while (bi < ib.Extents.Count && ib.Extents[bi].Start + ib.Extents[bi].Blocks <= start) bi++;
                        var ea = ai < ia.Extents.Count && ia.Extents[ai].Start <= start ? ia.Extents[ai] : null;
                        var eb = bi < ib.Extents.Count && ib.Extents[bi].Start <= start ? ib.Extents[bi] : null;
                        var chosen = ea ?? eb;
                        var source = ea != null ? a : b;
                        // An overlap is valid only if every byte agrees. In particular,
                        // never let a later package silently replace super metadata.
                        long length = (long)blocks * ia.BlockSize;
                        if (ea != null && eb != null)
                        {
                            for (long offset = 0; offset < length;)
                            {
                                int n = (int)Math.Min(buf.Length, length - offset);
                                ReadExtent(a, ea, ia.BlockSize, start, offset, buf.AsSpan(0, n));
                                ReadExtent(b, eb, ia.BlockSize, start, offset, other.AsSpan(0, n));
                                if (!buf.AsSpan(0, n).SequenceEqual(other.AsSpan(0, n)))
                                    throw new InvalidDataException($"Conflicting super image data at block {start + offset / ia.BlockSize}.");
                                offset += n;
                            }
                        }
                        ushort type = chosen?.Type ?? Skip;
                        uint maxBlocks = type == Raw ? (uint.MaxValue - 12) / ia.BlockSize : uint.MaxValue;
                        if (maxBlocks == 0) throw new InvalidDataException("Sparse block size exceeds chunk capacity.");
                        for (uint written = 0; written < blocks;)
                        {
                            uint nBlocks = Math.Min(blocks - written, maxBlocks);
                            long bytes = (long)nBlocks * ia.BlockSize;
                            w.Write(type); w.Write((ushort)0); w.Write(nBlocks);
                            w.Write(type == Raw ? checked((uint)(bytes + 12)) : type == Fill ? 16u : 12u);
                            if (type == Fill) w.Write(chosen.Pattern);
                            if (type == Raw)
                            {
                                for (long offset = 0; offset < bytes;)
                                {
                                    int n = (int)Math.Min(buf.Length, bytes - offset);
                                    ReadExtent(source, chosen, ia.BlockSize, start + written, offset, buf.AsSpan(0, n));
                                    w.Write(buf, 0, n); offset += n;
                                }
                            }
                            chunkCount = checked(chunkCount + 1); written += nBlocks;
                        }
                        progress?.Invoke((double)points[i + 1] * 100 / ia.Blocks);
                    }
                    dst.Position = 20; w.Write(chunkCount); w.Flush(); dst.Flush(true);
                }
                File.Move(temp, output, overwrite: true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

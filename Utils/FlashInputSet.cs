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

namespace Kirin_Tool.Utils;

// Preflight the entire write set and hold read handles until the batch ends.
public sealed class FlashInputSet : IDisposable
{
    private readonly List<FileStream> streams = new();
    public FlashInputSet(IEnumerable<string> paths)
    {
        try
        {
            foreach (string path in paths)
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                streams.Add(stream);
                if (stream.Length == 0) throw new InvalidDataException($"Empty flash image: {path}");
            }
            if (streams.Count == 0) throw new InvalidDataException("No images to flash.");
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (var stream in streams) stream.Dispose(); }
}

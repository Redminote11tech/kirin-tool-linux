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
using Kirin_Tool.Security;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Kirin_Tool.Services
{

    public class UnlockResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
    }
    public class FirmwareUnlocker
    {
        public static readonly Dictionary<string, List<(string Name, int Address, bool PwnFlag)>> CpuAddresses = new Dictionary<string, List<(string, int, bool)>>
        {
            { "hisi65x_a", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("FASTBOOT", 0x10000000, false) } },
            { "hisi65x_b", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("FASTBOOT", 0x10000000, false) } },
            { "hisi620", new List<(string, int, bool)>{ ("XLOADER", unchecked((int)0xF9800800), false), ("FASTBOOT", 0x06800000, false) } },
            { "hisi620c", new List<(string, int, bool)>{ ("XLOADER", unchecked((int)0xF9800800), false), ("FASTBOOT", 0x06800000, false) } },
            { "hisi925", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("FASTBOOT", 0x10000000, false) } },
            { "hisi935", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("FASTBOOT", 0x10000000, false) } },
            { "hisi950", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("FASTBOOT", 0x10000000, false) } },
            { "hisi955", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("FASTBOOT", 0x10000000, false) } },
            { "hisi960", new List<(string, int, bool)>{ ("XLOADER", 0x00020000, false), ("UCE", unchecked((int)0x6A908000), false), ("FASTBOOT", 0x1AC00000, false) } },
            { "hisi970", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x60049000), false), ("FASTBOOT", 0x16800000, false) } },
            { "hisi980", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x60049000), false), ("FASTBOOT", unchecked((int)0x1A400000), false) } },
            { "hisik3v2", new List<(string, int, bool)>{ ("USBLOADER", unchecked((int)0xF8000000), false) } },
            { "hisi710", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x6000D000), false), ("FASTBOOT", 0x1C000000, false) } },
            { "hisi710a", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x6000D000), false), ("FASTBOOT", 0x1C000000, false) } },
            { "hisi810", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x60000000), false), ("FASTBOOT", 0x1C000000, false) } },
            { "hisi820", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x60000000), false), ("FASTBOOT", 0x1A400000, false), ("BL2", 0x1E400000, false) } },
            { "hisi985", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x60000000), false), ("FASTBOOT", 0x1A400000, false), ("BL2", 0x1E400000, false) } },
            { "hisi990", new List<(string, int, bool)>{ ("null", 0x22000, false), ("XLOADER", 0x22000, true), ("UCE", unchecked((int)0x60000000), false), ("FASTBOOT", 0x1A400000, false), ("BL2", 0x1E400000, false) } },
        };

        public async Task<UnlockResult> UnlockFastboot(string cpu, ObservableCollection<ProgressItemViewModel> progressItems, IProgress<string> overallProgress, Func<string, Task<bool>> interactionHandler = null, bool useFastFlashLoader = false)
        {
            try
            {
                var loaderDir = Path.Combine(AppContext.BaseDirectory, "loaders", cpu);
                if (!Directory.Exists(loaderDir))
                {
                    return new UnlockResult { IsSuccess = false, Message = $"Loader directory for {cpu} not found at '{loaderDir}'" };
                }

                if (!CpuAddresses.TryGetValue(cpu, out var stages))
                    throw new NotSupportedException($"Unsupported CPU: {cpu}");
                if (useFastFlashLoader && cpu != "hisi980")
                    throw new NotSupportedException("The fast flash loader is supplied only for Kirin 980.");
                var prepared = new Dictionary<string, byte[]>();
                foreach (var stage in stages)
                {
                    if (!progressItems.Any(p => p.FileName == stage.Name))
                        throw new InvalidOperationException($"Missing required loader stage: {stage.Name}");
                    string name = useFastFlashLoader && stage.Name == "FASTBOOT" ? "fastbootf.ktl" : $"{stage.Name.ToLowerInvariant()}.ktl";
                    string path = Path.Combine(loaderDir, name);
                    byte[] data = CryptoUtil.DTL(await File.ReadAllBytesAsync(path));
                    if (data.Length == 0) throw new InvalidDataException($"Empty loader: {name}");
                    prepared.Add(stage.Name, data);
                }

                using (var flasher = new VcomFlasher())
                {
                    overallProgress.Report("Attempting to connect to device in VCOM mode...");
                    flasher.Connect();
                    overallProgress.Report("Device connected successfully! Sending handshake...");
                    await flasher.SendStartFrame();
                    overallProgress.Report("Handshake sent. Starting flash process...");

                    foreach (var loaderInfo in CpuAddresses[cpu])
                    {
                        var currentItem = progressItems.FirstOrDefault(p => p.FileName == loaderInfo.Name);
                        if (currentItem == null) throw new InvalidOperationException("Required loader stage is missing.");

                        var decryptedData = prepared[loaderInfo.Name];

                        currentItem.StatusText = "Uploading...";
                        overallProgress.Report($"Uploading {loaderInfo.Name}...");

                        var fileProgress = new Progress<(long sent, long total)>(fp => {
                            if (fp.total > 0) currentItem.ProgressValue = ((double)fp.sent / fp.total) * 100;
                        });

                        await flasher.UploadData(decryptedData, loaderInfo.Address, (loaderInfo.PwnFlag, cpu), fileProgress);

                        currentItem.ProgressValue = 100;
                        currentItem.StatusText = "Done";
                        overallProgress.Report($"Finished uploading {loaderInfo.Name}.");

                        if ((cpu == "hisi980" || cpu == "hisi810" || cpu == "hisi820" || cpu == "hisi985" || cpu == "hisi990") && loaderInfo.Name == "null")
                        {
                            overallProgress.Report("Waiting for cable manipulation...");
                            await Task.Delay(1000);
                            if (interactionHandler != null)
                            {
                                bool proceed = await interactionHandler("If you are using a modified cable (Harmony TP), please unplug it from the COMPUTER SIDE, wait a few seconds, then plug it back in.");
                                if (!proceed) return new UnlockResult { IsSuccess = false, Message = "Operation cancelled by user." };
                            }
                        }
                    }

                    if ((cpu == "hisi980" || cpu == "hisi810" || cpu == "hisi820" || cpu == "hisi985" || cpu == "hisi990") && interactionHandler != null)
                    {
                        string replugStr = useFastFlashLoader ? "If you are using a modified cable (Harmony TP), please unplug it from BOTH SIDES, and connect the device with a standard cable." : "If you are using a modified cable (Harmony TP), please unplug it from the COMPUTER SIDE, wait a few seconds, then plug it back in.";
                        if (cpu == "hisi810" || cpu == "hisi820" || cpu == "hisi985" || cpu == "hisi990")
                        {
                            replugStr = "If you are using a modified cable (Harmony TP), please unplug it from BOTH SIDES, and connect the device with a standard cable.";
                        }

                        // await interactionHandler("If you are using a modified cable (Harmony TP):\n1. Unplug it from both sides\n2. Connect the device to the computer with a normal cable\n3. Wait a few seconds\n4. Reconnect using the modified cable");
                        if (!await interactionHandler(replugStr))
                            return new UnlockResult { IsSuccess = false, Message = "Cable reconnection cancelled; loader state is unverified." };
                    }
                }

                var client = new FastbootClient();
                bool connected = false;
                for (int attempt = 0; attempt < 15; attempt++)
                {
                    if (await client.IsDeviceConnected()) { connected = true; break; }
                    await Task.Delay(1000);
                }
                if (!connected) throw new IOException("Loader transfer ended, but a unique fastboot device was not detected. Do not assume unlock succeeded.");
                overallProgress.Report("Loader transfer completed; fastboot device detected. Lock state has not been verified.");
                return new UnlockResult { IsSuccess = true, Message = "Fastboot device detected after loader transfer; lock state unverified." };
            }
            catch (Exception ex)
            {
                overallProgress.Report($"Error: {ex.Message}");
                return new UnlockResult { IsSuccess = false, Message = $"An error occurred: {ex.Message}" };
            }
        }
    }
}
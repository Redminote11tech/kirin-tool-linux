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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kirin_Tool.Models
{
    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        // D-10 / audit S24: the merged text above stays for compatibility, but the streams are
        // also kept apart so a verdict can require the success token to come from stdout.
        public string StandardOutput { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public bool IsSuccess => ExitCode == 0;
    }
}
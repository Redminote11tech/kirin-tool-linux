// Licensed to Kethily Daniel & NDXCode under one or more agreements.
// Kethily Daniel & NDXCode licenses this file to you under the Business Source License 1.1.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Kirin_Tool.Utils
{
    /// <summary>
    /// The one whitelist for UPDATE.APP partition names, shared by both parsers:
    /// <see cref="UpdateApp"/> (strict/offline extraction) and
    /// <c>Services/USBUpdate/USBUpdateApp</c> (the USB-update extractor that actually writes
    /// image files). The name comes from 32 attacker-controlled bytes in the package header, so
    /// it is untrusted input: the loose parser used to concatenate it into
    /// Path.Combine(dir, name + ".img") with no check at all, which is a path traversal
    /// (audit 2026-11-15 round3 §1.1).
    ///
    /// The rule is deliberately identical to the historical strict-parser check
    /// (AsciiLetterOrDigit | '_' | '-' | '.', never "." or ".."), plus a length cap so a name
    /// cannot be used to smuggle separator-like or overlong values past a length check.
    /// </summary>
    public static class PartitionNames
    {
        /// <summary>Upper bound for a partition name. UPDATE.APP stores it in a 32-byte field.</summary>
        public const int MaxLength = 64;

        public static bool IsSafe(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name!.Length > MaxLength) return false;
            if (name == "." || name == "..") return false;

            foreach (char c in name)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    return false;
            }
            return true;
        }

        /// <summary>Same rule, but a rejection is fatal: the caller is about to write a file.</summary>
        public static string Require(string? name)
        {
            if (!IsSafe(name))
                throw new InvalidDataException(
                    "UPDATE.APP entry has an unsafe partition name ('" + Describe(name) + "'): refusing to write it to disk.");
            return name!;
        }

        /// <summary>
        /// Resolves <c>&lt;directory&gt;/&lt;name&gt;&lt;suffix&gt;</c> and re-checks the result, so the
        /// final path can only ever land inside <paramref name="directory"/> (a whitelisted name
        /// cannot contain a separator, and the GetFullPath prefix test catches anything the
        /// whitelist would miss, e.g. on a platform with different separator rules).
        /// </summary>
        public static string ChildPathWithin(string directory, string? name, string suffix)
        {
            string safe = Require(name);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("a target directory is required", nameof(directory));

            string root = Path.GetFullPath(directory);
            string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(root, safe + suffix));

            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!target.StartsWith(prefix, comparison) || target.Length <= prefix.Length)
                throw new InvalidDataException(
                    "UPDATE.APP entry '" + Describe(name) + "' resolves outside " + root + "; refusing to write it.");

            return target;
        }

        /// <summary>Quote a rejected name without letting control characters into the message/log.</summary>
        private static string Describe(string? name)
        {
            if (name == null) return "(null)";
            var builder = new System.Text.StringBuilder(Math.Min(name.Length, 32));
            foreach (char c in name.Length <= 32 ? name : name.Substring(0, 32))
                builder.Append(char.IsControl(c) ? '?' : c);
            return builder.ToString();
        }
    }
}

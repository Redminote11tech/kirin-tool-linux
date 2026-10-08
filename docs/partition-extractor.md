# Candidate 6: Extractor Tools

Open **Extractor Tools** in the sidebar. Browse a Base, Cust or Preload
`UPDATE.APP`, tick the partitions to save, choose an output folder and click
**Extract Selected**. CPU selection and a connected phone are unnecessary.
Load one archive at a time; use separate folders for Base, Cust and Preload if
partition names overlap. The Linux native file/folder pickers are used, as elsewhere
in the app. Select All and Select None are available. Scanning and extraction run
away from the UI thread; Cancel stops either operation.

The page reuses `Utils/UpdateApp.cs`, the existing indexed reader used by firmware
flashing, with an opt-in strict mode. Existing firmware callers keep the original
constructor, partition mapping and extraction behavior. USB Update's separate
`USBUpdateApp` protocol/extraction implementation is unchanged.

All archive entries are shown, including zero-byte and repeated names. Raw archive
names are preserved; output is `NAME.img`, with `_2`, `_3`, etc. when names repeat
or collide irrespective of case. The proposed filename and size appear per row.
Data is copied exactly; sparse images stay sparse-format images. No decompression,
SUPER merging, checksum/signature validation, unlock-code generation or flashing
occurs in this tool. Headers and list.txt are not exported.

Strict mode checks header/data bounds and safe names; it does not silently skip
an entry with recognized magic but an invalid header. No partitions / invalid
input is reported as an error. The partition table is reread before extracting
and must match the displayed table; this is not a whole-source hash check.
The reader uses 64-bit offsets and bounded buffers for large archives.

Existing outputs are refused before copying. Each image is written to a unique
`.partial` file and renamed without overwrite only after the full byte count has
been copied. Cancellation or failure removes that unfinished image. Images
completed earlier in the run remain, and the status reports their count. Cancel
is not a rollback of the complete batch.

## Offline verification

`dotnet run --project tests/Extractor -c Release` exercises selection, Base/Cust/
Preload-shaped synthetic archives, exact image bytes, duplicate names, zero-byte
entries, malformed/truncated input, existing outputs, source-table changes, scan
and copy cancellation, offsets above 4 GiB, and the actual headless UI navigation,
checkboxes and extract handler. It also runs against the DLL extracted from the
built package. These are offline fixtures, not validation against every Huawei
firmware variant. The retained candidate-5 USB transport still needs its separate
hardware validation.

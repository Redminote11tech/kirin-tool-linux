# USB Update and Software Testpoint diagnostics (2.4.8)

The earlier UI discarded service logs. Data/finalization errors lost their raw
response before Software Testpoint replaced the failure with generic Base-version
advice. Old failures cannot be reconstructed from that discarded information.

Services now write to the same daily application log as fastboot: `log/` beside
the app when writable, otherwise `$XDG_STATE_HOME/Kirin-Tool/log/` (normally
`~/.local/state/Kirin-Tool/log/`). Software Testpoint logs its input file path,
preparation and handshake stages, partition starts/completions, exact available
error response bytes, timeouts, block offsets and exception chains. It does not
log firmware payloads or package authorization contents. Successful data blocks
are not individually logged.

An example failure is `Partition XLOADER, finalization: Device error (0x03).
Response: 7E 03 11 22 7E`. The same reason reaches `LastError`, progress callbacks
and the Software Testpoint error popup. This example is a simulated response,
not a captured response from a physical phone. A timeout is reported as a timeout,
not an assumed firmware rejection.

This is a diagnostic correction. Transfer framing, compression, CRC, buffers,
command order, ACK rules, timeouts, retries, loaders and XLOADER patching are
unchanged. In particular the inherited handshake routine still continues after
unmatched responses; the log now says so rather than implying handshake success.
Firmware compatibility and the cause of the earlier Kirin 710 failures remain
unresolved until an actual failing response/stage is captured.

`tests/UsbDiagnostics` exercises ACK/NACK, partial responses, timeout and transport
failures with an in-memory transport and verifies actual diagnostic persistence.
It never opens a USB device or writes firmware. Existing userspace buffer tests
and stable behavior checks are also retained. Stable-boundary hash exceptions are
explicitly annotated for these reviewed diagnostic-only changes.

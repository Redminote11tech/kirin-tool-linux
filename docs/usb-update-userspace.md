# Candidate 5: userspace USB Update

COM 1.0 remains on the user-validated candidate-4 serial implementation.
Only USB Update moves to libusb. Stable has no dependency on this transport.

The native bridge matches vendor 12d1, interface ff/13/23, alternate setting 0,
exactly one bulk IN and OUT. It discovers endpoint addresses and claims only
that interface. Multiple matching interfaces, permission errors, and existing
kernel owners are errors. No automatic driver detach, USB reset, configuration
switch or explicit vendor/CDC control request is performed. Standard USB
operations performed internally by libusb/kernel interface claim/release are
not eliminated; release can restore alternate setting zero. Unload the earlier kirin_usb_update
module manually after closing serial applications before testing candidate 5.

A managed input thread performs bounded bulk reads into a byte queue, preserving
bytes returned with a timeout. The protocol retains its byte reads, buffered
byte count, discard, and timeout interface. Successful short writes continue
with the remaining bytes. Any write error stops without replay. Disconnects
and buffer overflow surface as errors. Discard clears the current managed
queue; as with serial input, bytes arriving after the clear can still arrive.
USB scheduling/buffering timing equivalence remains to be measured on hardware.

USB Update protocol methods are unchanged except transport parameter types;
Windows still uses System.IO.Ports. Candidate 4 and its DKMS bridge remain in
git history on candidate/usb-update-transport as a reference. This candidate's
package must omit DKMS, /usr/src sources and the old driver rule. USB device
permissions still require udev configuration. AppImage can bundle the userspace
bridge and libusb, but cannot install host udev rules by itself.

Validation: fake-device managed tests cover fragmented reads, timeout partial
data, input discard, read deadlines, partial writes, errors, disconnects and
closing a pending read. Native mock tests cover discovery and ownership without
accessing a phone. These do not establish hardware equivalence. Before promoting,
compare USB traffic with candidate 4 and perform staged hardware validation.

## Candidate 5 fastboot read diagnostics

Read SN continues to query `getvar nve:SN`, exactly as frozen Windows source.
It now accepts bare and `(bootloader)`-prefixed values and reports query failures.
Dump Partitions checks process success before parsing the existing Windows INFO
`:partition` format. It reports rejected or unrecognized responses instead of
claiming that the device has no partitions. No partition list is fabricated and
no USB serial number is substituted for NVE data.

During development, read-only queries on the connected phone returned
`FAILED (remote: invalid command)` for both `nve:SN` and `ptable`. The user
reports testing immediately after VCOM Unlock Fastboot. These features remain
hardware-unresolved on that configuration; parser tests do not prove support.

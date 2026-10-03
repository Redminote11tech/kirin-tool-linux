# USB Update serial adapter — unvalidated hardware candidate

This independent GPL-2.0-only kernel module exposes Huawei DBAdapter ff/13/23
as one ttyUSB port, using the kernel USB-serial generic bulk implementation.
It is not incorporated into the BSL application and has not been released.
It does not issue firmware, handshake, vendor-control, line-coding or modem
control commands. USB enumeration itself and generic USB-core operations are
not eliminated. Port opening submits bulk IN reads; only application writes
submit bulk OUT payloads. Initial terminal settings disable echo.

## Evidence and limits

Huawei hw_cdcacm.inf maps vendor 12d1/subclass 13/protocol 23 to DBAdapter
Reserved Interface. The captured Mate 20 Pro USB Update configuration has
one bulk IN endpoint 83 and one bulk OUT endpoint 03 on that interface.
The module discovers endpoints instead of hardcoding their addresses or the
interface number. It rejects all other identities, alternate settings, extra
bulk endpoints and interrupt endpoints. ADB/HDB/PCUI/modem/storage are not
matched. Dynamic-ID attempts are also checked by probe.

Build and identity/layout tests passed on 7.2.6-1-cachyos-bore-lto. Driver
attachment, .NET SerialPort opening, data transfers, timeout/disconnect behavior
and actual software testpoint have NOT been hardware validated. No claim is
made that the Windows driver never uses additional control requests: if the
bulk-only bridge proves insufficient, capture Windows USB traffic instead of
inventing initialization requests. This is not a production flashing release.

## Build

Install matching kernel headers and compiler tools, then run `make` here.
Clang kernels require the LLVM toolchain. Secure Boot/module-signature policy
may require local signing. The generated .ko is specific to the built kernel.
DKMS metadata is supplied for eventual packaging; it is not installed by builds.

## Staged host validation (no firmware writes)

Close Kirin Tool before loading the module. Install the supplied udev rule
before attaching the driver, so ModemManager does not send probe commands:

```sh
sudo install -d -m755 /run/udev/rules.d
sudo install -m644 60-kirin-usb-update.rules /run/udev/rules.d/60-kirin-usb-update.rules
sudo udevadm control --reload-rules
sudo modprobe usbserial
sudo insmod ./kirin_usb_update.ko
```

The /run rule and explicit insmod are temporary. No broad `new_id`, USB reset,
interface detachment or device configuration switch is required. The module
will only bind an unclaimed matching interface. Do not unbind another driver
without first understanding why it owns that interface.

From the candidate application checkout, list the discovered tty without opening:

```sh
dotnet run --project tests/UsbUpdateProbe --no-build
```

Then, as a separate test, open and immediately close with application settings:

```sh
dotnet run --project tests/UsbUpdateProbe --no-build -- --open-close
```

The probe contains no firmware/handshake invocation or serial Write call, and
opens only a tty owned by this adapter. A successful open is NOT a flash test.
Permissions use uucp/uaccess on this Arch/CachyOS setup; adapt group naming for
other distributions. Do not run the full GUI as root to hide permission errors.

After the probe closes, unload the candidate and remove only its temporary rule:

```sh
sudo rmmod kirin_usb_update
sudo rm /run/udev/rules.d/60-kirin-usb-update.rules
sudo udevadm control --reload-rules
```

Do not unload while a port is in use. Full firmware validation is a later,
separate step and has not been authorized as part of the transport-only probe.

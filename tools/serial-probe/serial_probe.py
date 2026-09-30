#!/usr/bin/env python3
"""Huawei manufacture-mode serial probe (read-only listen, then AT handshakes).

ttyUSB mapping on NOH-AN00 (Mate 40 Pro, 12d1:107e) in manufacture mode:
  ttyUSB0 = if0 (subclass 0x13, proto 0x21)
  ttyUSB1 = if1 (subclass 0x13, proto 0x22)
  ttyUSB2 = if2 (subclass 0x13, proto 0x23)
  ttyUSB3 = if3 (ADB 0x42/1) - NOT a serial port, skipped
"""
import os, sys, time, select, termios, tty

PORTS = sys.argv[1].split(",") if len(sys.argv) > 1 else ["/dev/ttyUSB0", "/dev/ttyUSB1", "/dev/ttyUSB2"]
MODE = sys.argv[2] if len(sys.argv) > 2 else "listen"   # listen | at
WAIT = float(sys.argv[3]) if len(sys.argv) > 3 else 4.0

def dump(tag, data):
    if not data:
        return
    print(f"  [{tag}] {len(data)} bytes")
    print(f"    hex: {data.hex()}")
    text = "".join(chr(b) if 32 <= b < 127 else "." for b in data)
    print(f"    txt: {text!r}")

def open_port(path):
    fd = os.open(path, os.O_RDWR | os.O_NOCTTY | os.O_NONBLOCK)
    attrs = termios.tcgetattr(fd)
    attrs[0] = attrs[1] = attrs[3] = 0            # iflag, oflag, lflag = raw
    attrs[2] = termios.B115200 | termios.CS8 | termios.CLOCAL | termios.CREAD
    attrs[4] = attrs[5] = termios.B115200
    termios.tcsetattr(fd, termios.TCSANOW, attrs)
    try: termios.tcflush(fd, termios.TCIOFLUSH)
    except Exception: pass
    return fd

def read_loop(fd, tag, seconds):
    end = time.time() + seconds
    got = False
    while time.time() < end:
        r, _, _ = select.select([fd], [], [], 0.3)
        if fd in r:
            try: data = os.read(fd, 4096)
            except BlockingIOError: continue
            except OSError as e: dump(tag, f"ERR {e}".encode()); break
            if data: dump(tag, data); got = True
    return got

if MODE == "listen":
    for path in PORTS:
        print(f"== {path}: listen-only {WAIT}s (no writes) ==")
        try:
            fd = open_port(path)
        except OSError as e:
            print(f"  open failed: {e}"); continue
        read_loop(fd, os.path.basename(path), WAIT)
        os.close(fd)
elif MODE == "at":
    PROBES = [
        (b"\r\n",      "bare newline"),
        (b"AT\r",      "AT"),
        (b"ATI\r",     "ATI (identify)"),
        (b"AT^GETPORTMODE\r", "AT^GETPORTMODE"),
        (b"AT^VERSION?\r",    "AT^VERSION?"),
    ]
    for path in PORTS:
        print(f"== {path}: AT probes ==")
        try:
            fd = open_port(path)
        except OSError as e:
            print(f"  open failed: {e}"); continue
        for payload, label in PROBES:
            try: os.write(fd, payload)
            except OSError as e:
                print(f"  write failed on {label!r}: {e}"); break
            print(f"  >> {label}")
            if not read_loop(fd, os.path.basename(path), 1.5):
                print("    (no response)")
        os.close(fd)

import os, time, select, termios
def openp(p):
    fd=os.open(p,os.O_RDWR|os.O_NOCTTY|os.O_NONBLOCK)
    a=termios.tcgetattr(fd); a[0]=a[1]=a[3]=0; a[2]=termios.B115200|termios.CS8|termios.CLOCAL|termios.CREAD
    termios.tcsetattr(fd,termios.TCSANOW,a); return fd
def rd(fd,sec):
    out=b""; end=time.time()+sec
    while time.time()<end:
        r,_,_=select.select([fd],[],[],0.15)
        if fd in r:
            try: out+=os.read(fd,4096)
            except BlockingIOError: pass
    return out
CMDS=[b"AT^GETPORTMODE\r",b"AT^SETPORT?\r",b"AT^SN?\r",b"AT^HWVER?\r",b"AT^MEID?\r",b"AT^USBPORT?\r",
      b"AT^HISERVER?\r",b"AT^LOGLEVEL?\r",b"AT^PbmReadEntry?\r",b"AT^SYSINFO\r",b"AT+GMR\r",b"AT+GMI\r",
      b"AT+GMM\r",b"AT+GOI\r",b"AT+CPBF?\r",b"AT^CARDMODE\r",b"AT^FLASHSIZE?\r",b"AT^NVWREX=?\r"]
fd=openp("/dev/ttyUSB1"); rd(fd,0.5)
for c in CMDS:
    os.write(fd,c); r=rd(fd,1.0)
    t="".join(chr(b) if 32<=b<127 else "." for b in r)
    print(f"{c!r:24} -> {t!r}")
os.close(fd)
print("--- ttyUSB0 diag ping ---")
fd=openp("/dev/ttyUSB0"); rd(fd,0.5)
os.write(fd, b"\x7e\x7e"); r=rd(fd,2.0)
print("hex:", r.hex() if r else "(silent)")
os.write(fd, b"\x7e\x41\x7e"); r=rd(fd,2.0)
print("hex:", r.hex() if r else "(silent)")
os.close(fd)

import os, sys, time, select, termios
PORT="/dev/ttyUSB1"
CMDS=[b"AT+CGMI\r",b"AT+CGMM\r",b"AT+CGMR\r",b"AT+CGSN\r",b"AT+CIMI\r",b"AT+CPAS\r",b"AT+CSQ\r",
      b"ATE1\r",b"ATV1\r",b"ATZ\r",b"AT&F\r",b"AT^HVER?\r",b"AT^VERSION?\r",b"AT^CHIPVER?\r",
      b"AT^AUTH?\r",b"AT^AUTH=?\r",b"AT^AUTH\r",b"AT^PERMISSION?\r",b"AT^DEBUG?\r",b"AT+CGMR=<< ehem\r"]
fd=os.open(PORT,os.O_RDWR|os.O_NOCTTY|os.O_NONBLOCK)
a=termios.tcgetattr(fd); a[0]=a[1]=a[3]=0; a[2]=termios.B115200|termios.CS8|termios.CLOCAL|termios.CREAD
termios.tcsetattr(fd,termios.TCSANOW,a)
def rd(sec):
    out=b""; end=time.time()+sec
    while time.time()<end:
        r,_,_=select.select([fd],[],[],0.2)
        if fd in r:
            try: out+=os.read(fd,4096)
            except BlockingIOError: pass
    return out
rd(0.5)
for c in CMDS:
    try: os.write(fd,c)
    except OSError as e: print("write err",e); break
    r=rd(1.2)
    t="".join(chr(b) if 32<=b<127 else "." for b in r)
    print(f">> {c!r:26} -> {t!r}")
os.close(fd)

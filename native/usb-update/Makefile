CFLAGS ?= -O2
all: libkirin_usb_update.so
libkirin_usb_update.so: bridge.c
	$(CC) $(CFLAGS) -fPIC -shared -Wall -Wextra -Werror $(shell pkg-config --cflags libusb-1.0) $< -o $@ $(shell pkg-config --libs libusb-1.0) -Wl,-rpath,'$$ORIGIN'
clean:
	rm -f libkirin_usb_update.so

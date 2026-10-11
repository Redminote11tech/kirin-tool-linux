# Makefile for the Kirin Tool Linux bundled fastboot
#
# Sources: AOSP platform/system/core fastboot + libsparse @ android-6.0.1_r1
# (Apache-2.0), trimmed for the Huawei/Kirin flashing flows used by Kirin Tool
# and extended with the vendor OEM upload-to-file client behavior (see
# PATCHES.md).

CC      ?= gcc
CXX     ?= g++
CFLAGS  ?= -O2 -Wall
CFLAGS  += -I libsparse/include -I src
CFLAGS  += '-DFASTBOOT_REVISION="kirin-tool-linux-2.1-stable vendor-upload-ultraflash-v3 (AOSP android-6.0.1_r1)"'
LIBS    := -lz -lpthread

SRCS_CXX := src/fastboot.cpp src/bootimg_utils.cpp
SRCS_C   := src/protocol.c src/usb_linux.c src/util.c src/util_linux.c src/engine.c \
            libsparse/sparse.c libsparse/backed_block.c libsparse/output_file.c \
            libsparse/sparse_read.c libsparse/sparse_err.c libsparse/sparse_crc32.c

OBJS := $(SRCS_CXX:.cpp=.o) $(SRCS_C:.c=.o)

all: fastboot

$(OBJS): Makefile src/fastboot.h

fastboot: $(OBJS)
	$(CXX) -o $@ $(OBJS) $(LIBS) $(LDFLAGS)

%.o: %.c
	$(CC) $(CFLAGS) -c -o $@ $<

%.o: %.cpp
	$(CXX) $(CFLAGS) -c -o $@ $<

clean:
	rm -f fastboot $(OBJS)

.PHONY: all clean

# Fake USB only: never links usb_linux.c or opens a device.
test: tests/vendor-upload-test
	@mkdir -p tests/output
	./tests/vendor-upload-test tests/output/backup.img
	@test -z "$$(find tests/output -type f -name '*.partial.*' -print -quit)"

tests/vendor-upload-test: tests/vendor_upload_test.c src/protocol.c src/fastboot.h
	$(CC) $(CFLAGS) -ffunction-sections -fdata-sections -Wl,--gc-sections -o $@ tests/vendor_upload_test.c src/protocol.c

.PHONY: test

# Exercise the real argument dispatcher, with USB replaced by a mock.
tests/cli_usb_stub.o: tests/cli_usb_stub.c
	$(CC) $(CFLAGS) -c -o $@ $<
tests/cli-fastboot: $(filter-out src/usb_linux.o,$(OBJS)) tests/cli_usb_stub.o
	$(CXX) -o $@ $^ $(LIBS) $(LDFLAGS)
test-cli: tests/cli-fastboot
	python3 tests/cli_dispatch_test.py
	python3 tests/vendor_features_test.py
.PHONY: test-cli

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
CFLAGS  += '-DFASTBOOT_REVISION="kirin-tool-linux-2.0-stable vendor-storage-upload-v1 (AOSP android-6.0.1_r1)"'
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

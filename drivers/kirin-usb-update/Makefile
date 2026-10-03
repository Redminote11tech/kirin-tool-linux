obj-m += kirin_usb_update.o
KDIR ?= /lib/modules/$(shell uname -r)/build
# Use the same compiler family as the target kernel (needed for Clang LTO).
ifneq ($(shell grep '^CONFIG_CC_IS_CLANG=y' $(KDIR)/.config 2>/dev/null),)
export LLVM := 1
endif
.PHONY: all clean
all:
	$(MAKE) -C $(KDIR) M=$(CURDIR) modules
clean:
	$(MAKE) -C $(KDIR) M=$(CURDIR) clean

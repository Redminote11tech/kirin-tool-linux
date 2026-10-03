// SPDX-License-Identifier: GPL-2.0-only
/* Huawei DBAdapter bulk byte-stream adapter. No firmware/protocol commands. */
#include <linux/module.h>
#include <linux/tty.h>
#include <linux/usb.h>
#include <linux/usb/serial.h>
#include "match.h"

static const struct usb_device_id kirin_ids[] = {
	{ USB_VENDOR_AND_INTERFACE_INFO(0x12d1, 0xff, 0x13, 0x23) },
	{ }
};
MODULE_DEVICE_TABLE(usb, kirin_ids);

static int kirin_probe(struct usb_serial *serial,
		       const struct usb_device_id *id)
{
	const struct usb_interface_descriptor *d =
		&serial->interface->cur_altsetting->desc;
	/* Also enforce the identity for manual/dynamic binding attempts. */
	if (!kirin_identity_matches(le16_to_cpu(serial->dev->descriptor.idVendor),
	    d->bInterfaceClass, d->bInterfaceSubClass, d->bInterfaceProtocol,
	    d->bAlternateSetting))
		return -ENODEV;
	return 0;
}

static int kirin_ports(struct usb_serial *serial,
		       struct usb_serial_endpoints *epds)
{
	/* Accept only the observed single bidirectional bulk stream. */
	if (!kirin_endpoints_match(epds->num_bulk_in, epds->num_bulk_out,
	    epds->num_interrupt_in, epds->num_interrupt_out))
		return -ENODEV;
	return 1;
}

static void kirin_init_termios(struct tty_struct *tty)
{
	/* Never echo unsolicited device bytes during the interval between open
	 * and the application's termios configuration. This is a binary stream.
	 */
	tty->termios.c_iflag = 0;
	tty->termios.c_oflag = 0;
	tty->termios.c_lflag = 0;
	tty->termios.c_cflag = B9600 | CS8 | CREAD | CLOCAL;
}

static struct usb_serial_driver kirin_driver = {
	.driver = { .owner = THIS_MODULE, .name = "kirin_usb_update" },
	.description = "Huawei DBAdapter USB Update",
	.id_table = kirin_ids,
	.num_ports = 1,
	.num_bulk_in = 1,
	.num_bulk_out = 1,
	.probe = kirin_probe,
	.init_termios = kirin_init_termios,
	.calc_num_ports = kirin_ports,
	/* All I/O uses the kernel USB-serial generic bulk implementation.
	 * No control transfers, line coding, DTR/RTS or vendor requests.
	 */
};
static struct usb_serial_driver * const kirin_drivers[] = {
	&kirin_driver, NULL
};
module_usb_serial_driver(kirin_drivers, kirin_ids);
MODULE_LICENSE("GPL");
MODULE_DESCRIPTION("Narrow Huawei ff/13/23 DBAdapter bulk serial adapter");

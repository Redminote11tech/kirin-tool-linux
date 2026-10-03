/* SPDX-License-Identifier: GPL-2.0-only */
#ifndef KIRIN_USB_UPDATE_MATCH_H
#define KIRIN_USB_UPDATE_MATCH_H
static inline int kirin_identity_matches(unsigned int vendor, unsigned int cls,
	unsigned int sub, unsigned int proto, unsigned int alt)
{
	return vendor == 0x12d1 && cls == 0xff && sub == 0x13 &&
		proto == 0x23 && alt == 0;
}
static inline int kirin_endpoints_match(unsigned int in, unsigned int out,
	unsigned int interrupt_in, unsigned int interrupt_out)
{
	return in == 1 && out == 1 && !interrupt_in && !interrupt_out;
}
#endif

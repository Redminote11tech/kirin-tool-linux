/* SPDX-License-Identifier: GPL-2.0-only */
#include <assert.h>
#include <stdio.h>
#include "match.h"
int main(void)
{
	/* Enumerate all subclass/protocol pairs, including captured siblings. */
	unsigned int sub, proto;
	for (sub = 0; sub < 256; sub++)
		for (proto = 0; proto < 256; proto++)
			assert(kirin_identity_matches(0x12d1, 0xff, sub, proto, 0)
				== (sub == 0x13 && proto == 0x23));
	assert(!kirin_identity_matches(0x18d1, 0xff, 0x13, 0x23, 0));
	assert(!kirin_identity_matches(0x12d1, 0x08, 0x13, 0x23, 0));
	assert(!kirin_identity_matches(0x12d1, 0xff, 0x13, 0x23, 1));
	for (unsigned int in = 0; in < 3; in++)
	 for (unsigned int out = 0; out < 3; out++)
	  for (unsigned int ii = 0; ii < 2; ii++)
	   for (unsigned int io = 0; io < 2; io++)
	    assert(kirin_endpoints_match(in, out, ii, io)
		== (in == 1 && out == 1 && ii == 0 && io == 0));
	puts("PASS: driver rejects sibling identities and invalid endpoint layouts");
}

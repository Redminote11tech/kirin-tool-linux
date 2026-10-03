/* Fake USB for full CLI dispatch regression. Never opens real hardware. */
#include "usb.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
usb_handle *usb_open(ifc_match_func f) { return (usb_handle *)1; }
int usb_close(usb_handle *h) { return 0; }
int usb_wait_for_disconnect(usb_handle *h) { return 0; }
int usb_write(usb_handle *h,const void *data,int n) {
 const char *expected=getenv("EXPECT_COMMAND");
 if(!expected || strlen(expected)!=(size_t)n || memcmp(data,expected,n)) {
  fprintf(stderr,"Unexpected wire command\n"); exit(90);
 }
 fprintf(stderr,"WIRE_MATCH\n"); return n;
}
int usb_read(usb_handle *h,void *data,int n) {
 const char *s=getenv("MOCK_RESPONSE");
 if(!s || strlen(s)>(size_t)n)exit(91);
 memcpy(data,s,strlen(s));return strlen(s);
}

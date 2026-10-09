/* Fake USB for full CLI dispatch regression. Never opens real hardware. */
#include "usb.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
static FILE *trace;
static int trace_event(char expected, void *data, int n, int write)
{
 if(!trace) {trace=fopen(getenv("MOCK_TRACE"),"r");if(!trace)exit(92);}
 char line[8192];if(!fgets(line,sizeof(line),trace))exit(93);
 if(expected=='R' && line[0]=='E') {errno=EIO;return -1;}
 if(line[0]!=expected) {fprintf(stderr,"Trace order mismatch: %s",line);exit(94);}
 int bytes=0;
 for(char *p=line+2; p[0] && p[0]!='\n'; p+=2) {
  unsigned v;if(!p[1] || sscanf(p,"%2x",&v)!=1 || bytes>=n)exit(95);
  if(write && ((const unsigned char*)data)[bytes]!=(unsigned char)v)exit(96);
  if(!write)((unsigned char*)data)[bytes]=(unsigned char)v;
  bytes++;
 }
 if(write && bytes!=n)exit(97);
 fprintf(stderr,"TRACE_MATCH %c %d\n",expected,bytes);return bytes;
}
usb_handle *usb_open(ifc_match_func f) { return (usb_handle *)1; }
int usb_close(usb_handle *h) { return 0; }
int usb_wait_for_disconnect(usb_handle *h) { return 0; }
int usb_write(usb_handle *h,const void *data,int n) {
 if(getenv("MOCK_TRACE"))return trace_event('W',(void*)data,n,1);
 const char *expected=getenv("EXPECT_COMMAND");
 if(!expected || strlen(expected)!=(size_t)n || memcmp(data,expected,n)) {
  fprintf(stderr,"Unexpected wire command\n"); exit(90);
 }
 fprintf(stderr,"WIRE_MATCH\n"); return n;
}
int usb_read(usb_handle *h,void *data,int n) {
 if(getenv("MOCK_TRACE"))return trace_event('R',data,n,0);
 const char *s=getenv("MOCK_RESPONSE");
 if(!s || strlen(s)>(size_t)n)exit(91);
 memcpy(data,s,strlen(s));return strlen(s);
}

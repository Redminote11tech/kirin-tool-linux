/* SPDX-License-Identifier: MIT */
#include <libusb.h>
#include <assert.h>
#include <string.h>
#include <stdio.h>
int ku_open(void **,char *,int);void ku_close(void *);int ku_write(void *,unsigned char *,int,int *,unsigned int);
static int devices=1,busy,claims,closes,open_error;
static struct libusb_endpoint_descriptor eps[2]={{.bEndpointAddress=0x83,.bmAttributes=2},{.bEndpointAddress=3,.bmAttributes=2}};
static struct libusb_interface_descriptor alt={.bInterfaceNumber=2,.bNumEndpoints=2,.bInterfaceClass=255,.bInterfaceSubClass=19,.bInterfaceProtocol=35,.endpoint=eps};
static struct libusb_interface iface={.altsetting=&alt,.num_altsetting=1};
static struct libusb_config_descriptor cfg={.bNumInterfaces=1,.interface=&iface};
int libusb_init(libusb_context **c){*c=(void*)1;return 0;}
void libusb_exit(libusb_context *c){(void)c;}
ssize_t libusb_get_device_list(libusb_context *c,libusb_device ***l){static libusb_device *list[]={(void*)2,(void*)3,NULL};(void)c;*l=list;return devices;}
void libusb_free_device_list(libusb_device **l,int u){(void)l;(void)u;}
int libusb_get_device_descriptor(libusb_device *d,struct libusb_device_descriptor *v){(void)d;memset(v,0,sizeof(*v));v->idVendor=0x12d1;return 0;}
int libusb_get_active_config_descriptor(libusb_device *d,struct libusb_config_descriptor **v){(void)d;*v=&cfg;return 0;}
void libusb_free_config_descriptor(struct libusb_config_descriptor *v){(void)v;}
int libusb_open(libusb_device *d,libusb_device_handle **h){(void)d;if(open_error)return open_error;*h=(void*)4;return 0;}
void libusb_close(libusb_device_handle *h){(void)h;closes++;}
int libusb_kernel_driver_active(libusb_device_handle *h,int i){(void)h;assert(i==2);return busy;}
int libusb_claim_interface(libusb_device_handle *h,int i){(void)h;assert(i==2);claims++;return 0;}
int libusb_release_interface(libusb_device_handle *h,int i){(void)h;assert(i==2);return 0;}
int libusb_bulk_transfer(libusb_device_handle *h,unsigned char ep,unsigned char *b,int n,int *t,unsigned int timeout){(void)h;(void)b;assert(ep==3);assert(timeout==77);*t=n;return 0;}
const char *libusb_error_name(int e){(void)e;return "mock error";}
int main(void){void *p=NULL;char err[256]={0};int n;unsigned char b=0;
 assert(ku_open(&p,err,sizeof(err))==0&&p&&claims==1);assert(ku_write(p,&b,1,&n,77)==0&&n==1);ku_close(p);assert(closes==1);
 busy=1;assert(ku_open(&p,err,sizeof(err))==LIBUSB_ERROR_BUSY&&!p&&claims==1);busy=0;
 devices=2;assert(ku_open(&p,err,sizeof(err))==LIBUSB_ERROR_BUSY&&!p&&claims==1);devices=0;assert(ku_open(&p,err,sizeof(err))==LIBUSB_ERROR_NO_DEVICE);devices=1;
 for(int protocol=0;protocol<256;protocol++){if(protocol==35)continue;alt.bInterfaceProtocol=protocol;assert(ku_open(&p,err,sizeof(err))==LIBUSB_ERROR_NO_DEVICE);}
 alt.bInterfaceProtocol=35;eps[1].bmAttributes=3;assert(ku_open(&p,err,sizeof(err))==LIBUSB_ERROR_NO_DEVICE);eps[1].bmAttributes=2;
 open_error=LIBUSB_ERROR_ACCESS;assert(ku_open(&p,err,sizeof(err))==LIBUSB_ERROR_ACCESS&&!p);
 puts("PASS native selection, ambiguity, busy driver, permissions, endpoints and cleanup");return 0;}

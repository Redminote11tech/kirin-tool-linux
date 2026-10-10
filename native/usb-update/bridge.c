/* SPDX-License-Identifier: MIT
 * USB Update transport bridge. No protocol packets or control transfers.
 */
#include <libusb.h>
#include <stdlib.h>
#include <stdio.h>
struct bridge { libusb_context *ctx; libusb_device_handle *handle; int iface; unsigned char in,out; };
/* Discovery reads cached configuration descriptors; opening only claims the
 * matching interface. Never reset/reconfigure a device or detach a driver. */
int ku_open(void **result, char *error, int error_size) {
 struct bridge *b=calloc(1,sizeof(*b));libusb_device **list=NULL,*chosen=NULL;
 int r, candidates=0;ssize_t count;
 *result=NULL;if(!b)return LIBUSB_ERROR_NO_MEM;
 r=libusb_init(&b->ctx);if(r<0)goto fail;
 count=libusb_get_device_list(b->ctx,&list);if(count<0){r=(int)count;goto fail;}
 for(ssize_t i=0;i<count;i++) {
  struct libusb_device_descriptor d;struct libusb_config_descriptor *cfg=NULL;
  if(libusb_get_device_descriptor(list[i],&d)<0||d.idVendor!=0x12d1)continue;
  if(libusb_get_active_config_descriptor(list[i],&cfg)<0)continue;
  for(int j=0;j<cfg->bNumInterfaces;j++)for(int k=0;k<cfg->interface[j].num_altsetting;k++) {
   const struct libusb_interface_descriptor *a=&cfg->interface[j].altsetting[k];
   if(a->bAlternateSetting||a->bInterfaceClass!=0xff||a->bInterfaceSubClass!=0x13||a->bInterfaceProtocol!=0x23||a->bNumEndpoints!=2)continue;
   unsigned char in=0,out=0;
   for(int e=0;e<2;e++) {
    const struct libusb_endpoint_descriptor *ep=&a->endpoint[e];
    if((ep->bmAttributes&3)!=LIBUSB_TRANSFER_TYPE_BULK)continue;
    if(ep->bEndpointAddress&LIBUSB_ENDPOINT_IN)in=ep->bEndpointAddress;else out=ep->bEndpointAddress;
   }
   if(!in||!out)continue;
   candidates++;chosen=list[i];b->iface=a->bInterfaceNumber;b->in=in;b->out=out;
  }
  libusb_free_config_descriptor(cfg);
 }
 if(candidates!=1){r=candidates?LIBUSB_ERROR_BUSY:LIBUSB_ERROR_NO_DEVICE;snprintf(error,error_size,"Expected one Huawei DBAdapter interface; found %d",candidates);goto fail_named;}
 r=libusb_open(chosen,&b->handle);if(r<0)goto fail;
 r=libusb_kernel_driver_active(b->handle,b->iface);
 if(r==1){r=LIBUSB_ERROR_BUSY;snprintf(error,error_size,"DBAdapter is owned by a kernel driver. Close serial apps and unload kirin_usb_update before using candidate 5.");goto fail_named;}
 if(r<0)goto fail;
 r=libusb_claim_interface(b->handle,b->iface);if(r<0)goto fail;
 libusb_free_device_list(list,1);*result=b;return 0;
fail: snprintf(error,error_size,"USB Update: %s",libusb_error_name(r));
fail_named:
 if(list)libusb_free_device_list(list,1);
 if(b->handle)libusb_close(b->handle);
 if(b->ctx)libusb_exit(b->ctx);
 free(b);return r;
}
int ku_read(void *ctx,unsigned char *buffer,int length,int *transferred,unsigned int timeout) {
 struct bridge *b=ctx;return libusb_bulk_transfer(b->handle,b->in,buffer,length,transferred,timeout);
}
int ku_write(void *ctx,unsigned char *buffer,int length,int *transferred,unsigned int timeout) {
 struct bridge *b=ctx;return libusb_bulk_transfer(b->handle,b->out,buffer,length,transferred,timeout);
}
void ku_close(void *ctx) {
 struct bridge *b=ctx;if(!b)return;
 libusb_release_interface(b->handle,b->iface);libusb_close(b->handle);libusb_exit(b->ctx);free(b);
}

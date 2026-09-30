/* Hardware-free regression tests for the independently implemented vendor reader. */
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <inttypes.h>
#include <string.h>
#include <signal.h>
#include <sys/resource.h>
#include <sys/stat.h>
#include <unistd.h>
#include "fastboot.h"

static int state, mode, requests, closed;
static uint64_t remaining, total, copied, address;
static char kind[16];
#define CHECK(c) do { if (!(c)) { fprintf(stderr,"assertion failed line %d: %s\n",__LINE__,#c); exit(2); } } while(0)
int usb_close(usb_handle *u) { closed++; return 0; }
int usb_write(usb_handle *u,const void *data,int size)
{
    if (mode >= 10) { CHECK(state == 0); CHECK(size == 17); CHECK(!memcmp(data,"download:00000004",17)); state=1; return size; }
    char cmd[128];CHECK(size<sizeof(cmd));memcpy(cmd,data,size);cmd[size]=0;
    if (state==0) { char expected[64];snprintf(expected,sizeof(expected),"getvar:%s:oeminfo",kind);CHECK(!strcmp(cmd,expected));state=1; }
    else {
        CHECK(state==2);uint64_t at,n;char expected[64];snprintf(expected,sizeof(expected),"upload_%s:%%16" SCNx64 ":%%16" SCNx64,kind);
        CHECK(sscanf(cmd,expected,&at,&n)==2);CHECK(at==address+copied);CHECK(n==(total-copied>16777216?16777216:total-copied));
        requests++;remaining=n;state=3;
    }
    return size;
}
static int response(void *p,const char *s){memcpy(p,s,strlen(s));return strlen(s);}
int usb_read(usb_handle *u,void *data,int count)
{
    if (mode >= 10) { CHECK(state == 1); state=2; return response(data, mode == 10 ? "DATA00000002" : "OKAY"); }
    if(state==1){state=2;
        if(mode==1)return response(data,"OKAYnot-a-range");
        if(mode==2)return response(data,"FAILunknown variable");
        char text[64];snprintf(text,sizeof(text),"OKAY%016" PRIx64 ":%016" PRIx64,address,total);return response(data,text);
    }
    if(state==3){state=4;if(mode==3)return response(data,"FAILupload refused");return response(data,"OKAY");}
    if(state==4){if(mode==4)return 0;int n=remaining<count?(int)remaining:count;
        // Short USB reads are legal; the reader must accumulate them exactly.
        if(n>65537)n=65537;
        for(int i=0;i<n;i++)((unsigned char*)data)[i]=(unsigned char)(copied+i);
        copied+=n;remaining-=n;if(!remaining)state=5;return n;
    }
    CHECK(state==5);state=2;return response(data,mode==5?"FAILfinalization":"OKAY");
}
static void reset(int m,uint64_t bytes,const char *k){mode=m;state=0;requests=closed=0;total=bytes;copied=0;address=0x12340000;strcpy(kind,k);}
static void keep(const char *p){FILE *f=fopen(p,"wb");CHECK(f);CHECK(fwrite("KEEP",1,4,f)==4);CHECK(!fclose(f));}
static void unchanged(const char *p){char data[8]={0};FILE *f=fopen(p,"rb");CHECK(f);CHECK(fread(data,1,8,f)==4);CHECK(!strcmp(data,"KEEP"));fclose(f);}
int main(int argc,char **argv)
{
    CHECK(argc==2);const char *path=argv[1];
    for(int m=1;m<=5;m++){keep(path);reset(m,8192,"emmc");CHECK(fb_dump_partition(NULL,kind,"oeminfo",path)<0);unchanged(path);}
    struct rlimit before,limited;CHECK(!getrlimit(RLIMIT_FSIZE,&before));limited=before;limited.rlim_cur=4096;
    signal(SIGXFSZ,SIG_IGN);keep(path);CHECK(!setrlimit(RLIMIT_FSIZE,&limited));reset(0,8192,"emmc");
    CHECK(fb_dump_partition(NULL,kind,"oeminfo",path)<0);CHECK(!setrlimit(RLIMIT_FSIZE,&before));unchanged(path);
    // Boundary requires two vendor requests, exercises raw bytes and short reads.
    for(int k=0;k<2;k++){
        reset(0,16777216+257,k?"storage":"emmc");CHECK(!fb_dump_partition(NULL,kind,"oeminfo",path));CHECK(requests==2);
        struct stat st;CHECK(!stat(path,&st));CHECK(st.st_size==total);
        FILE *f=fopen(path,"rb");CHECK(f);for(uint64_t i=0;i<total;i++)CHECK(fgetc(f)==(unsigned char)i);fclose(f);
    }
    reset(0,10,"emmc");CHECK(fb_dump_partition(NULL,kind,"oeminfo bad",path)<0);CHECK(state==0);
    for(int m=10;m<=11;m++){reset(m,4,"emmc");CHECK(fb_download_data(NULL,"TEST",4)<0);CHECK(state==2);}
    printf("PASS native vendor upload: emmc/storage sequence, 16-MiB boundary, short reads, invalid metadata, query/start/tail failures, EOF, disk-write failure and prior-file preservation, rejected short/missing DATA negotiation\n");
    unlink(path);return 0;
}

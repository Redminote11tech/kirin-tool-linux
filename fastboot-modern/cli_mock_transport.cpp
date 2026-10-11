// Full modern CLI test transport; this file is never linked into production.
#include <cstdint>
#include "transport.h"
#include "usb.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cerrno>
#include <memory>
class TraceTransport : public Transport {
    FILE* trace_;
    ssize_t Event(char kind,void* data,size_t size,bool write) {
        char line[8192]; if(!fgets(line,sizeof(line),trace_)) std::exit(93);
        if(kind=='R' && line[0]=='E') {errno=EIO;return -1;}
        if(line[0]!=kind) {fprintf(stderr,"Trace order mismatch: %s",line);std::exit(94);}
        size_t count=0;
        if(line[1]=='@') {
            unsigned value,n;
            if(sscanf(line+2,":%x:%u",&value,&n)!=2 || value>255 || n>size)std::exit(98);
            count=n;
            if(write) {for(size_t i=0;i<count;++i)if(static_cast<unsigned char*>(data)[i]!=value)std::exit(96);}
            else memset(data,value,count);
        } else {
            for(char* p=line+2; p[0] && p[0]!='\n'; p+=2) {
                unsigned value; if(!p[1] || sscanf(p,"%2x",&value)!=1 || count>=size)std::exit(95);
                if(write && static_cast<unsigned char*>(data)[count]!=value) {fprintf(stderr,"Wire mismatch: expected %s",line);std::exit(96);}
                if(!write)static_cast<unsigned char*>(data)[count]=value;
                ++count;
            }
        }
        if(write && count!=size)std::exit(97);
        fprintf(stderr,"TRACE_MATCH %c %zu\n",kind,count);return count;
    }
public:
    TraceTransport() {
        const char* path=getenv("MOCK_TRACE");
        static std::unique_ptr<FILE,decltype(&fclose)> shared(path?fopen(path,"r"):nullptr,&fclose);
        trace_=shared.get();
        if(!trace_)std::exit(92);
    }
    ~TraceTransport() override = default;
    ssize_t Read(void* data,size_t size) override {return Event('R',data,size,false);}
    ssize_t Write(const void* data,size_t size) override {return Event('W',const_cast<void*>(data),size,true);}
    int Close() override {return 0;}
    int Reset() override {return 0;}
    int WaitForDisconnect() override {return 0;}
};
std::unique_ptr<Transport> KirinFakeTransport() {return std::make_unique<TraceTransport>();}
std::unique_ptr<UsbTransport> usb_open(ifc_match_func, uint32_t) {std::exit(99);}

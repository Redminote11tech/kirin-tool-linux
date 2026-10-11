// Hardware-free tests for the independently ported Huawei host extensions.
#include "fastboot_driver.h"
#include "mock_transport.h"
#include <android-base/file.h>
#include <gtest/gtest.h>
#include <cstdlib>
#include <cstdio>
#include <fstream>
#include <string>
#include <unistd.h>
using namespace testing;
using namespace fastboot;

class HuaweiTest : public Test {
protected:
    InSequence sequence;
    std::string folder, file;
    void SetUp() override {
        char tmp[] = "/tmp/kirin-huawei-parity-XXXXXX";
        folder = mkdtemp(tmp); file = folder + "/dump.img";
        unsetenv("HUAWEI_ULTRAFLASH");
    }
    void TearDown() override { unlink(file.c_str()); rmdir(folder.c_str()); unsetenv("HUAWEI_ULTRAFLASH"); }
    std::string contents() { std::string data; EXPECT_TRUE(android::base::ReadFileToString(file,&data)); return data; }
    void keep() { ASSERT_TRUE(android::base::WriteStringToFile("KEEP",file)); }
};

TEST_F(HuaweiTest, NamedMemoryShortReadsAndReceipt) {
    auto ptr = std::make_unique<MockTransport>(); auto* tr = ptr.get();
    FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("getvar:memory:oeminfo"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY0000000012340000:0000000000000004")));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("upload_memory:0000000012340000:0000000000000004"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("AB")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("CD")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    ASSERT_EQ(driver.HuaweiDump("memory","oeminfo",file),SUCCESS) << driver.Error();
    EXPECT_EQ(contents(),"ABCD");
}

TEST_F(HuaweiTest, StorageSixteenMiBBoundary) {
    auto ptr = std::make_unique<MockTransport>(); auto* tr = ptr.get();
    FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("upload_storage:0000000012340000:0000000001000000"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_CALL(*tr,Read(_,1024*1024)).Times(16).WillRepeatedly(Invoke([](void* data,size_t size){memset(data,'Z',size);return size;}));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("upload_storage:0000000013340000:0000000000000007"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("1234567")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    ASSERT_EQ(driver.HuaweiUpload("storage",0x12340000,0x1000007,file),SUCCESS) << driver.Error();
    std::string data = contents(); ASSERT_EQ(data.size(),0x1000007U);
    EXPECT_EQ(data.substr(0x1000000),"1234567"); EXPECT_EQ(data.substr(0,4),"ZZZZ");
}

TEST_F(HuaweiTest, TailFailurePreservesExistingOutput) {
    keep();
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("upload_emmc:0000000000000000:0000000000000004"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("ABCD")));
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("FAILtail refused")));
    EXPECT_EQ(driver.HuaweiUpload("emmc",0,4,file),DEVICE_FAIL);
    EXPECT_EQ(contents(),"KEEP"); EXPECT_NE(driver.Error().find("tail refused"),std::string::npos);
}

TEST_F(HuaweiTest, InvalidRangesNeverWriteToTransport) {
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).Times(0);
    EXPECT_EQ(driver.HuaweiUpload("memory",UINT64_MAX,2,file),BAD_ARG);
    EXPECT_EQ(driver.HuaweiUpload("memory",0,0,file),BAD_ARG);
    EXPECT_EQ(driver.HuaweiUpload("bad",0,4,file),BAD_ARG);
    EXPECT_EQ(driver.HuaweiDump("storage","bad:name",file),BAD_ARG);
    EXPECT_EQ(driver.HuaweiUpload("memory",0,4,folder+"/missing/out"),IO_ERROR);
}

TEST_F(HuaweiTest, UnexpectedDataDoesNotUseVendorFraming) {
    keep();auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("DATA00000004")));
    EXPECT_EQ(driver.HuaweiUpload("memory",0,4,file),BAD_DEV_RESP); EXPECT_EQ(contents(),"KEEP");
}

TEST_F(HuaweiTest, UltraBeginAndEnd) {
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("ultraflash:boot"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("ultraflash:"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("OKAY")));
    EXPECT_EQ(driver.HuaweiUltraBegin("boot"),SUCCESS); EXPECT_EQ(driver.HuaweiUltraEnd(),SUCCESS);
}

TEST_F(HuaweiTest, UltraRemoteRefusalDisablesFurtherNegotiation) {
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).With(AllArgs(RawData("ultraflash:boot"))).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("FAILCommand not allowed")));
    EXPECT_EQ(driver.HuaweiUltraBegin("boot"),SUCCESS); EXPECT_EQ(driver.HuaweiUltraEnd(),SUCCESS);
    EXPECT_EQ(driver.HuaweiUltraBegin("cust"),SUCCESS); EXPECT_EQ(driver.HuaweiUltraEnd(),SUCCESS);
}

TEST_F(HuaweiTest, UltraTransportFailureDoesNotFallback) {
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Return(-1));
    EXPECT_EQ(driver.HuaweiUltraBegin("boot"),IO_ERROR);
    EXPECT_EQ(driver.HuaweiUltraEnd(),SUCCESS);
}

TEST_F(HuaweiTest, UltraUnexpectedDataDoesNotFlash) {
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).WillOnce(ReturnArg<1>());
    EXPECT_CALL(*tr,Read(_,_)).WillOnce(Invoke(CopyData("DATA00000004")));
    EXPECT_EQ(driver.HuaweiUltraBegin("boot"),BAD_DEV_RESP); EXPECT_EQ(driver.HuaweiUltraEnd(),SUCCESS);
}

TEST_F(HuaweiTest, UltraOptOutNeverTouchesTransport) {
    auto ptr=std::make_unique<MockTransport>();auto* tr=ptr.get();FastBootDriver driver(std::move(ptr));
    EXPECT_CALL(*tr,Write(_,_)).Times(0);
    for(const char* mode:{"0","off"}) {setenv("HUAWEI_ULTRAFLASH",mode,1);EXPECT_EQ(driver.HuaweiUltraBegin("boot"),SUCCESS);EXPECT_EQ(driver.HuaweiUltraEnd(),SUCCESS);}
}

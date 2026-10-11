// Huawei host protocol reconstructed from the user-supplied frozen Windows
// clients. No proprietary decompiler output or firmware is included.
#include "fastboot_driver.h"
#include "transport.h"
#include <android-base/stringprintf.h>
#include <algorithm>
#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cstdint>
#include <inttypes.h>
#include <unistd.h>
#include <vector>

namespace fastboot {
using android::base::StringPrintf;

RetCode FastBootDriver::HuaweiUltraBegin(const std::string& partition) {
    huawei_ultra_active_ = false;
    const char* mode = getenv("HUAWEI_ULTRAFLASH");
    if (huawei_ultra_disabled_ || (mode && (!strcmp(mode,"0") || !strcmp(mode,"off")))) return SUCCESS;
    int data_size = -1;
    RetCode result = RawCommand("ultraflash:" + partition, nullptr, nullptr, &data_size);
    if (result == DEVICE_FAIL) {
        fprintf(stderr, "UltraFlash refused (%s); using standard flashing\n", Error().c_str());
        huawei_ultra_disabled_ = true;
        return SUCCESS;
    }
    if (result != SUCCESS) return result;
    if (data_size >= 0) { error_ = "unexpected DATA during UltraFlash negotiation"; return BAD_DEV_RESP; }
    huawei_ultra_active_ = true;
    return SUCCESS;
}

RetCode FastBootDriver::HuaweiUltraEnd() {
    if (!huawei_ultra_active_) return SUCCESS;
    huawei_ultra_active_ = false;
    int data_size = -1;
    RetCode result = RawCommand("ultraflash:", nullptr, nullptr, &data_size);
    if (result == SUCCESS && data_size >= 0) { error_ = "unexpected DATA during UltraFlash end"; return BAD_DEV_RESP; }
    return result;
}

RetCode FastBootDriver::HuaweiDump(const std::string& kind, const std::string& partition,
                                 const std::string& filename) {
    if ((kind != "emmc" && kind != "storage" && kind != "memory") || partition.empty() ||
        partition.size() > 32 || partition.find_first_not_of("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-") != std::string::npos) {
        error_ = "invalid vendor partition name or kind"; return BAD_ARG;
    }
    std::string response;
    RetCode result = GetVar(kind + ":" + partition, &response);
    if (result != SUCCESS) { fprintf(stderr, "KIRIN_DUMP_QUERY_FAILED\n"); return result; }
    auto parse_hex16 = [](const std::string& text, uint64_t* value) {
        if (text.size() != 16 || text.find_first_not_of("0123456789abcdefABCDEF") != std::string::npos) return false;
        *value = strtoull(text.c_str(), nullptr, 16); return true;
    };
    uint64_t address, length;
    if (response.size() != 33 || (response[16] != ':' && response[16] != ' ') ||
        !parse_hex16(response.substr(0,16), &address) || !parse_hex16(response.substr(17), &length)) {
        error_ = "invalid vendor partition offset/length"; return BAD_DEV_RESP;
    }
    return HuaweiUpload(kind, address, length, filename);
}

RetCode FastBootDriver::HuaweiUpload(const std::string& kind, uint64_t address,
                                   uint64_t length, const std::string& filename) {
    if ((kind != "memory" && kind != "emmc" && kind != "storage" && kind != "ufseye") ||
        !length || length > UINT64_MAX - address || filename.empty()) {
        error_ = "invalid vendor upload range"; return BAD_ARG;
    }
    std::vector<char> buffer(1024 * 1024);
    bool to_stdout = filename == "stdout";
    std::string temporary;
    FILE* output = stdout;
    if (!to_stdout) {
        temporary = filename + ".partial.XXXXXX";
        int fd = mkstemp(temporary.data());
        if (fd < 0) { error_ = ErrnoStr("cannot create backup"); return IO_ERROR; }
        output = fdopen(fd,"wb");
        if (!output) { close(fd); unlink(temporary.c_str()); error_ = ErrnoStr("cannot open backup stream"); return IO_ERROR; }
    }
    RetCode result = SUCCESS;
    for (uint64_t copied = 0; copied < length;) {
        uint64_t request = std::min<uint64_t>(length - copied, 16 * 1024 * 1024);
        std::string command = StringPrintf("upload_%s:%016" PRIx64 ":%016" PRIx64, kind.c_str(), address + copied, request);
        int data_size = -1;
        result = RawCommand(command, nullptr, nullptr, &data_size);
        if (result != SUCCESS) break;
        if (data_size >= 0) { error_ = "unexpected DATA in vendor OKAY-framed upload"; result = BAD_DEV_RESP; break; }
        for (uint64_t remaining = request; remaining;) {
            size_t count = std::min<uint64_t>(remaining, buffer.size());
            ssize_t got = transport_->Read(buffer.data(),count);
            if (got <= 0 || static_cast<size_t>(got) > count) { error_ = "short/failed vendor upload"; result = IO_ERROR; break; }
            if (fwrite(buffer.data(),1,got,output) != static_cast<size_t>(got)) { error_ = ErrnoStr("backup write failed"); result = IO_ERROR; break; }
            remaining -= got;
        }
        if (result != SUCCESS) break;
        result = HandleResponse(nullptr,nullptr,&data_size);
        if (result != SUCCESS) break;
        if (data_size >= 0) { error_ = "unexpected DATA in vendor upload completion"; result = BAD_DEV_RESP; break; }
        copied += request;
    }
    if (result == SUCCESS && (fflush(output) || (!to_stdout && fsync(fileno(output))))) {
        error_ = ErrnoStr("backup flush failed"); result = IO_ERROR;
    }
    if (!to_stdout) {
        if (fclose(output) && result == SUCCESS) { error_ = ErrnoStr("backup close failed"); result = IO_ERROR; }
        if (result == SUCCESS && rename(temporary.c_str(),filename.c_str())) { error_ = ErrnoStr("cannot publish backup"); result = IO_ERROR; }
        if (result != SUCCESS) unlink(temporary.c_str());
    }
    if (result == SUCCESS) fprintf(stderr,"KIRIN_DUMP_OK bytes=%" PRIu64 "\n",length);
    return result;
}
} // namespace fastboot

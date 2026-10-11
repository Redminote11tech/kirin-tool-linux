#!/usr/bin/env python3
"""Apply independently written Huawei extensions to pinned AOSP build sources."""
import argparse, pathlib, shutil
p=argparse.ArgumentParser();p.add_argument('source');a=p.parse_args()
root=pathlib.Path(a.source).resolve(); here=pathlib.Path(__file__).resolve().parent

def edit(relative, old, new):
    path=root/relative;s=path.read_text()
    if new in s:return
    if s.count(old)!=1:raise ValueError(f'Pinned source mismatch in {relative}: {old[:70]}')
    path.write_text(s.replace(old,new))

edit('vendor/libbase/hex.cpp','#include "android-base/hex.h"','#include "android-base/hex.h"\n#include <cstdint>')
edit('vendor/libziparchive/include/ziparchive/zip_writer.h','#include <cstdio>','#include <cstdio>\n#include <cstdint>')
edit('vendor/core/fastboot/fastboot_driver.h','    RetCode Boot(std::string* response = nullptr, std::vector<std::string>* info = nullptr);','''    RetCode HuaweiUltraBegin(const std::string& partition);
    RetCode HuaweiUltraEnd();
    RetCode HuaweiDump(const std::string& kind, const std::string& partition, const std::string& filename);
    RetCode HuaweiUpload(const std::string& kind, uint64_t address, uint64_t length, const std::string& filename);
    RetCode Boot(std::string* response = nullptr, std::vector<std::string>* info = nullptr);''')
edit('vendor/core/fastboot/fastboot_driver.h','    bool disable_checks_;','''    bool disable_checks_;
    bool huawei_ultra_disabled_ = false;
    bool huawei_ultra_active_ = false;''')
edit('vendor/CMakeLists.fastboot.txt','set(fastboot_SOURCES\n','set(fastboot_SOURCES\n\tcore/fastboot/huawei_vendor.cpp\n')
shutil.copyfile(here/'huawei_vendor.cpp',root/'vendor/core/fastboot/huawei_vendor.cpp')
old='''    if (args->empty()) syntax_error("empty oem command");

    std::string command(cmd);'''
new='''    if (args->empty()) syntax_error("empty oem command");
    if (cmd == FB_CMD_OEM) {
        const std::string op = args->front();
        auto hex = [](const std::string& text) -> uint64_t {
            std::string digits = text;
            if (digits.size() >= 2 && digits[0] == '0' && (digits[1] == 'x' || digits[1] == 'X')) digits.erase(0, 2);
            if (digits.empty() || digits.find_first_not_of("0123456789abcdefABCDEF") != std::string::npos)
                syntax_error("invalid hexadecimal operand");
            char* end = nullptr;
            errno = 0;
            uint64_t value = strtoull(text.c_str(), &end, 16);
            if (errno || !end || *end) syntax_error("invalid hexadecimal operand");
            return value;
        };
        fastboot::RetCode result;
        bool vendor = true;
        if (op == "dump-emmc" || op == "dump-storage" || op == "memory") {
            if (args->size() != 3) syntax_error("vendor dump requires a name and local filename");
            if (op == "memory" && ((*args)[1].empty() || (*args)[1].size() > 15)) syntax_error("memory name must be 1-15 bytes");
            result = fb->HuaweiDump(op == "dump-emmc" ? "emmc" : op == "dump-storage" ? "storage" : "memory", (*args)[1], (*args)[2]);
        } else if (op == "memupload") {
            if (args->size() != 4) syntax_error("memupload requires hex address, hex length and local filename");
            result = fb->HuaweiUpload("memory", hex((*args)[1]), hex((*args)[2]), (*args)[3]);
        } else if (op == "dump-ufseye") {
            if (args->size() != 5) syntax_error("dump-ufseye requires hex index, hex size, hex lane and local filename");
            uint64_t index = hex((*args)[1]), size = hex((*args)[2]), lane = hex((*args)[3]);
            if (!index || !size || size > 0x1000000 || lane > 1) syntax_error("invalid UFS-eye operands");
            result = fb->HuaweiUpload("ufseye", index | (lane << 8), size, (*args)[4]);
        } else vendor = false;
        if (vendor) {
            if (result != fastboot::SUCCESS) die("vendor upload failed: %s", fb->Error().c_str());
            args->clear();
            return;
        }
    }

    std::string command(cmd);'''
edit('vendor/core/fastboot/fastboot.cpp',old,new)
edit('vendor/core/fastboot/fastboot.cpp','''    switch (buf->type) {
        case FB_BUFFER_SPARSE: {''','''    if (fb->HuaweiUltraBegin(partition) != fastboot::SUCCESS)
        die("UltraFlash negotiation failed: %s", fb->Error().c_str());
    switch (buf->type) {
        case FB_BUFFER_SPARSE: {''')
edit('vendor/core/fastboot/fastboot.cpp','''}

std::string get_current_slot() {''','''    if (fb->HuaweiUltraEnd() != fastboot::SUCCESS)
        die("UltraFlash end failed: %s", fb->Error().c_str());
}

std::string get_current_slot() {''')
edit('CMakeLists.txt','set(ANDROID_VENDOR android-tools)','set(ANDROID_VENDOR kirin-tool-linux-huawei-parity)')
edit('vendor/core/fastboot/fastboot.cpp','''            " oem [COMMAND...]           Execute OEM-specific command.\\n"''','''            " oem [COMMAND...]           Execute OEM-specific command.\\n"
            " oem memory NAME OUTPUT     Upload named Huawei DDR memory.\\n"
            " oem memupload ADDR SIZE OUTPUT  Upload a Huawei memory range (hex).\\n"
            " oem dump-emmc PART OUTPUT | oem dump-storage PART OUTPUT\\n"
            " oem dump-ufseye INDEX SIZE LANE OUTPUT  Upload UFS-eye data (hex).\\n"
            " OUTPUT stdout sends raw vendor bytes to stdout; logs use stderr.\\n"
            " HUAWEI_ULTRAFLASH=0 or off disables Huawei negotiation.\\n"''')
print('Prepared pinned modern AOSP source with Huawei upload and UltraFlash extensions')

shutil.copyfile(here/'tests.cmake',root/'vendor/KirinTests.cmake')
path = root/'vendor/CMakeLists.fastboot.txt'
s = path.read_text()
marker = '\nif(KIRIN_PARITY_TEST_SOURCE)\n    include(KirinTests.cmake)\nendif()\n'
if marker not in s:path.write_text(s+marker)
edit('vendor/core/fastboot/fastboot.cpp', 'static std::unique_ptr<Transport> open_device() {', '#ifdef KIRIN_FAKE_TRANSPORT\nstd::unique_ptr<Transport> KirinFakeTransport();\n#endif\nstatic std::unique_ptr<Transport> open_device() {\n#ifdef KIRIN_FAKE_TRANSPORT\n    return KirinFakeTransport();\n#endif')

# Explicit queries must preserve the app's nonzero-on-refusal contract. Optional
# startup information remains optional because DumpInfo ignores the result.
edit('vendor/core/fastboot/fastboot.cpp', 'static void DisplayVarOrError(', 'static bool DisplayVarOrError(')
edit('vendor/core/fastboot/fastboot.cpp', '''        fprintf(stderr, "FAILED (%s)\\n", fb->Error().c_str());
        return;
    }
    fprintf(stderr, "%s: %s\\n", label.c_str(), value.c_str());
}''', '''        fprintf(stderr, "FAILED (%s)\\n", fb->Error().c_str());
        return false;
    }
    fprintf(stderr, "%s: %s\\n", label.c_str(), value.c_str());
    return true;
}''')
edit('vendor/core/fastboot/fastboot.cpp', '            DisplayVarOrError(variable, variable);', '            if (!DisplayVarOrError(variable, variable)) return 1;')
# Retain the frozen older client's authenticated-message download command.
edit('vendor/core/fastboot/fastboot.cpp', '''            } else if (args.size() == 1 &&
                       (args[0] == "unlock"''', '''            } else if (args[0] == "unlock_bootloader" && args.size() == 2) {
                std::string filename = args[1];
                fastboot_buffer buf;
                if (!load_buf(filename.c_str(), &buf, fp.get()) || buf.type != FB_BUFFER_FD) die("cannot load '%s'", filename.c_str());
                fb->Download("unlock_message", buf.fd.get(), buf.sz);
                fb->RawCommand("flashing unlock_bootloader", "unlocking bootloader");
                args.clear();
            } else if (args.size() == 1 &&
                       (args[0] == "unlock"''')
edit('vendor/core/fastboot/fastboot.cpp', '''args[0] == "lock_critical" || args[0] == "get_unlock_ability"))''', '''args[0] == "lock_critical" || args[0] == "get_unlock_ability" ||
                        args[0] == "get_unlock_bootloader_nonce" || args[0] == "lock_bootloader"))''')

shutil.copyfile(here/'atomic_output.h',root/'vendor/core/fastboot/atomic_output.h')
for path in ['vendor/core/fastboot/fastboot.cpp','vendor/core/fastboot/fastboot_driver.cpp']:
    edit(path, '#include "fastboot_driver.h"', '#include "fastboot_driver.h"\n#include "atomic_output.h"')
edit('vendor/core/fastboot/fastboot_driver.cpp', '''    std::ofstream ofs;
    ofs.open(outfile, std::ofstream::out | std::ofstream::binary);
    if (ofs.fail()) {''', '''    KirinAtomicOutput output(outfile);
    if (output.Fd() < 0) {''')
edit('vendor/core/fastboot/fastboot_driver.cpp', '''        ofs.write(data, size);
        if (ofs.fail() || ofs.bad()) {''', '''        if (!android::base::WriteFully(output.Fd(), data, size)) {''')
edit('vendor/core/fastboot/fastboot_driver.cpp', '''    ofs.close();
    return ret;''', '''    if (ret == SUCCESS && !output.Commit()) {
        error_ = ErrnoStr("Cannot publish uploaded file");
        return IO_ERROR;
    }
    return ret;''')
edit('vendor/core/fastboot/fastboot.cpp', '''    unique_fd fd(TEMP_FAILURE_RETRY(
            open(outfile.c_str(), O_WRONLY | O_CREAT | O_TRUNC | O_CLOEXEC | O_BINARY, 0644)));
    auto fetch = std::bind(fetch_partition, _1, borrowed_fd(fd), fb);
    do_for_partitions(partition, slot_override, fetch, false /* force slot */);''', '''    KirinAtomicOutput output(outfile);
    if (output.Fd() < 0) die("Cannot create fetch output: %s", strerror(errno));
    auto fetch = std::bind(fetch_partition, _1, borrowed_fd(output.Fd()), fb);
    do_for_partitions(partition, slot_override, fetch, false /* force slot */);
    if (!output.Commit()) die("Cannot publish fetched file: %s", strerror(errno));''')
# A fetch range must return exactly the requested bytes, not a shorter DATA
# followed by OKAY or an oversized response that could corrupt the next range.
edit('vendor/core/fastboot/fastboot_driver.cpp', '''    RetCode ret = RunAndReadBuffer(cmd, response, info, [&](const char* data, uint64_t size) {
        if (!android::base::WriteFully(fd, data, size)) {''', '''    uint64_t received = 0;
    const int64_t expected_size = size;
    RetCode ret = RunAndReadBuffer(cmd, response, info, [&](const char* data, uint64_t size) {
        if (expected_size >= 0 && (received > static_cast<uint64_t>(expected_size) ||
            size > static_cast<uint64_t>(expected_size) - received)) {
            error_ = "Fetch DATA exceeds requested range";
            return BAD_DEV_RESP;
        }
        received += size;
        if (!android::base::WriteFully(fd, data, size)) {''')
edit('vendor/core/fastboot/fastboot_driver.cpp', '''    epilog_(ret);
    return ret;
}

// Helpers''', '''    if (ret == SUCCESS && expected_size >= 0 && received != static_cast<uint64_t>(expected_size)) {
        error_ = "Fetch DATA does not match requested range";
        ret = BAD_DEV_RESP;
    }
    epilog_(ret);
    return ret;
}

// Helpers''')

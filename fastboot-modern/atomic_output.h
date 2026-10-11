// Local output transaction for downloaded data. Also removes partial files when
// the AOSP CLI's fatal error callback exits before stack unwinding.
#pragma once
#include <algorithm>
#include <cstdlib>
#include <string>
#include <vector>
#include <unistd.h>
#include <cstdio>

class KirinAtomicOutput {
    std::string destination_, temporary_;
    int fd_ = -1;
    static std::vector<std::string>& Files() {
        static std::vector<std::string> files;
        return files;
    }
    static void Cleanup() { for (const auto& file : Files()) unlink(file.c_str()); }
    void Forget() {
        auto& files = Files();
        files.erase(std::remove(files.begin(), files.end(), temporary_), files.end());
    }
public:
    explicit KirinAtomicOutput(const std::string& destination) : destination_(destination) {
        auto& files = Files();
        static bool registered = [] { std::atexit(Cleanup); return true; }();
        (void)registered;
        temporary_ = destination + ".partial.XXXXXX";
        fd_ = mkstemp(temporary_.data());
        if (fd_ >= 0) files.push_back(temporary_);
        else temporary_.clear();
    }
    KirinAtomicOutput(const KirinAtomicOutput&) = delete;
    KirinAtomicOutput& operator=(const KirinAtomicOutput&) = delete;
    ~KirinAtomicOutput() {
        if (fd_ >= 0) close(fd_);
        if (fd_ >= 0 || !temporary_.empty()) { Forget(); unlink(temporary_.c_str()); }
    }
    int Fd() const { return fd_; }
    bool Commit() {
        if (fsync(fd_)) return false;
        int result = close(fd_);
        fd_ = -1;
        if (result || rename(temporary_.c_str(), destination_.c_str())) return false;
        Forget(); temporary_.clear();
        return true;
    }
};

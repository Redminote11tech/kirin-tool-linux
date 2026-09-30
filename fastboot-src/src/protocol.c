/*
 * Copyright (C) 2008 The Android Open Source Project
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *  * Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 *  * Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in
 *    the documentation and/or other materials provided with the
 *    distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
 * FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
 * COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS
 * OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
 * SUCH DAMAGE.
 */

#define min(a, b) \
    ({ typeof(a) _a = (a); typeof(b) _b = (b); (_a < _b) ? _a : _b; })
#define round_down(a, b) \
    ({ typeof(a) _a = (a); typeof(b) _b = (b); _a - (_a % _b); })

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <stdint.h>
#include <inttypes.h>
#include <unistd.h>

#include <sparse/sparse.h>

#include "fastboot.h"

static char ERROR[128];

char *fb_get_error(void)
{
    return ERROR;
}

static int check_response(usb_handle *usb, unsigned int size, char *response)
{
    unsigned char status[65];
    int r;

    for(;;) {
        r = usb_read(usb, status, 64);
        if(r < 0) {
            sprintf(ERROR, "status read failed (%s)", strerror(errno));
            usb_close(usb);
            return -1;
        }
        status[r] = 0;

        if(r < 4) {
            sprintf(ERROR, "status malformed (%d bytes)", r);
            usb_close(usb);
            return -1;
        }

        if(!memcmp(status, "INFO", 4)) {
            fprintf(stderr,"(bootloader) %s\n", status + 4);
            continue;
        }

        if(!memcmp(status, "OKAY", 4)) {
            if(response) {
                strcpy(response, (char*) status + 4);
            }
            return 0;
        }

        if(!memcmp(status, "FAIL", 4)) {
            if(r > 4) {
                sprintf(ERROR, "remote: %s", status + 4);
            } else {
                strcpy(ERROR, "remote failure");
            }
            return -1;
        }

        if(!memcmp(status, "DATA", 4) && size > 0){
            unsigned dsize = strtoul((char*) status + 4, 0, 16);
            if(dsize > size) {
                strcpy(ERROR, "data size too large");
                usb_close(usb);
                return -1;
            }
            return dsize;
        }

        strcpy(ERROR,"unknown status code");
        usb_close(usb);
        break;
    }

    return -1;
}

static int _command_start(usb_handle *usb, const char *cmd, unsigned size,
                          char *response)
{
    int cmdsize = strlen(cmd);

    if(response) {
        response[0] = 0;
    }

    if(cmdsize > 64) {
        sprintf(ERROR,"command too large");
        return -1;
    }

    if(usb_write(usb, cmd, cmdsize) != cmdsize) {
        sprintf(ERROR,"command write failed (%s)", strerror(errno));
        usb_close(usb);
        return -1;
    }

    return check_response(usb, size, response);
}

static int _command_data(usb_handle *usb, const void *data, unsigned size)
{
    int r;

    r = usb_write(usb, data, size);
    if(r < 0) {
        sprintf(ERROR, "data transfer failure (%s)", strerror(errno));
        usb_close(usb);
        return -1;
    }
    if(r != ((int) size)) {
        sprintf(ERROR, "data transfer failure (short transfer)");
        usb_close(usb);
        return -1;
    }

    return r;
}

static int _command_end(usb_handle *usb)
{
    int r;
    r = check_response(usb, 0, 0);
    if(r < 0) {
        return -1;
    }
    return 0;
}

static int _command_send(usb_handle *usb, const char *cmd,
                         const void *data, unsigned size,
                         char *response)
{
    int r;
    if (size == 0) {
        return -1;
    }

    r = _command_start(usb, cmd, size, response);
    if (r < 0) {
        return -1;
    }

    r = _command_data(usb, data, size);
    if (r < 0) {
        return -1;
    }

    r = _command_end(usb);
    if(r < 0) {
        return -1;
    }

    return size;
}

static int _command_send_no_data(usb_handle *usb, const char *cmd,
                                 char *response)
{
    return _command_start(usb, cmd, 0, response);
}

int fb_command(usb_handle *usb, const char *cmd)
{
    return _command_send_no_data(usb, cmd, 0);
}

int fb_command_response(usb_handle *usb, const char *cmd, char *response)
{
    return _command_send_no_data(usb, cmd, response);
}

/* Vendor read-only storage upload, reconstructed from the supplied pre-license-
 * change Windows clients. See docs/flashing-safety.md for binary provenance.
 * getvar:{emmc,storage}:partition -> 16 hex offset, separator, 16 hex length.
 * upload_{emmc,storage}:offset:length -> OKAY, exact raw bytes, OKAY.
 * The local filename is never sent to the device.
 */
static int parse_hex16(const char *s, uint64_t *value)
{
    uint64_t n = 0;
    for (int i = 0; i < 16; ++i) {
        unsigned char c = s[i];
        unsigned d;
        if (c >= '0' && c <= '9') d = c - '0';
        else if (c >= 'a' && c <= 'f') d = c - 'a' + 10;
        else if (c >= 'A' && c <= 'F') d = c - 'A' + 10;
        else return -1;
        n = (n << 4) | d;
    }
    *value = n;
    return 0;
}

int fb_dump_partition(usb_handle *usb, const char *kind, const char *partition, const char *filename)
{
    char command[65], response[65];
    uint64_t address, length;
    int result = -1;
    FILE *fp = NULL;
    char *temporary = NULL;
    unsigned char *buffer = NULL;
    if (strcmp(kind, "emmc") && strcmp(kind, "storage")) {
        snprintf(ERROR, sizeof(ERROR), "unsupported upload kind"); return -1;
    }
    size_t pn = strlen(partition);
    if (!pn || pn > 32 || strspn(partition, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-") != pn) {
        snprintf(ERROR, sizeof(ERROR), "invalid partition name"); return -1;
    }
    snprintf(command, sizeof(command), "getvar:%s:%s", kind, partition);
    if (fb_command_response(usb, command, response) < 0) {
        fprintf(stderr, "KIRIN_DUMP_QUERY_FAILED\n");
        return -1;
    }
    if (strlen(response) != 33 || (response[16] != ':' && response[16] != ' ') ||
        parse_hex16(response, &address) || parse_hex16(response + 17, &length) ||
        !length || length > UINT64_MAX - address) {
        snprintf(ERROR, sizeof(ERROR), "invalid vendor partition offset/length"); return -1;
    }
    if (strlen(filename) > SIZE_MAX - 16) return -1;
    temporary = calloc(strlen(filename) + 16, 1);
    buffer = malloc(1024 * 1024);
    if (!temporary || !buffer) {
        snprintf(ERROR, sizeof(ERROR), "out of memory"); goto out;
    }
    sprintf(temporary, "%s.partial.XXXXXX", filename);
    int fd = mkstemp(temporary);
    if (fd < 0) { snprintf(ERROR, sizeof(ERROR), "cannot create backup: %s", strerror(errno)); goto out; }
    fp = fdopen(fd, "wb");
    if (!fp) { close(fd); snprintf(ERROR, sizeof(ERROR), "cannot open backup stream"); goto out; }
    for (uint64_t copied = 0; copied < length;) {
        uint64_t request = length - copied;
        if (request > 16 * 1024 * 1024) request = 16 * 1024 * 1024;
        snprintf(command, sizeof(command), "upload_%s:%016" PRIx64 ":%016" PRIx64, kind, address + copied, request);
        if (_command_start(usb, command, 0, NULL) < 0) goto out;
        uint64_t remaining = request;
        while (remaining) {
            int count = remaining > 1024 * 1024 ? 1024 * 1024 : (int)remaining;
            int read = usb_read(usb, buffer, count);
            if (read <= 0 || read > count) {
                snprintf(ERROR, sizeof(ERROR), "short/failed vendor upload"); goto out;
            }
            if (fwrite(buffer, 1, read, fp) != (size_t)read) {
                snprintf(ERROR, sizeof(ERROR), "backup write failed: %s", strerror(errno)); goto out;
            }
            remaining -= read;
        }
        if (_command_end(usb) < 0) goto out;
        copied += request;
    }
    if (fflush(fp) || fsync(fileno(fp))) {
        snprintf(ERROR, sizeof(ERROR), "backup flush failed: %s", strerror(errno)); goto out;
    }
    if (fclose(fp)) {
        fp = NULL; snprintf(ERROR, sizeof(ERROR), "backup close failed: %s", strerror(errno)); goto out;
    }
    fp = NULL;
    if (rename(temporary, filename)) {
        snprintf(ERROR, sizeof(ERROR), "cannot publish backup: %s", strerror(errno)); goto out;
    }
    fprintf(stderr, "KIRIN_DUMP_OK bytes=%" PRIu64 "\n", length);
    result = 0;
out:
    if (fp) fclose(fp);
    if (temporary) { if (result) unlink(temporary); free(temporary); }
    free(buffer);
    return result;
}

int fb_download_data(usb_handle *usb, const void *data, unsigned size)
{
    char cmd[64];
    int r;

    sprintf(cmd, "download:%08x", size);
    r = _command_send(usb, cmd, data, size, 0);

    if(r < 0) {
        return -1;
    } else {
        return 0;
    }
}

#define USB_BUF_SIZE 1024
static char usb_buf[USB_BUF_SIZE];
static int usb_buf_len;

static int fb_download_data_sparse_write(void *priv, const void *data, int len)
{
    int r;
    usb_handle *usb = priv;
    int to_write;
    const char *ptr = data;

    if (usb_buf_len) {
        to_write = min(USB_BUF_SIZE - usb_buf_len, len);

        memcpy(usb_buf + usb_buf_len, ptr, to_write);
        usb_buf_len += to_write;
        ptr += to_write;
        len -= to_write;
    }

    if (usb_buf_len == USB_BUF_SIZE) {
        r = _command_data(usb, usb_buf, USB_BUF_SIZE);
        if (r != USB_BUF_SIZE) {
            return -1;
        }
        usb_buf_len = 0;
    }

    if (len > USB_BUF_SIZE) {
        if (usb_buf_len > 0) {
            sprintf(ERROR, "internal error: usb_buf not empty\n");
            return -1;
        }
        to_write = round_down(len, USB_BUF_SIZE);
        r = _command_data(usb, ptr, to_write);
        if (r != to_write) {
            return -1;
        }
        ptr += to_write;
        len -= to_write;
    }

    if (len > 0) {
        if (len > USB_BUF_SIZE) {
            sprintf(ERROR, "internal error: too much left for usb_buf\n");
            return -1;
        }
        memcpy(usb_buf, ptr, len);
        usb_buf_len = len;
    }

    return 0;
}

static int fb_download_data_sparse_flush(usb_handle *usb)
{
    int r;

    if (usb_buf_len > 0) {
        r = _command_data(usb, usb_buf, usb_buf_len);
        if (r != usb_buf_len) {
            return -1;
        }
        usb_buf_len = 0;
    }

    return 0;
}

int fb_download_data_sparse(usb_handle *usb, struct sparse_file *s)
{
    char cmd[64];
    int r;
    int size = sparse_file_len(s, true, false);
    if (size <= 0) {
        return -1;
    }

    sprintf(cmd, "download:%08x", size);
    r = _command_start(usb, cmd, size, 0);
    if (r < 0) {
        return -1;
    }

    r = sparse_file_callback(s, true, false, fb_download_data_sparse_write, usb);
    if (r < 0) {
        return -1;
    }

    r = fb_download_data_sparse_flush(usb);
    if (r < 0) {
        return -1;
    }

    return _command_end(usb);
}

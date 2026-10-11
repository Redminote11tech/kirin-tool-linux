# Mate 40 Pro 4.2.0.196 bootloader inspection

Read-only inspection of the user-provided NOH-AN00 base.UPDATE.APP, October 11,
2026. Extracted FASTBOOT, BL2 and XLOADER into private audit files, preserving the
archive. No phone operation, image modification, resizing, decryption-key guess or
new rescue-partition mapping was performed. Firmware dumps are not redistributed.

## What is established

| UPDATE.APP entry | Exact extracted bytes | SHA256 |
| --- | ---: | --- |
| FASTBOOT | 4,135,104 | `ff75969a6b8acc5d1a3437aa71aaed146ce606043688af38f45e6a239f1f63e7` |
| BL2 | 466,048 | `a0b287f990ee5cb61903d351ef2fe6cdeadfb097433c9d36cd3fc110b667eb1f` |
| XLOADER | 493,568 | `795badc3705c810e770133be9c43ed3bca9b9a561773ab5992e37af95d8f1eb6` |

The partition names and repeated security/container wrapper headers are readable.
FASTBOOT and BL2 repeat three 716-byte wrapper records, with marker `0xe291f358`
at offsets 128, 844 and 1560. XLOADER contains additional wrapped stages. Raw fields
are recorded privately without assigning undocumented encryption algorithm/key
semantics to them.

The main payload regions have near-uniform byte distributions (~7.997 bits/byte
in typical 64-KiB blocks) and no readable rescue/partition/UltraFlash handler names
or error strings in ASCII or UTF-16LE. Candidate compression signatures were tested:
no valid zlib stream, and the two FASTBOOT gzip candidates fail decompression.
An ELF-like marker in XLOADER is part of a wrapper region and fails normal ELF header
validation; it is not a recovered executable that can be usefully disassembled.
These observations strongly indicate protected/encrypted code. They do not identify
an encryption key or prove its derivation scheme.

This is consistent with [TASZK's firsthand firmware analysis](https://labs.taszk.io/articles/post/huawei_kirin990_bootrom_patch/),
which explains that their meaningful xloader/fastboot/BL2 comparisons required
firmware decryption using a controlled device of an equivalent chipset. That study
also reports Kirin 9000 BootROM/xloader fixes before commercial release; it does not
supply a Mate 40 Pro decryption key or plaintext bootloader.

## Recovery lengths already recoverable from this archive

The HISIUFS_GPT header and partition-entry CRCs validate. Both applicable table
variants agree on the recovery geometry when interpreted with their matching LBA
units (512 and 4096 bytes, inferred by exact image-size agreement):

| Actual GPT partition | Partition bytes | UPDATE.APP image bytes |
| --- | ---: | ---: |
| erecovery | 47,185,920 (45 MiB) | 47,185,920 |
| erecovery_ramdisk | 33,554,432 (32 MiB) | 33,554,432 |
| erecovery_vendor | 25,165,824 (24 MiB) | 25,165,824 |
| erecovery_vbmeta | 2,097,152 (2 MiB) | 413,696 |

The three recovery images already exactly match their ordinary GPT partitions.
The log failures concern `rescue_recovery`, `rescue_recovery_ramdisk` and
`rescue_recovery_vendor`. Those names are not in this GPT. A bootloader could
implement virtual aliases, so their absence alone does not prove unavailability.
No expected length for those aliases has been recovered from plaintext code.

`partition length get error` is a device reply at the write stage. It does not,
by itself, report a numerical required length or establish an image-size mismatch.
Changing/padding the images or replacing rescue targets with ordinary eRecovery
names would therefore be an unsupported inference, and was not implemented.

## Logging correction

2.4.3's native UltraFlash fallback received the device's FAIL text into
`fb_get_error()` but printed only a generic refusal. 2.4.4 prints that exact error
before fallback, for example:

```text
UltraFlash refused (remote: Command not allowed); using standard flashing
```

That is a synthetic tested example, not a recovered reply from the earlier phone
session. The previous generic log cannot reconstruct the discarded response.
The change preserves the existing fallback protocol and does not make the device
accept UltraFlash. Hardware-free CLI tests cover multiple reasons and bare FAIL.

## Remaining limit

A useful reverse engineering of the actual FASTBOOT lookup routine needs a
plaintext/decrypted matching bootloader, or device-side diagnostic evidence that
exposes the partition map. The supplied protected payloads alone have not yielded
that routine. No Kirin 9000 factory loader or old-chip exploit is inferred from this
inspection, and no firmware key or live device dump is currently available here.

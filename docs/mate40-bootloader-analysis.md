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
FASTBOOT and BL2 contain three wrapper/certificate starts at offsets 0, 716 and
1432, with marker `0xe291f358` at offsets 128, 844 and 1560. The verified certificate
sizes are 716, 716 and 732 bytes respectively (the original inspection incorrectly
described the third record as another 716-byte record). XLOADER contains additional wrapped stages. Raw fields
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

## October 11 follow-up: discovering permitted commands

Rechecked the same FASTBOOT hash against plausible LZMA-alone headers: six
candidates, no complete valid stream. Targeted ASCII, UTF-16LE and UTF-16BE scans
found none of `Command not allowed`, `partition length get error`,
`rescue_recovery`, `erecovery`, `getvar`, `ultraflash`, `get-psid` or `ptable`.
Every complete 4-KiB block starting at offset 4096 had entropy at least 7.5
bits/byte. These findings reinforce the protected-payload inference; they do
not establish a specific cipher or recover plaintext instructions.

The connected NOH-AN00 accepted `oem get-product-model`. All six standard
`partition-size:` queries for the rescue aliases and ordinary eRecovery names,
and `getvar ptable`, returned `Command not allowed`. Queries were read-only; no
flashing, unlock, UltraFlash negotiation or reboot was performed.

A complete permission analysis needs plaintext FASTBOOT and, if checks are
delegated, the matching BL2. The reverse-engineering targets would be the command
dispatcher, its denial path, per-command lock/authentication/mode checks, the
partition-name resolver, and the call producing the length-lookup error. Finding
a command string alone would not prove permission in the phone's current state.
The read-only query results establish a partial observed allow/deny set, not the
complete policy. No matching plaintext bootloader or functioning Kirin 9000
decryption route has been established in this investigation.

[Impalabs' HEXACON 2022 slides, pages 11–13](https://2022.hexacon.fr/slides/22-Hexacon-Hara-Kirin_Dissecting_the_Privileged_Components_of_Huawei_Mobile_Devices.pdf)
describe device-assisted firmware decryption on earlier Kirin devices and a split
FASTBOOT/BL2 architecture on Kirin 990. This is background evidence, not proof of
the Mate 40 Pro's exact implementation or a usable Kirin 9000 exploit.

## Further search: Mate 40-specific decryption research

[Core Escalation, Black Hat USA 2023](https://i.blackhat.com/BH-US-23/Presentations/US-23-Wen-Core-Escalation.pdf)
specifically targets Mate 40/Kirin 9000 firmware decryption. Its device-assisted
route establishes a decryption oracle after cross-core privilege escalation and
TEEOS modification. This corrects the earlier search's focus on older Kirins:
Mate 40 decryption has been demonstrated. No applicable offline decoder or
ready-to-run matching implementation was found, and support for the supplied
4.2.0.196 build is unverified.

Local Binwalk scans of FASTBOOT and BL2 found no recognized embedded signatures.
The Kaitai Huawei packed-bootloader specification describes the Nexus 6P
meta-image header, which does not match these files. UPDATE.APP splitters extract
the images but do not establish payload decryption.

## Verified container reverse engineering

Further analysis of both supplied images recovered and cryptographically verified
the certificate layout. This is reverse engineering of the container, not a
decompilation of the protected executable payload.

| Region | Start | Size | Verified interpretation |
| --- | ---: | ---: | --- |
| Key certificate 0 | `0x000` | 716 | 460 signed bytes followed by 256-byte RSA signature |
| Key certificate 1 | `0x2cc` | 716 | 460 signed bytes followed by 256-byte RSA signature |
| Content certificate | `0x598` | 732 | 476 signed bytes followed by 256-byte RSA signature |
| Post-certificate words | `0x874` | 12 | Eight zero bytes followed by payload word count |
| Zero padding | `0x880` | 1920 | All zero through `0x1000` |
| Protected payload | `0x1000` | Variable | Exact stored SHA256 match in both files |

For each certificate, the low16 bits of the little-endian field at relative
`0x88`, multiplied by four, give its signed byte count. The RSA modulus is the
256-byte big-endian value at relative`0x90`; the following20 bytes match the
N-prime parameter area of the public CryptoCell structure. The exponent65537
was verified by successful signature verification, not guessed from headerflags.
The256-byte signature is stored as a little-endian integer and verifies after
byte reversal using RSA-PSS, SHA256, MGF1-SHA256 and32-byte salt. All three
signatures verify independently for each image: six signatures total.

The first two certificates store32-byte hashes at relative`0x1ac`. Each equals
SHA256 of the next certificate's276-byte N/Np blob. Both images use the same
three public-key blobs. This verifies the embedded key-chain relationship, but
not the root key's correspondence to the live phone's fused trust anchor.
These are signing public keys; no AES decryption key was recovered.

The content certificate stores SHA256 of the protected payload at absolute
`0x74c`. The payload-length field at`0x87c` is a32-bit little-endian word count.
The formula`0x1000 + 4 * word_count` exactly equals each file size:

| Image | Word count | Protected payload bytes | Total file bytes |
| --- | ---: | ---: | ---: |
| FASTBOOT | 1,032,752 | 4,131,008 | 4,135,104 |
| BL2 | 115,488 | 461,952 | 466,048 |

These are bootloader-container payload lengths, not rescue-partition lengths.
They cannot solve the earlier partition-lookup error. The8-byte field at`0x744`
varies between images and resembles the nonce field in related CryptoCell
content-certificate formats; its exact Huawei semantics remain unverified.
The cipher/key-selection flags are not assigned undocumented meanings here.

A private reproducible parser verifies the six signatures, chain hashes, payload
hashes and file-size equations, then optionally carves the protected payloads.
Corrupting a signed-header byte, payload byte or length byte causes rejection.
Protected payload entropy is7.999959 and7.999581 bits/byte respectively; no
repeated16-byte blocks or targeted plaintext command strings were recovered.
The carve is not decryption, and no executable handler addresses or permitted
command table can be honestly reported from it.

Reference: [published CryptoCell secure-boot structure definitions](https://coral.googlesource.com/tf-a-mtk/+/4129340717c215f3cc3841e32ed0b6cd5baf7ffb/include/drivers/arm/cryptocell/712/secureboot_gen_defs.h).
Related certificate formats explain the N/Np and RSA-PSS layout; compatibility
was established by actual verification against these images.

## Encryption metadata and the existing loader decoder

Compared the signed fields with Arm's published certificate generator and verifier
at [CryptoCell-312 runtime commit 91539d6](https://github.com/ARM-software/cryptocell-312-runtime/tree/91539d62a67662e40e7d925694e55bbc7e679f84).
That related implementation uses AES-CTR, builds the IV from a nonce and load
address, and takes the encryption key as a separate input. It is not a matching
decoder for the supplied Huawei format: interpreting `0x000501ff` using its flag
positions yields five images and AES key selector 15, whereas the Huawei signed
region contains one payload hash. The newer certificate also has different
public-key and record sizes. Consequently, its flag meanings and IV construction
must not be copied into a purported Huawei decryptor.

Both supplied content certificates have eight raw bytes `0200000000000000`
at `0x73c`, an image-specific eight-byte nonce candidate at `0x744`, and eight
`ff` bytes after the payload hash at `0x76c`. The unsigned area at `0x874`
contains eight zero bytes followed by the verified word count. These observations
are recorded without claiming a decoded load address, cipher selector or key
derivation. RSA public-key verification does not recover an AES key.

The app's existing `CryptoUtil.DTL` reads a **two-byte big-endian** encrypted-key
length, an RSA-OAEP-SHA256 encrypted key, a 16-byte IV, and AES-CBC/PKCS7 data.
The bundled `.ktl` reference files have a 256-byte encrypted-key envelope and
block-aligned ciphertext. Both supplied OTA images instead start with a zero
encrypted-key length and do not match that envelope. DTL is therefore not a
direct OTA bootloader decoder. No factory loader was reused on the phone.

The concrete missing input remains a matching plaintext FASTBOOT/BL2 pair or a
working way to decrypt these protected payloads. The Mate 40 research provides
a device-assisted lead, not an established decoder for HarmonyOS 4.2.0.196.
Until that input exists, neither the complete command permission table nor the
rescue-alias length-lookup routine has been recovered. This follow-up made no
phone operation and no change to the flashing implementation.

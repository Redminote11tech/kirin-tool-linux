import os, subprocess
cases = [
    (["oem", "lock-state", "info"], "oem lock-state info", "OKAY", 0),
    (["oem", "oeminforead-BASE_VERSION"], "oem oeminforead-BASE_VERSION", "OKAY", 0),
    (["oem", "get-build-number"], "oem get-build-number", "OKAY", 0),
    (["oem", "dump-emmc", "oeminfo", "tests/output/cli dump.img"], "getvar:emmc:oeminfo", "FAILmock query refused", 1),
    (["oem", "dump-storage", "oeminfo", "tests/output/cli dump.img"], "getvar:storage:oeminfo", "FAILmock query refused", 1),
    (["getvar", "nve:SN"], "getvar:nve:SN", "OKAY", 0),
]
for args, wire, response, code in cases:
    p = subprocess.run(["tests/cli-fastboot", *args], env={**os.environ, "EXPECT_COMMAND":wire, "MOCK_RESPONSE":response}, capture_output=True, text=True, timeout=5)
    assert p.returncode == code and "WIRE_MATCH" in p.stderr and "usage:" not in p.stderr, (args, p.returncode, p.stderr)
    print("PASS full CLI dispatch", " ".join(args[:2]))

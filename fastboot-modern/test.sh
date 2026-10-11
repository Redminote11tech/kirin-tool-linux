#!/usr/bin/env bash
# All test binaries exclude the native USB backend or use MockTransport.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="${KIRIN_FASTBOOT_BUILD_DIR:-$HERE/.build}"
SOURCE="$WORK/source/android-tools-35.0.2"
if [[ ! -f "$SOURCE/CMakeLists.txt" ]]; then
    echo 'Build with fastboot-modern/build.sh first; install Google Test/Mock development packages.' >&2
    exit 1
fi
python3 "$HERE/prepare.py" "$SOURCE"
cmake -S "$SOURCE" -B "$WORK/build" \
    -DKIRIN_PARITY_TEST_SOURCE="$HERE/huawei_vendor_test.cpp" \
    -DKIRIN_PARITY_CLI_MOCK="$HERE/cli_mock_transport.cpp"
cmake --build "$WORK/build" --target kirin_driver_tests kirin_workflow_tests kirin_cli_tests -j "${KIRIN_BUILD_JOBS:-4}"
"$WORK/build/vendor/kirin_driver_tests"
"$WORK/build/vendor/kirin_workflow_tests"
python3 "$HERE/cli_tests.py" "$WORK/build/vendor/kirin_cli_tests"

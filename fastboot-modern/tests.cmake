find_package(GTest REQUIRED)
# Ubuntu 20.04's FindGTest uses the older imported target names.
if(NOT TARGET GTest::gtest_main)
    add_library(GTest::gtest_main INTERFACE IMPORTED)
    target_link_libraries(GTest::gtest_main INTERFACE GTest::Main)
endif()
if(NOT TARGET GTest::gmock)
    find_library(KIRIN_GMOCK_LIBRARY NAMES gmock)
    if(NOT KIRIN_GMOCK_LIBRARY)
        message(FATAL_ERROR "Install the Google Mock development package")
    endif()
    add_library(GTest::gmock UNKNOWN IMPORTED)
    set_target_properties(GTest::gmock PROPERTIES IMPORTED_LOCATION "${KIRIN_GMOCK_LIBRARY}")
endif()
add_executable(kirin_driver_tests
    core/fastboot/fastboot_driver.cpp
    core/fastboot/fastboot_driver_test.cpp
    core/fastboot/huawei_vendor.cpp
    ${KIRIN_PARITY_TEST_SOURCE})
target_include_directories(kirin_driver_tests PRIVATE
    core/fastboot libbase/include core/include core/libsparse/include
    core/fs_mgr/libstorage_literals mkbootimg/include/bootimg)
target_link_libraries(kirin_driver_tests PRIVATE libsparse libbase libcutils liblog
    GTest::gtest_main GTest::gmock z Threads::Threads)
set(mock_fastboot_sources ${fastboot_SOURCES})
list(FILTER mock_fastboot_sources EXCLUDE REGEX "usb_(linux|osx)\\.cpp$")
add_executable(kirin_cli_tests ${mock_fastboot_sources} ${KIRIN_PARITY_CLI_MOCK})
get_target_property(modern_includes fastboot INCLUDE_DIRECTORIES)
get_target_property(modern_definitions fastboot COMPILE_DEFINITIONS)
get_target_property(modern_libraries fastboot LINK_LIBRARIES)
target_include_directories(kirin_cli_tests PRIVATE ${modern_includes} core/fastboot)
target_compile_definitions(kirin_cli_tests PRIVATE ${modern_definitions} KIRIN_FAKE_TRANSPORT)
target_link_libraries(kirin_cli_tests PRIVATE ${modern_libraries})
list(FILTER mock_fastboot_sources EXCLUDE REGEX "/main\\.cpp$")
add_executable(kirin_workflow_tests ${mock_fastboot_sources} ${KIRIN_PARITY_CLI_MOCK}
    core/fastboot/fastboot_test.cpp core/fastboot/task_test.cpp)
target_include_directories(kirin_workflow_tests PRIVATE ${modern_includes} core/fastboot)
target_compile_definitions(kirin_workflow_tests PRIVATE ${modern_definitions} KIRIN_FAKE_TRANSPORT)
target_link_libraries(kirin_workflow_tests PRIVATE ${modern_libraries} GTest::gtest_main GTest::gmock)

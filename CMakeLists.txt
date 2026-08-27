# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
# Native C++ targets (revit_usd_export.dll, revit_usd_export_tests.exe).
# repo build invokes this via tools/repoman/cmake_build.py

cmake_minimum_required(VERSION 3.20)

if(MSVC AND NOT CMAKE_VS_PLATFORM_TOOLSET)
    set(CMAKE_VS_PLATFORM_TOOLSET "v143" CACHE STRING "Visual Studio platform toolset")
endif()

project(revit-usd-export CXX)

list(PREPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build configuration" FORCE)
endif()

# ---------------------------------------------------------------------------- cache / options
set(REVIT_VERSION "2024" CACHE STRING "Revit version to bake into REVIT_USD_EXPORT_APP_VERSION")
option(REVIT_USD_EXPORT_BUILD_TESTS "Build revit_usd_export_tests" OFF)
option(REVIT_USD_EXPORT_WITH_PYTHON "Link Python (required by OpenUSD deps)" ON)

set(REVIT_USD_EXPORT_DEPS_ROOT "${CMAKE_SOURCE_DIR}/_build/target-deps" CACHE PATH "Packman target-deps root")
set(REVIT_USD_EXPORT_TBB_ROOT "" CACHE PATH "oneTBB install root")

# Read VERSION.md every configure unless overridden via -DREVIT_USD_EXPORT_PLUGIN_VERSION=...
file(READ "${CMAKE_SOURCE_DIR}/VERSION.md" _plugin_version_raw)
string(REGEX REPLACE "[ \t\r\n]+" "" _plugin_version_from_file "${_plugin_version_raw}")
if(NOT REVIT_USD_EXPORT_PLUGIN_VERSION)
    set(REVIT_USD_EXPORT_PLUGIN_VERSION "${_plugin_version_from_file}")
endif()
message(STATUS "revit-usd-export: plugin version ${REVIT_USD_EXPORT_PLUGIN_VERSION}")

set(REVIT_USD_EXPORT_FMT_ROOT "${REVIT_USD_EXPORT_DEPS_ROOT}/fmt" CACHE PATH "fmt root")

if(NOT REVIT_USD_EXPORT_DOCTEST_INCLUDE_DIR)
    set(REVIT_USD_EXPORT_DOCTEST_INCLUDE_DIR "${REVIT_USD_EXPORT_DEPS_ROOT}/doctest/include" CACHE PATH "doctest include directory")
endif()

if(REVIT_USD_EXPORT_WITH_PYTHON AND NOT Python3_ROOT_DIR)
    set(Python3_ROOT_DIR "${REVIT_USD_EXPORT_DEPS_ROOT}/python" CACHE PATH "Python install root")
endif()

if(REVIT_USD_EXPORT_WITH_PYTHON)
    find_package(Python3 3.12 COMPONENTS Development REQUIRED)
endif()

# Output layout under the cmake build tree.
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/lib")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/lib")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/lib")

include(RevitUsdExportCompilerConfig)
include(RevitUsdExportUSD)

set(REVIT_USD_EXPORT_USD_LIBS
    ar arch gf kind pcp plug python sdf tf usd usdGeom usdLux usdPhysics usdShade usdUtils vt
)

# ---------------------------------------------------------------------------- revit_usd_export (shared library)
file(GLOB REVIT_USD_EXPORT_SOURCES CONFIGURE_DEPENDS "${CMAKE_SOURCE_DIR}/source/cpp/*.cpp")
file(GLOB REVIT_USD_EXPORT_FMT_SOURCES "${REVIT_USD_EXPORT_FMT_ROOT}/src/*.cc")

add_library(revit_usd_export SHARED
    ${REVIT_USD_EXPORT_SOURCES}
    ${REVIT_USD_EXPORT_FMT_SOURCES}
)

target_include_directories(revit_usd_export PRIVATE
    "${CMAKE_SOURCE_DIR}/include"
    "${CMAKE_SOURCE_DIR}/source/cpp"
)
target_include_directories(revit_usd_export SYSTEM PRIVATE
    "${REVIT_USD_EXPORT_FMT_ROOT}/include"
)

target_compile_definitions(revit_usd_export PRIVATE
    revit_usd_export_EXPORTS
    "REVIT_USD_EXPORT_APP_VERSION=\"${REVIT_VERSION}\""
    "REVIT_USD_EXPORT_PLUGIN_VERSION=\"${REVIT_USD_EXPORT_PLUGIN_VERSION}\""
)

target_link_libraries(revit_usd_export PRIVATE revit_usd_export_build_options)
revit_target_link_usd(revit_usd_export ${REVIT_USD_EXPORT_USD_LIBS})

if(MSVC)
    target_compile_options(revit_usd_export PRIVATE
        ${REVIT_USD_EXPORT_USD_MSVC_WARNING_DISABLES}
        /wd4459
    )
endif()

# ---------------------------------------------------------------------------- revit_usd_export_tests (doctest executable)
if(REVIT_USD_EXPORT_BUILD_TESTS)
    file(GLOB REVIT_USD_EXPORT_TEST_SOURCES CONFIGURE_DEPENDS "${CMAKE_SOURCE_DIR}/source/tests/*.cpp")

    add_executable(revit_usd_export_tests
        ${REVIT_USD_EXPORT_TEST_SOURCES}
        "${CMAKE_SOURCE_DIR}/source/cpp/client.cpp"
        "${CMAKE_SOURCE_DIR}/source/cpp/Transcoding.cpp"
    )

    target_include_directories(revit_usd_export_tests PRIVATE
        "${CMAKE_SOURCE_DIR}/source/cpp"
    )
    target_include_directories(revit_usd_export_tests SYSTEM PRIVATE
        "${REVIT_USD_EXPORT_DOCTEST_INCLUDE_DIR}"
    )

    target_link_libraries(revit_usd_export_tests PRIVATE
        revit_usd_export_build_options
        revit_usd_export
    )
    revit_target_link_usd(revit_usd_export_tests tf)

    target_compile_definitions(revit_usd_export_tests PRIVATE
        "$<$<CONFIG:Debug>:TRACE;DEBUG>"
        "$<$<CONFIG:Release>:TRACE>"
    )

    set_target_properties(revit_usd_export_tests PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/tests"
    )

    if(MSVC)
        target_compile_options(revit_usd_export_tests PRIVATE
            ${REVIT_USD_EXPORT_USD_MSVC_WARNING_DISABLES}
            /wd4251
        )
    endif()
endif()

# Runtime staging (CMake install → lib/).
# REVIT_USD_EXPORT_USD_ROOT is set by cmake_build.py to .../usd/release or .../usd/debug.

install(
    TARGETS revit_usd_export
    RUNTIME DESTINATION lib
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

if(REVIT_USD_EXPORT_BUILD_TESTS)
    install(
        TARGETS revit_usd_export_tests
        RUNTIME DESTINATION tests
    )
endif()

# USD shared libraries (exclude revit_usd_export.dll — installed via TARGETS above).
install(
    DIRECTORY "${REVIT_USD_EXPORT_USD_ROOT}/lib/"
    DESTINATION lib
    FILES_MATCHING PATTERN "*.dll"
)

install(DIRECTORY "${REVIT_USD_EXPORT_USD_ROOT}/lib/usd" DESTINATION lib)

install(DIRECTORY "${REVIT_USD_EXPORT_USD_ROOT}/lib/python" DESTINATION lib)

# Packman Python runtime tree — staged post-install by tools/repoman/stage_runtime.py
# (target-deps/python is a packman junction; CMake install cannot dereference it on Windows).

install(
    DIRECTORY "${REVIT_USD_EXPORT_TBB_ROOT}/bin/"
    DESTINATION lib
    FILES_MATCHING PATTERN "tbb12*.dll"
)

install(
    FILES
        "${REVIT_USD_EXPORT_DEPS_ROOT}/python/python312.dll"
    DESTINATION lib
    OPTIONAL
)

install(DIRECTORY "${CMAKE_SOURCE_DIR}/source/resources/" DESTINATION lib/img)

install(DIRECTORY "${CMAKE_SOURCE_DIR}/source/assets/" DESTINATION lib/assets)

install(
    FILES
        "${CMAKE_SOURCE_DIR}/source/config/revit.usd.export.core.toml"
        "${CMAKE_SOURCE_DIR}/source/config/revit.usd.export.client.toml"
    DESTINATION lib/config
)

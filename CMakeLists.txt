# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
# Native C++ targets (usd_exporter_revit.dll, usd_exporter_revit_tests.exe).
# repo build invokes this via tools/repoman/cmake_build.py

cmake_minimum_required(VERSION 3.20)

if(MSVC AND NOT CMAKE_VS_PLATFORM_TOOLSET)
    set(CMAKE_VS_PLATFORM_TOOLSET "v143" CACHE STRING "Visual Studio platform toolset")
endif()

project(usd-exporter-revit CXX)

list(PREPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build configuration" FORCE)
endif()

# ---------------------------------------------------------------------------- cache / options
set(REVIT_VERSION "2024" CACHE STRING "Revit version to bake into USD_EXPORTER_REVIT_APP_VERSION")
option(USD_EXPORTER_REVIT_BUILD_TESTS "Build usd_exporter_revit_tests" OFF)
option(USD_EXPORTER_REVIT_WITH_PYTHON "Link Python (required by OpenUSD deps)" ON)

set(USD_EXPORTER_REVIT_DEPS_ROOT "${CMAKE_SOURCE_DIR}/_build/target-deps" CACHE PATH "Packman target-deps root")
set(USD_EXPORTER_REVIT_TBB_ROOT "" CACHE PATH "oneTBB install root")

# Read VERSION.md every configure unless overridden via -DUSD_EXPORTER_REVIT_VERSION=...
file(READ "${CMAKE_SOURCE_DIR}/VERSION.md" _version_raw)
string(REGEX REPLACE "[ \t\r\n]+" "" _version_from_file "${_version_raw}")
if(NOT USD_EXPORTER_REVIT_VERSION)
    set(USD_EXPORTER_REVIT_VERSION "${_version_from_file}")
endif()
message(STATUS "usd-exporter-revit: version ${USD_EXPORTER_REVIT_VERSION}")

set(USD_EXPORTER_REVIT_FMT_ROOT "${USD_EXPORTER_REVIT_DEPS_ROOT}/fmt" CACHE PATH "fmt root")

if(NOT USD_EXPORTER_REVIT_DOCTEST_INCLUDE_DIR)
    set(USD_EXPORTER_REVIT_DOCTEST_INCLUDE_DIR "${USD_EXPORTER_REVIT_DEPS_ROOT}/doctest/include" CACHE PATH "doctest include directory")
endif()

if(USD_EXPORTER_REVIT_WITH_PYTHON AND NOT Python3_ROOT_DIR)
    set(Python3_ROOT_DIR "${USD_EXPORTER_REVIT_DEPS_ROOT}/python" CACHE PATH "Python install root")
endif()

if(USD_EXPORTER_REVIT_WITH_PYTHON)
    find_package(Python3 3.12 COMPONENTS Development REQUIRED)
endif()

# Output layout under the cmake build tree.
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/lib")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/lib")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/lib")

include(UsdExporterRevitCompilerConfig)
include(UsdExporterRevitUSD)

set(USD_EXPORTER_REVIT_USD_LIBS
    ar arch gf kind pcp plug python sdf tf usd usdGeom usdLux usdPhysics usdShade usdUtils vt
)

# ---------------------------------------------------------------------------- usd_exporter_revit (shared library)
file(GLOB USD_EXPORTER_REVIT_SOURCES CONFIGURE_DEPENDS "${CMAKE_SOURCE_DIR}/source/cpp/*.cpp")
file(GLOB USD_EXPORTER_REVIT_FMT_SOURCES "${USD_EXPORTER_REVIT_FMT_ROOT}/src/*.cc")

add_library(usd_exporter_revit SHARED
    ${USD_EXPORTER_REVIT_SOURCES}
    ${USD_EXPORTER_REVIT_FMT_SOURCES}
)

target_include_directories(usd_exporter_revit PRIVATE
    "${CMAKE_SOURCE_DIR}/include"
    "${CMAKE_SOURCE_DIR}/source/cpp"
)
target_include_directories(usd_exporter_revit SYSTEM PRIVATE
    "${USD_EXPORTER_REVIT_FMT_ROOT}/include"
)

target_compile_definitions(usd_exporter_revit PRIVATE
    usd_exporter_revit_EXPORTS
    "USD_EXPORTER_REVIT_APP_VERSION=\"${REVIT_VERSION}\""
    "USD_EXPORTER_REVIT_VERSION=\"${USD_EXPORTER_REVIT_VERSION}\""
)

target_link_libraries(usd_exporter_revit PRIVATE usd_exporter_revit_build_options)
usd_exporter_revit_target_link_usd(usd_exporter_revit ${USD_EXPORTER_REVIT_USD_LIBS})

if(MSVC)
    target_compile_options(usd_exporter_revit PRIVATE
        ${USD_EXPORTER_REVIT_USD_MSVC_WARNING_DISABLES}
        /wd4459
    )
endif()

# ---------------------------------------------------------------------------- usd_exporter_revit_tests (doctest executable)
if(USD_EXPORTER_REVIT_BUILD_TESTS)
    file(GLOB USD_EXPORTER_REVIT_TEST_SOURCES CONFIGURE_DEPENDS "${CMAKE_SOURCE_DIR}/source/tests/*.cpp")

    add_executable(usd_exporter_revit_tests
        ${USD_EXPORTER_REVIT_TEST_SOURCES}
        "${CMAKE_SOURCE_DIR}/source/cpp/client.cpp"
        "${CMAKE_SOURCE_DIR}/source/cpp/Transcoding.cpp"
    )

    target_include_directories(usd_exporter_revit_tests PRIVATE
        "${CMAKE_SOURCE_DIR}/source/cpp"
    )
    target_include_directories(usd_exporter_revit_tests SYSTEM PRIVATE
        "${USD_EXPORTER_REVIT_DOCTEST_INCLUDE_DIR}"
    )

    target_link_libraries(usd_exporter_revit_tests PRIVATE
        usd_exporter_revit_build_options
        usd_exporter_revit
    )
    usd_exporter_revit_target_link_usd(usd_exporter_revit_tests tf)

    target_compile_definitions(usd_exporter_revit_tests PRIVATE
        "$<$<CONFIG:Debug>:TRACE;DEBUG>"
        "$<$<CONFIG:Release>:TRACE>"
    )

    set_target_properties(usd_exporter_revit_tests PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out/tests"
    )

    if(MSVC)
        target_compile_options(usd_exporter_revit_tests PRIVATE
            ${USD_EXPORTER_REVIT_USD_MSVC_WARNING_DISABLES}
            /wd4251
        )
    endif()
endif()

# Runtime staging (CMake install → lib/).
# USD_EXPORTER_REVIT_USD_ROOT is set by cmake_build.py to .../usd/release or .../usd/debug.

install(
    TARGETS usd_exporter_revit
    RUNTIME DESTINATION lib
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

if(USD_EXPORTER_REVIT_BUILD_TESTS)
    install(
        TARGETS usd_exporter_revit_tests
        RUNTIME DESTINATION tests
    )
endif()

# USD shared libraries (exclude usd_exporter_revit.dll — installed via TARGETS above).
install(
    DIRECTORY "${USD_EXPORTER_REVIT_USD_ROOT}/lib/"
    DESTINATION lib
    FILES_MATCHING PATTERN "*.dll"
)

install(DIRECTORY "${USD_EXPORTER_REVIT_USD_ROOT}/lib/usd" DESTINATION lib)

install(DIRECTORY "${USD_EXPORTER_REVIT_USD_ROOT}/lib/python" DESTINATION lib)

# Packman Python runtime tree — staged post-install by tools/repoman/stage_runtime.py
# (target-deps/python is a packman junction; CMake install cannot dereference it on Windows).

install(
    DIRECTORY "${USD_EXPORTER_REVIT_TBB_ROOT}/bin/"
    DESTINATION lib
    FILES_MATCHING PATTERN "tbb12*.dll"
)

install(
    FILES
        "${USD_EXPORTER_REVIT_DEPS_ROOT}/python/python312.dll"
    DESTINATION lib
    OPTIONAL
)

install(DIRECTORY "${CMAKE_SOURCE_DIR}/source/resources/" DESTINATION lib/img)

install(DIRECTORY "${CMAKE_SOURCE_DIR}/source/assets/" DESTINATION lib/assets)

install(
    FILES
        "${CMAKE_SOURCE_DIR}/source/config/usd.exporter.revit.core.toml"
        "${CMAKE_SOURCE_DIR}/source/config/usd.exporter.revit.client.toml"
    DESTINATION lib/config
)

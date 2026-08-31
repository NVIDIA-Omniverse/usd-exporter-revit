# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
# Usage requirements for compiling against OpenUSD, exposed as the INTERFACE target
# `usd_exporter_revit_build_options`. Native targets link it PRIVATE.

include_guard(GLOBAL)

add_library(usd_exporter_revit_build_options INTERFACE)

target_compile_features(usd_exporter_revit_build_options INTERFACE cxx_std_17)

set(_msvc "$<CXX_COMPILER_ID:MSVC>")
set(_debug "$<CONFIG:Debug>")

target_compile_definitions(usd_exporter_revit_build_options INTERFACE
    TBB_SUPPRESS_DEPRECATED_MESSAGES
    "$<${_debug}:TBB_USE_DEBUG=1>"
)

if(WIN32)
    target_compile_definitions(usd_exporter_revit_build_options INTERFACE NOMINMAX)
endif()

target_compile_options(usd_exporter_revit_build_options INTERFACE
    "$<${_msvc}:/utf-8;/bigobj;/EHsc;/GR>"
)

# MSVC warning disables for USD-consuming targets (historical parity with legacy build).
set(USD_EXPORTER_REVIT_USD_MSVC_WARNING_DISABLES
    /wd4003
    /wd4100
    /wd4005
    /wd4127
    /wd4201
    /wd4305
    /wd4244
    /wd4267
    /wd4275
    /wd4996
)

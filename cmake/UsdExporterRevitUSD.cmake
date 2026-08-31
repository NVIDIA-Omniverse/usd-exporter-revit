# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
# OpenUSD discovery for usd-exporter-revit. Does not use pxrConfig.cmake; instead adds
# `<usd_root>/include` as a SYSTEM include and links individual USD libraries from `<usd_root>/lib`.
#
# Inputs: USD_EXPORTER_REVIT_USD_ROOT or a CMAKE_PREFIX_PATH entry with include/pxr/pxr.h,
# plus USD_EXPORTER_REVIT_TBB_ROOT for the separate oneTBB package.
# Provides: usd_exporter_revit_usd_headers, PXR_VERSION, usd_exporter_revit_target_link_usd().

include_guard(GLOBAL)

if(NOT USD_EXPORTER_REVIT_USD_ROOT)
    foreach(_prefix IN LISTS CMAKE_PREFIX_PATH)
        if(EXISTS "${_prefix}/include/pxr/pxr.h")
            set(USD_EXPORTER_REVIT_USD_ROOT "${_prefix}")
            break()
        endif()
    endforeach()
endif()

if(USD_EXPORTER_REVIT_USD_ROOT AND EXISTS "${USD_EXPORTER_REVIT_USD_ROOT}/include/pxr/pxr.h")
    set(USD_EXPORTER_REVIT_USD_ROOT "${USD_EXPORTER_REVIT_USD_ROOT}" CACHE PATH "OpenUSD install root")
    set(USD_EXPORTER_REVIT_USD_INCLUDE_DIR "${USD_EXPORTER_REVIT_USD_ROOT}/include")
    set(USD_EXPORTER_REVIT_USD_LIB_DIR "${USD_EXPORTER_REVIT_USD_ROOT}/lib")

    # Modular vs monolithic: absence of usdGeom module libs indicates monolithic usd_ms.
    file(GLOB _usd_modular_probe "${USD_EXPORTER_REVIT_USD_LIB_DIR}/*usdGeom*")
    if(_usd_modular_probe)
        set(USD_EXPORTER_REVIT_USD_MONOLITHIC OFF)
    else()
        set(USD_EXPORTER_REVIT_USD_MONOLITHIC ON)
    endif()

    file(STRINGS "${USD_EXPORTER_REVIT_USD_INCLUDE_DIR}/pxr/pxr.h" _pxr_ver_line REGEX "#define[ \t]+PXR_VERSION[ \t]+[0-9]+")
    string(REGEX MATCH "[0-9]+" PXR_VERSION "${_pxr_ver_line}")
    message(STATUS "usd-exporter-revit: OpenUSD at ${USD_EXPORTER_REVIT_USD_ROOT} (PXR_VERSION=${PXR_VERSION}, monolithic=${USD_EXPORTER_REVIT_USD_MONOLITHIC})")

    add_library(usd_exporter_revit_usd_headers INTERFACE)
    target_include_directories(usd_exporter_revit_usd_headers SYSTEM INTERFACE "${USD_EXPORTER_REVIT_USD_INCLUDE_DIR}")
    target_link_directories(usd_exporter_revit_usd_headers INTERFACE "${USD_EXPORTER_REVIT_USD_LIB_DIR}")

    if(NOT USD_EXPORTER_REVIT_TBB_ROOT)
        message(FATAL_ERROR
            "The OpenUSD package requires oneTBB. "
            "Set -DUSD_EXPORTER_REVIT_TBB_ROOT=<onetbb-install>.")
    endif()
    if(NOT EXISTS "${USD_EXPORTER_REVIT_TBB_ROOT}/include/tbb/tbb.h")
        message(FATAL_ERROR "oneTBB headers not found at ${USD_EXPORTER_REVIT_TBB_ROOT}/include")
    endif()
    target_include_directories(
        usd_exporter_revit_usd_headers
        SYSTEM INTERFACE
        "${USD_EXPORTER_REVIT_TBB_ROOT}/include"
    )
    find_library(
        USD_EXPORTER_REVIT_TBB_LIBRARY
        NAMES tbb12 tbb tbb12_debug tbb_debug
        PATHS "${USD_EXPORTER_REVIT_TBB_ROOT}/lib"
        NO_DEFAULT_PATH
    )
    if(NOT USD_EXPORTER_REVIT_TBB_LIBRARY)
        message(FATAL_ERROR "oneTBB library not found at ${USD_EXPORTER_REVIT_TBB_ROOT}/lib")
    endif()
    target_link_libraries(usd_exporter_revit_usd_headers INTERFACE "${USD_EXPORTER_REVIT_TBB_LIBRARY}")

    # Boost headers ship inside some USD packages under include/boost-*.
    file(GLOB _boost_header_dirs LIST_DIRECTORIES true "${USD_EXPORTER_REVIT_USD_INCLUDE_DIR}/boost-*")
    if(_boost_header_dirs)
        list(GET _boost_header_dirs 0 _boost_include_dir)
        target_include_directories(usd_exporter_revit_usd_headers SYSTEM INTERFACE "${_boost_include_dir}")
    endif()

    # On Windows, extract vc toolset suffix from boost*.dll for BOOST_LIB_TOOLSET.
    if(WIN32)
        file(GLOB _boost_dlls "${USD_EXPORTER_REVIT_USD_LIB_DIR}/boost*.dll")
        if(_boost_dlls)
            list(GET _boost_dlls 0 _boost_dll)
            get_filename_component(_boost_dll_name "${_boost_dll}" NAME_WE)
            string(REGEX MATCH "vc([^-]+)" _vc_match "${_boost_dll_name}")
            if(CMAKE_MATCH_1)
                target_compile_definitions(usd_exporter_revit_usd_headers INTERFACE "BOOST_LIB_TOOLSET=\"vc${CMAKE_MATCH_1}\"")
            endif()
        endif()
    endif()
elseif(NOT USD_EXPORTER_REVIT_USD_ROOT)
    message(FATAL_ERROR
        "OpenUSD not found: set -DUSD_EXPORTER_REVIT_USD_ROOT=<usd-install> "
        "or add a CMAKE_PREFIX_PATH entry containing include/pxr/pxr.h.")
elseif(NOT EXISTS "${USD_EXPORTER_REVIT_USD_ROOT}/include/pxr/pxr.h")
    message(FATAL_ERROR
        "OpenUSD headers not found at ${USD_EXPORTER_REVIT_USD_ROOT}/include/pxr/pxr.h. "
        "Check USD_EXPORTER_REVIT_USD_ROOT or fetch dependencies first.")
endif()

function(usd_exporter_revit_target_link_usd target)
    if(NOT TARGET usd_exporter_revit_usd_headers)
        message(FATAL_ERROR "usd_exporter_revit_target_link_usd(${target}) needs OpenUSD: set -DUSD_EXPORTER_REVIT_USD_ROOT=<usd-install> or add it to CMAKE_PREFIX_PATH.")
    endif()

    target_link_libraries(${target} PRIVATE usd_exporter_revit_usd_headers)

    if(USD_EXPORTER_REVIT_USD_MONOLITHIC)
        find_library(USD_EXPORTER_REVIT_USDLIB_ms NAMES "usd_usd_ms" "usd_ms" PATHS "${USD_EXPORTER_REVIT_USD_LIB_DIR}" NO_DEFAULT_PATH)
        if(NOT USD_EXPORTER_REVIT_USDLIB_ms)
            message(FATAL_ERROR "monolithic OpenUSD library (usd_usd_ms/usd_ms) not found in ${USD_EXPORTER_REVIT_USD_LIB_DIR}")
        endif()
        target_link_libraries(${target} PRIVATE "${USD_EXPORTER_REVIT_USDLIB_ms}")
    else()
        foreach(_name ${ARGN})
            set(_var "USD_EXPORTER_REVIT_USDLIB_${_name}")
            find_library(${_var} NAMES "usd_${_name}" "${_name}" PATHS "${USD_EXPORTER_REVIT_USD_LIB_DIR}" NO_DEFAULT_PATH)
            if(NOT ${_var})
                message(FATAL_ERROR "OpenUSD library '${_name}' (usd_${_name}/${_name}) not found in ${USD_EXPORTER_REVIT_USD_LIB_DIR}")
            endif()
            target_link_libraries(${target} PRIVATE "${${_var}}")
        endforeach()
    endif()

    if(USD_EXPORTER_REVIT_WITH_PYTHON)
        if(NOT TARGET Python3::Python)
            message(FATAL_ERROR "USD_EXPORTER_REVIT_WITH_PYTHON is ON but Python3::Python was not found. Set -DPython3_ROOT_DIR=<python-root>.")
        endif()
        target_include_directories(${target} SYSTEM PRIVATE "${Python3_INCLUDE_DIRS}")
        if(WIN32)
            target_link_directories(${target} PRIVATE "${Python3_ROOT_DIR}/libs")
        endif()
        target_link_libraries(${target} PRIVATE Python3::Python)
    endif()
endfunction()

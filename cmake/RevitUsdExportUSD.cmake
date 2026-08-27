# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
# OpenUSD discovery for OpenUSD Exporter Plugin for Revit. Does not use pxrConfig.cmake; instead adds
# `<usd_root>/include` as a SYSTEM include and links individual USD libraries from `<usd_root>/lib`.
#
# Inputs: REVIT_USD_EXPORT_USD_ROOT or a CMAKE_PREFIX_PATH entry with include/pxr/pxr.h,
# plus REVIT_USD_EXPORT_TBB_ROOT for the separate oneTBB package.
# Provides: revit_usd_export_usd_headers, PXR_VERSION, revit_target_link_usd().

include_guard(GLOBAL)

if(NOT REVIT_USD_EXPORT_USD_ROOT)
    foreach(_prefix IN LISTS CMAKE_PREFIX_PATH)
        if(EXISTS "${_prefix}/include/pxr/pxr.h")
            set(REVIT_USD_EXPORT_USD_ROOT "${_prefix}")
            break()
        endif()
    endforeach()
endif()

if(REVIT_USD_EXPORT_USD_ROOT AND EXISTS "${REVIT_USD_EXPORT_USD_ROOT}/include/pxr/pxr.h")
    set(REVIT_USD_EXPORT_USD_ROOT "${REVIT_USD_EXPORT_USD_ROOT}" CACHE PATH "OpenUSD install root")
    set(REVIT_USD_EXPORT_USD_INCLUDE_DIR "${REVIT_USD_EXPORT_USD_ROOT}/include")
    set(REVIT_USD_EXPORT_USD_LIB_DIR "${REVIT_USD_EXPORT_USD_ROOT}/lib")

    # Modular vs monolithic: absence of usdGeom module libs indicates monolithic usd_ms.
    file(GLOB _usd_modular_probe "${REVIT_USD_EXPORT_USD_LIB_DIR}/*usdGeom*")
    if(_usd_modular_probe)
        set(REVIT_USD_EXPORT_USD_MONOLITHIC OFF)
    else()
        set(REVIT_USD_EXPORT_USD_MONOLITHIC ON)
    endif()

    file(STRINGS "${REVIT_USD_EXPORT_USD_INCLUDE_DIR}/pxr/pxr.h" _pxr_ver_line REGEX "#define[ \t]+PXR_VERSION[ \t]+[0-9]+")
    string(REGEX MATCH "[0-9]+" PXR_VERSION "${_pxr_ver_line}")
    message(STATUS "revit-usd-export: OpenUSD at ${REVIT_USD_EXPORT_USD_ROOT} (PXR_VERSION=${PXR_VERSION}, monolithic=${REVIT_USD_EXPORT_USD_MONOLITHIC})")

    add_library(revit_usd_export_usd_headers INTERFACE)
    target_include_directories(revit_usd_export_usd_headers SYSTEM INTERFACE "${REVIT_USD_EXPORT_USD_INCLUDE_DIR}")
    target_link_directories(revit_usd_export_usd_headers INTERFACE "${REVIT_USD_EXPORT_USD_LIB_DIR}")

    if(NOT REVIT_USD_EXPORT_TBB_ROOT)
        message(FATAL_ERROR
            "The OpenUSD package requires oneTBB. "
            "Set -DREVIT_USD_EXPORT_TBB_ROOT=<onetbb-install>.")
    endif()
    if(NOT EXISTS "${REVIT_USD_EXPORT_TBB_ROOT}/include/tbb/tbb.h")
        message(FATAL_ERROR "oneTBB headers not found at ${REVIT_USD_EXPORT_TBB_ROOT}/include")
    endif()
    target_include_directories(
        revit_usd_export_usd_headers
        SYSTEM INTERFACE
        "${REVIT_USD_EXPORT_TBB_ROOT}/include"
    )
    find_library(
        REVIT_USD_EXPORT_TBB_LIBRARY
        NAMES tbb12 tbb tbb12_debug tbb_debug
        PATHS "${REVIT_USD_EXPORT_TBB_ROOT}/lib"
        NO_DEFAULT_PATH
    )
    if(NOT REVIT_USD_EXPORT_TBB_LIBRARY)
        message(FATAL_ERROR "oneTBB library not found at ${REVIT_USD_EXPORT_TBB_ROOT}/lib")
    endif()
    target_link_libraries(revit_usd_export_usd_headers INTERFACE "${REVIT_USD_EXPORT_TBB_LIBRARY}")

    # Boost headers ship inside some USD packages under include/boost-*.
    file(GLOB _boost_header_dirs LIST_DIRECTORIES true "${REVIT_USD_EXPORT_USD_INCLUDE_DIR}/boost-*")
    if(_boost_header_dirs)
        list(GET _boost_header_dirs 0 _boost_include_dir)
        target_include_directories(revit_usd_export_usd_headers SYSTEM INTERFACE "${_boost_include_dir}")
    endif()

    # On Windows, extract vc toolset suffix from boost*.dll for BOOST_LIB_TOOLSET.
    if(WIN32)
        file(GLOB _boost_dlls "${REVIT_USD_EXPORT_USD_LIB_DIR}/boost*.dll")
        if(_boost_dlls)
            list(GET _boost_dlls 0 _boost_dll)
            get_filename_component(_boost_dll_name "${_boost_dll}" NAME_WE)
            string(REGEX MATCH "vc([^-]+)" _vc_match "${_boost_dll_name}")
            if(CMAKE_MATCH_1)
                target_compile_definitions(revit_usd_export_usd_headers INTERFACE "BOOST_LIB_TOOLSET=\"vc${CMAKE_MATCH_1}\"")
            endif()
        endif()
    endif()
elseif(NOT REVIT_USD_EXPORT_USD_ROOT)
    message(FATAL_ERROR
        "OpenUSD not found: set -DREVIT_USD_EXPORT_USD_ROOT=<usd-install> "
        "or add a CMAKE_PREFIX_PATH entry containing include/pxr/pxr.h.")
elseif(NOT EXISTS "${REVIT_USD_EXPORT_USD_ROOT}/include/pxr/pxr.h")
    message(FATAL_ERROR
        "OpenUSD headers not found at ${REVIT_USD_EXPORT_USD_ROOT}/include/pxr/pxr.h. "
        "Check REVIT_USD_EXPORT_USD_ROOT or fetch dependencies first.")
endif()

function(revit_target_link_usd target)
    if(NOT TARGET revit_usd_export_usd_headers)
        message(FATAL_ERROR "revit_target_link_usd(${target}) needs OpenUSD: set -DREVIT_USD_EXPORT_USD_ROOT=<usd-install> or add it to CMAKE_PREFIX_PATH.")
    endif()

    target_link_libraries(${target} PRIVATE revit_usd_export_usd_headers)

    if(REVIT_USD_EXPORT_USD_MONOLITHIC)
        find_library(REVIT_USD_EXPORT_USDLIB_ms NAMES "usd_usd_ms" "usd_ms" PATHS "${REVIT_USD_EXPORT_USD_LIB_DIR}" NO_DEFAULT_PATH)
        if(NOT REVIT_USD_EXPORT_USDLIB_ms)
            message(FATAL_ERROR "monolithic OpenUSD library (usd_usd_ms/usd_ms) not found in ${REVIT_USD_EXPORT_USD_LIB_DIR}")
        endif()
        target_link_libraries(${target} PRIVATE "${REVIT_USD_EXPORT_USDLIB_ms}")
    else()
        foreach(_name ${ARGN})
            set(_var "REVIT_USD_EXPORT_USDLIB_${_name}")
            find_library(${_var} NAMES "usd_${_name}" "${_name}" PATHS "${REVIT_USD_EXPORT_USD_LIB_DIR}" NO_DEFAULT_PATH)
            if(NOT ${_var})
                message(FATAL_ERROR "OpenUSD library '${_name}' (usd_${_name}/${_name}) not found in ${REVIT_USD_EXPORT_USD_LIB_DIR}")
            endif()
            target_link_libraries(${target} PRIVATE "${${_var}}")
        endforeach()
    endif()

    if(REVIT_USD_EXPORT_WITH_PYTHON)
        if(NOT TARGET Python3::Python)
            message(FATAL_ERROR "REVIT_USD_EXPORT_WITH_PYTHON is ON but Python3::Python was not found. Set -DPython3_ROOT_DIR=<python-root>.")
        endif()
        target_include_directories(${target} SYSTEM PRIVATE "${Python3_INCLUDE_DIRS}")
        if(WIN32)
            target_link_directories(${target} PRIVATE "${Python3_ROOT_DIR}/libs")
        endif()
        target_link_libraries(${target} PRIVATE Python3::Python)
    endif()
endfunction()

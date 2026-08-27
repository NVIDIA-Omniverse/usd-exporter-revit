// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

extern "C"
{
    /**
     * Determine if initialization was successful.
     * @return  A bool indicating if initialization was successful.
     */
    REVIT_USD_EXPORT_API bool initialized();

    /**
     * Perform some one-time initialization.
     * @return  A bool indicating if startup was successful.
     */
    REVIT_USD_EXPORT_API bool revit_usd_export_core_startup();

    /**
     * Resolve the plugin install root used for config and bundled assets.
     *
     * Derived from the directory containing `revit_usd_export.dll`.
     * @return  The install root path, or an empty string when resolution fails.
     */
    REVIT_USD_EXPORT_API const char* revit_usd_export_install_path();

    /**
     * Get the linear units.
     * @param[in] name            Name of the linear units.
     * @return  The linear unit
     */
    REVIT_USD_EXPORT_API double revit_usd_export_getGeomLinearUnits(const char* name);
}

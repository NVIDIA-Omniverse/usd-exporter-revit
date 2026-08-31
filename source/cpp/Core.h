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
    USD_EXPORTER_REVIT_API bool initialized();

    /**
     * Perform some one-time initialization.
     * @return  A bool indicating if startup was successful.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_startup();

    /**
     * Resolve the plugin install root used for config and bundled assets.
     *
     * Derived from the directory containing `usd_exporter_revit.dll`.
     * @return  The install root path, or an empty string when resolution fails.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_install_path();

    /**
     * Get the linear units.
     * @param[in] name            Name of the linear units.
     * @return  The linear unit
     */
    USD_EXPORTER_REVIT_API double usd_exporter_revit_getGeomLinearUnits(const char* name);
}

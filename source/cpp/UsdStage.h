// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

extern "C"
{
    /**
     * Sets the stage Edit Target to the Variant Set Edit Target.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim holding the variant set.
     * @param[in] set_name            Name of the variant set.
     * @param[in] option_name         Name of the variant option.
     */
    USD_EXPORTER_REVIT_API bool pxr_usd_stageSetVariantEditTarget(const long stage_id, const char* prim_path, const char* set_name, const char* option_name);

    /**
     * Sets the stage Edit Target to the Root Layer.
     * @param[in] stage_id            Stage Id.
     */
    USD_EXPORTER_REVIT_API void pxr_usd_stageSetRootEditTarget(const long stage_id);

    /**
     * Sets the stage Edit Target to the Session Layer.
     * @param[in] stage_id            Stage Id.
     */
    USD_EXPORTER_REVIT_API void pxr_usd_stageSetSessionEditTarget(const long stage_id);
}

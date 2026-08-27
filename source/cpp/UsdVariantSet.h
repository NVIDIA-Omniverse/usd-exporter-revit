// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

extern "C"
{
    /**
     * Adds a variant set to a prim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the parent prim.
     * @param[in] set_name            Name of the variant set.
     */
    REVIT_USD_EXPORT_API void pxr_usd_addVariantSet(const long stage_id, const char* prim_path, const char* set_name);

    /**
     * Adds a variant option to a variant set.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim holding the variant set.
     * @param[in] set_name            Name of the variant set.
     * @param[in] option_name         Name of the variant option.
     */
    REVIT_USD_EXPORT_API void pxr_usd_addVariantOption(const long stage_id, const char* prim_path, const char* set_name, const char* option_name);

    /**
     * Sets the selection of a variant set to the specified option.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim holding the variant set.
     * @param[in] set_name            Name of the variant set.
     * @param[in] option_name         Name of the variant option.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setVariantSelection(const long stage_id, const char* prim_path, const char* set_name, const char* option_name);
}

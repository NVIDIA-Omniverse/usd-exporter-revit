// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

extern "C"
{
    /**
     * Creates a rectangular (rect) light with an optional texture.
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path at which to define the light.
     * @param[in] width         The width of the rectangular light, in the local X axis.
     * @param[in] height        The height of the rectangular light, in the local Y axis.
     * @param[in] intensity     Light intensity.
     * @param[in] texturePath   Texture file path.
     * @return  If successful, the light's Prim path is returned.
     */
    REVIT_USD_EXPORT_API const char* revit_usd_export_core_defineRectLight(const long int stage_id, const char* prim_path, const float width, const float height, float intensity, const char* texturePath);
}

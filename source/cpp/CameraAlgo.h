// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <pxr/base/gf/camera.h>
#include <pxr/usd/usdGeom/camera.h>

extern "C"
{
    /**
     * Defines a basic 3d camera on the stage.
     * @param[in] stage_id    Stage Id.
     * @param[in] prim_path   The absolute prim path at which to define the camera.
     * @param[in] cameraData  The camera data to set, including the world space transform matrix.
     * @return  If successful, the camera's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_defineCamera(const long int stage_id, const char* prim_path, const pxr::GfCamera* cameraData);

    /**
     * Defines a basic 3d camera on the stage.
     * @param[in] stage_id    Stage Id.
     * @param[in] prim_path   The absolute prim path at which to define the camera.
     * @param[in] transform   Transform. 4x4 matrix.
     * @param[in] perspective true : Perspective, false : Orthographic.
     * @param[in] horizontalAperture  	Sets the width of the projector aperture in tenths of a world unit.
     * @param[in] verticalAperture  	Sets the height of the projector aperture in tenths of a world unit.
     * @param[in] horizontalApertureOffset       Sets the horizontal offset of the projector aperture in tenths of a world unit.
     * @param[in] verticalApertureOffset         Sets the vertical offset of the projector aperture in tenths of a world unit.
     * @param[in] focalLength  These are the values actually stored in the class and they correspond to measurements of an actual physical camera (in mm).
     * @param[in] clippingRangeNear  clipping range in world units (Near).
     * @param[in] clippingRangeFar   clipping range in world units (Far).
     * @param[in] fStop           lens aperture.
     * @param[in] focusDistance    focus distance in world units.
     * @return  If successful, the camera's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_defineCameraEx(
        const long int stage_id,
        const char* prim_path,
        const double transform[4][4],
        const bool perspective = true,
        const float horizontalAperture = 20.955f,
        const float verticalAperture = 15.2908f,
        const float horizontalApertureOffset = 0.0f,
        const float verticalApertureOffset = 0.0f,
        const float focalLength = 50.0f,
        const float clippingRangeNear = 1.0f,
        const float clippingRangeFar = 1000000.0f,
        const float fStop = 0.0f,
        const float focusDistance = 0.0f
    );
}

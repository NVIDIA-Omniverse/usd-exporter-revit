// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

extern "C"
{
    /**
     * Creates a cylinder light.
     * @param[in] stage_id      Stage Id.
     * @param[in] parent_path   Path to the parent prim to define the light under.
     * @param[in] name          Name of the light.
     * @param[in] length        Length of the cylinder.
     * @param[in] radius        Radius of the cylinder.
     * @param[in] intensity     Light intensity.
     * @return  If successful, the light's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* pxr_usd_defineCylinderLight(const long stage_id, const char* parent_path, const char* name, const float length, const float radius, const float intensity);

    /**
     * Creates a disk light.
     * @param[in] stage_id      Stage Id.
     * @param[in] parent_path   Path to the parent prim to define the light under.
     * @param[in] name          Name of the light.
     * @param[in] radius        Radius of the disk.
     * @param[in] intensity     Light intensity.
     * @return  If successful, the light's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* pxr_usd_defineDiskLight(const long stage_id, const char* parent_path, const char* name, const float radius, const float intensity);

    /**
     * Creates a sphere light.
     * @param[in] stage_id      Stage Id.
     * @param[in] parent_path   Path to the parent prim to define the light under.
     * @param[in] name          Name of the light.
     * @param[in] radius        Radius of the sphere.
     * @param[in] intensity     Light intensity.
     * @return  If successful, the light's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* pxr_usd_defineSphereLight(const long stage_id, const char* parent_path, const char* name, const float radius, const float intensity);

    /**
     * Create the UsdLuxShapingAPI attribute for IES file and set it to the provided file.
     * @param[in] stage_id      Stage Id.
     * @param[in] light_path    Path to the light to create and set the attribute.
     * @param[in] file_path     SdfAssetPath for the IES file.
     */
    USD_EXPORTER_REVIT_API void pxr_usd_createLuxShapingApiIesFileAttr(const long stage_id, const char* light_path, const char* file_path);

    /**
     * Create the UsdLuxShapingAPI attribute for IES file and set it to the provided file.
     * @param[in] stage_id      Stage Id.
     * @param[in] light_path    Path to the light to create and set the attribute.
     * @param[in] file_path     SdfAssetPath for the IES file.
     * @param[in] time          Time code value for setting the attribute.
     */
    USD_EXPORTER_REVIT_API void pxr_usd_createLuxShapingApiIesFileAttrAtTime(const long stage_id, const char* light_path, const char* file_path, const double time);
}

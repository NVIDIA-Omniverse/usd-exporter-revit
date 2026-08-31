// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <pxr/base/gf/vec3f.h>

extern "C"
{
    /**
     * Defines a UsdGeomCylinder as the child of a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] parent_path         Usd prim path to define the Cylinder under.
     * @param[in] name                Name of the Cylinder to be created.
     * @param[in] start               Start point of the center-line of the cylinder.
     * @param[in] end                 End point of the center-line of the cylinder.
     * @param[in] radius              Radius of the cylinder.
     * @return  If successful, the Cylinder's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* pxr_usd_defineCylinder(const long stage_id, const char* parent_path, const char* name, const pxr::GfVec3f start, const pxr::GfVec3f end, const double radius);

    /**
     * Defines a UsdGeomScope as the child of a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] parent_path         Usd prim path to define the Scope under.
     * @param[in] name                Name of the Scope to be created.
     * @return  If successful, the Scope's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* pxr_usd_defineScope(const long stage_id, const char* parent_path, const char* name);
}

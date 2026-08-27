// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <pxr/base/gf/vec3f.h>
#include <pxr/base/tf/token.h>
#include <pxr/usd/usd/attribute.h>
#include <pxr/usd/usd/timeCode.h>
#include <pxr/usd/usdLux/distantLight.h>
#include <pxr/usd/usdLux/domeLight.h>
#include <pxr/usd/usdLux/rectLight.h>
#include <pxr/usd/usdLux/shapingAPI.h>
#include <pxr/usd/usdLux/sphereLight.h>

using namespace pxr;

namespace revit::usd_export::core
{
//! Author the width attribute for a UsdLuxRectLight prim
//!
//! @param prim The UsdLuxRectLight prim to author the attribute
//! @param value The float value to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createRectWidthAttr(pxr::UsdLuxRectLight& prim, float value, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Author the height attribute for a UsdLuxRectLight prim
//!
//! @param prim The UsdLuxRectLight prim to author the attribute
//! @param value The float value to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createRectHeightAttr(pxr::UsdLuxRectLight& prim, float value, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Author the extent attribute for a UsdLuxCylinderLight, UsdLuxDiskLight,
//! UsdLuxRectLight, UsdLuxSphereLight, or UsdLuxPortalLight.
//!
//! Setting this attribute improves performance by negating the need to compute it on load.
//!
//! @param prim The prim to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createLightExtentAttr(pxr::UsdPrim prim, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Author the texture file attribute for a UsdLuxRectLight prim
//!
//! @param prim The UsdLuxRectLight prim to author the attribute
//! @param value The SdfAssetPath value to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createRectTextureFileAttr(pxr::UsdLuxRectLight& prim, const pxr::SdfAssetPath& value, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Author the intensity attribute for a prim with UsdLuxLight[API] applied
//!
//! @param prim The UsdLuxLight[API] prim to author the attribute
//! @param value The float value to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createIntensityAttr(pxr::UsdLuxLightAPI& prim, float value, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Author the "enable color temperature" attribute for a prim with UsdLuxLight[API] applied
//!
//! @param prim The UsdLuxLight[API] prim to author the attribute
//! @param value The bool value to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createEnableColorTemperatureAttr(pxr::UsdLuxLightAPI& prim, bool value, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Author the color temperature attribute for a prim with UsdLuxLight[API] applied
//!
//! @param prim The UsdLuxLight[API] prim to author the attribute
//! @param value The float value to author the attribute
//! @param time The time at which the attribute value is written
REVIT_USD_EXPORT_API void createColorTemperatureAttr(pxr::UsdLuxLightAPI& prim, float value, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());
} // namespace revit::usd_export::core

extern "C"
{
    /**
     * Author the "enable color temperature" attribute for a prim path applied with UsdTimeCode::Default
     * @param[in] stage_id         Stage Id.
     * @param[in] prim_path        The absolute prim path.
     * @param[in] value            The bool value to author the attribute
     */
    REVIT_USD_EXPORT_API void revit_usd_export_core_createEnableColorTemperatureAttr(const long int stage_id, const char* prim_path, const bool value);

    /**
     * Author the color temperature attribute for a prim path applied with UsdTimeCode::Default
     * @param[in] stage_id         Stage Id.
     * @param[in] prim_path        The absolute prim path.
     * @param[in] value            The float value to author the attribute
     */
    REVIT_USD_EXPORT_API void revit_usd_export_core_createColorTemperatureAttr(const long int stage_id, const char* prim_path, const float value);

    /**
     * Author the extent attribute for a UsdLuxCylinderLight, UsdLuxDiskLight, UsdLuxRectLight, UsdLuxSphereLight, or UsdLuxPortalLight with UsdTimeCode::Default
     * @param[in] stage_id         Stage Id.
     * @param[in] prim_path        The absolute prim path.
     */
    REVIT_USD_EXPORT_API void revit_usd_export_core_createLightExtentAttr(const long int stage_id, const char* prim_path);

    /**
     * Author the intensity attribute for a prim path applied with UsdTimeCode::Default
     * @param[in] stage_id         Stage Id.
     * @param[in] prim_path        The absolute prim path.
     * @param[in] value            The float value to author the attribute
     */
    REVIT_USD_EXPORT_API void revit_usd_export_core_createIntensityAttr(const long int stage_id, const char* prim_path, const float value);
}

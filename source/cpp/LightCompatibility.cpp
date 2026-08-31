// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "LightCompatibility.h"

#include "Log.h"
#include "StageCache.h"

namespace usd::exporter::revit::core
{

void createRectWidthAttr(pxr::UsdLuxRectLight& prim, float value, pxr::UsdTimeCode time)
{
    prim.CreateWidthAttr().Set(value, time);
}

void createRectHeightAttr(pxr::UsdLuxRectLight& prim, float value, pxr::UsdTimeCode time)
{
    prim.CreateHeightAttr().Set(value, time);
}

void createLightExtentAttr(pxr::UsdPrim prim, pxr::UsdTimeCode time)
{
    // The core lights UsdLuxCylinderLight, UsdLuxDiskLight, UsdLuxRectLight, UsdLuxSphereLight, and UsdLuxPortalLight
    // all inherit from from UsdLuxBoundableLightBase, are now Boundable, and have extents/bbox computations.
    if (auto boundable = pxr::UsdGeomBoundable(prim))
    {
        pxr::VtArray<pxr::GfVec3f> extent;
        pxr::UsdGeomBoundable::ComputeExtentFromPlugins(boundable, time, &extent);
        boundable.CreateExtentAttr().Set(extent, time);
    }
    else
    {
        // Warn the user that the light is not boundable
        USD_EXPORTER_REVIT_LOG_WARN(kUsdExporterRevitChannel, "Provided prim <%s> is not a UsdGeomBoundable.", prim.GetPath().GetText());
    }
}

void createRectTextureFileAttr(pxr::UsdLuxRectLight& prim, const pxr::SdfAssetPath& value, pxr::UsdTimeCode time)
{
    prim.CreateTextureFileAttr().Set(value, time);
}

void createIntensityAttr(pxr::UsdLuxLightAPI& prim, float value, pxr::UsdTimeCode time)
{
    prim.CreateIntensityAttr().Set(value, time);
}

void createEnableColorTemperatureAttr(pxr::UsdLuxLightAPI& prim, bool value, pxr::UsdTimeCode time)
{
    prim.CreateEnableColorTemperatureAttr().Set(value, time);
}

void createColorTemperatureAttr(pxr::UsdLuxLightAPI& prim, float value, pxr::UsdTimeCode time)
{
    prim.CreateColorTemperatureAttr().Set(value, time);
}

} // namespace usd::exporter::revit::core

extern "C"
{
    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_createEnableColorTemperatureAttr(const long int stage_id, const char* prim_path, const bool value)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return;
        }
        pxr::UsdLuxLightAPI lightAPI = pxr::UsdLuxLightAPI::Apply(prim);

        usd::exporter::revit::core::createEnableColorTemperatureAttr(lightAPI, value);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_createColorTemperatureAttr(const long int stage_id, const char* prim_path, const float value)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return;
        }
        pxr::UsdLuxLightAPI lightAPI = pxr::UsdLuxLightAPI::Apply(prim);

        usd::exporter::revit::core::createColorTemperatureAttr(lightAPI, value);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_createLightExtentAttr(const long int stage_id, const char* prim_path)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return;
        }

        usd::exporter::revit::core::createLightExtentAttr(prim);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_createIntensityAttr(const long int stage_id, const char* prim_path, const float value)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return;
        }
        pxr::UsdLuxLightAPI lightAPI = pxr::UsdLuxLightAPI::Apply(prim);

        usd::exporter::revit::core::createIntensityAttr(lightAPI, value);
    }
}

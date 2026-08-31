// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "UsdLux.h"

#include "LightCompatibility.h"
#include "StageCache.h"

#include <pxr/usd/usdLux/cylinderLight.h>
#include <pxr/usd/usdLux/diskLight.h>
#include <pxr/usd/usdLux/lightAPI.h>
#include <pxr/usd/usdLux/shapingAPI.h>
#include <pxr/usd/usdLux/sphereLight.h>

extern "C"
{
    USD_EXPORTER_REVIT_API const char* pxr_usd_defineCylinderLight(const long stage_id, const char* parent_path, const char* name, const float length, const float radius, const float intensity)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim parentPrim = stage->GetPrimAtPath(pxr::SdfPath(parent_path));
        if (!parentPrim)
        {
            return nullptr;
        }

        pxr::SdfPath lightPath = parentPrim.GetPath().AppendChild(pxr::TfToken(name));
        pxr::UsdLuxCylinderLight light = pxr::UsdLuxCylinderLight::Define(stage, lightPath);

        if (!light)
        {
            return nullptr;
        }

        light.CreateLengthAttr(pxr::VtValue(length));
        light.CreateRadiusAttr(pxr::VtValue(radius));

        pxr::UsdLuxLightAPI lightApi = pxr::UsdLuxLightAPI::Apply(light.GetPrim());
        usd::exporter::revit::core::createIntensityAttr(lightApi, intensity);

        std::string newPath = light.GetPath().GetAsString();
        std::string& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    USD_EXPORTER_REVIT_API const char* pxr_usd_defineDiskLight(const long stage_id, const char* parent_path, const char* name, const float radius, const float intensity)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim parentPrim = stage->GetPrimAtPath(pxr::SdfPath(parent_path));
        if (!parentPrim)
        {
            return nullptr;
        }

        pxr::SdfPath lightPath = parentPrim.GetPath().AppendChild(pxr::TfToken(name));
        pxr::UsdLuxDiskLight light = pxr::UsdLuxDiskLight::Define(stage, lightPath);

        if (!light)
        {
            return nullptr;
        }

        light.CreateRadiusAttr(pxr::VtValue(radius));

        pxr::UsdLuxLightAPI lightApi = pxr::UsdLuxLightAPI::Apply(light.GetPrim());
        usd::exporter::revit::core::createIntensityAttr(lightApi, intensity);

        std::string newPath = light.GetPath().GetAsString();
        std::string& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    USD_EXPORTER_REVIT_API const char* pxr_usd_defineSphereLight(const long stage_id, const char* parent_path, const char* name, const float radius, const float intensity)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim parentPrim = stage->GetPrimAtPath(pxr::SdfPath(parent_path));
        if (!parentPrim)
        {
            return nullptr;
        }

        pxr::SdfPath lightPath = parentPrim.GetPath().AppendChild(pxr::TfToken(name));
        pxr::UsdLuxSphereLight light = pxr::UsdLuxSphereLight::Define(stage, lightPath);

        if (!light)
        {
            return nullptr;
        }

        light.CreateRadiusAttr(pxr::VtValue(radius));

        pxr::UsdLuxLightAPI lightApi = pxr::UsdLuxLightAPI::Apply(light.GetPrim());
        usd::exporter::revit::core::createIntensityAttr(lightApi, intensity);

        std::string newPath = light.GetPath().GetAsString();
        std::string& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    USD_EXPORTER_REVIT_API void pxr_usd_createLuxShapingApiIesFileAttr(const long stage_id, const char* light_path, const char* file_path)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim lightPrim = stage->GetPrimAtPath(pxr::SdfPath(light_path));
        if (!lightPrim)
        {
            return;
        }

        pxr::UsdLuxShapingAPI shapingApi = pxr::UsdLuxShapingAPI::Apply(lightPrim);

        if (!shapingApi)
        {
            return;
        }

        shapingApi.CreateShapingIesFileAttr().Set(pxr::SdfAssetPath(file_path));
    }

    USD_EXPORTER_REVIT_API void pxr_usd_createLuxShapingApiIesFileAttrAtTime(const long stage_id, const char* light_path, const char* file_path, const double time)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim lightPrim = stage->GetPrimAtPath(pxr::SdfPath(light_path));
        if (!lightPrim)
        {
            return;
        }

        pxr::UsdLuxShapingAPI shapingApi = pxr::UsdLuxShapingAPI::Apply(lightPrim);

        if (!shapingApi)
        {
            return;
        }

        const pxr::UsdTimeCode _time(time);
        shapingApi.CreateShapingIesFileAttr().Set(pxr::SdfAssetPath(file_path), _time);
    }
}

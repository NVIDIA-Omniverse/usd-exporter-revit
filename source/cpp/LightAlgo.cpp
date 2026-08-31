// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "LightAlgo.h"

#include "LightCompatibility.h"
#include "Log.h"
#include "SdfUtils.h"
#include "StageCache.h"
#include "UsdUtils.h"

using namespace pxr;

namespace usd::exporter::revit::core
{

inline bool initLightApiAttrs(pxr::UsdSchemaBase& light, float intensity = 1.0f, float exposure = 0.0f)
{
    auto lightApi = UsdLuxLightAPI(light);

    if (!lightApi)
    {
        USD_EXPORTER_REVIT_LOG_WARN("UsdLuxLightApi is not compatible with prim at path \"[%s]\"", usd::exporter::revit::core::detail::getPathAsString(light.GetPath()).c_str());
        return false;
    }

    usd::exporter::revit::core::createIntensityAttr(lightApi, intensity);
    lightApi.CreateExposureAttr(VtValue(exposure));

    return true;
}

UsdLuxRectLight defineRectLight(UsdStagePtr stage, const SdfPath& path, float width, float height, float intensity, const char* texturePath)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!usd::exporter::revit::core::detail::isEditablePrimLocation(stage, path, &reason))
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Unable to define UsdLuxRectLight due to an invalid location: %s", reason.c_str());
        return UsdLuxRectLight();
    }

    auto light = UsdLuxRectLight::Define(stage, path);

    if (!light)
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Light schema is not compatible with prim at \"[%s]\"", usd::exporter::revit::core::detail::getPathAsString(path).c_str());
        return UsdLuxRectLight();
    }

    // Explicitly author the specifier and type name
    UsdPrim prim = light.GetPrim();
    prim.SetSpecifier(SdfSpecifierDef);
    prim.SetTypeName(prim.GetTypeName());

    if (!initLightApiAttrs(light, intensity))
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Unable to define UsdLuxRectLight at \"[%s]\"", usd::exporter::revit::core::detail::getPathAsString(path).c_str());
        return UsdLuxRectLight();
    }

    usd::exporter::revit::core::createRectWidthAttr(light, width);
    usd::exporter::revit::core::createRectHeightAttr(light, height);
    usd::exporter::revit::core::createLightExtentAttr(light.GetPrim());

    if (texturePath != nullptr)
    {
        usd::exporter::revit::core::createRectTextureFileAttr(light, SdfAssetPath(texturePath));
    }

    return light;
}
} // namespace usd::exporter::revit::core

extern "C"
{
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_defineRectLight(const long int stage_id, const char* prim_path, const float width, const float height, float intensity, const char* texturePath)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdLuxRectLight rectLight = usd::exporter::revit::core::defineRectLight(stage, pxr::SdfPath(prim_path), width, height, intensity, texturePath);
        if (!rectLight.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = rectLight.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }
}

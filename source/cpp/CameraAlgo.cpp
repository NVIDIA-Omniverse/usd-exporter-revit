// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "CameraAlgo.h"

#include "Log.h"
#include "SdfUtils.h"
#include "StageCache.h"
#include "UsdUtils.h"

using namespace pxr;

namespace revit::usd_export::core
{
UsdGeomCamera defineCamera(UsdStagePtr stage, const SdfPath& path, const GfCamera& cameraData)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(stage, path, &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomCamera due to an invalid location: %s", reason.c_str());
        return UsdGeomCamera();
    }

    // Early out if we know that we cannot successfully set the camera attributes
    // UsdGeomCamera::SetFromCamera() will silently fail if it is unable to successfully call UsdGeomXformable::MakeMatrixXform()
    // In order to catch this case we attempt that change ourselves prior to defining the camera
    if (auto xformable = UsdGeomXformable::Get(stage, path))
    {
        // The xformOp may be invalid if there are xform op opinions in the composed layer stack stronger than that of the current edit target.
        if (!xformable.MakeMatrixXform())
        {
            REVIT_LOG_ERROR(
                "Unable to define UsdGeomCamera at \"%s\" due to non-editable attributes: %s",
                revit::usd_export::core::detail::getPathAsString(path).c_str(),
                "Xform op opinions in the composed layer stack are stronger than that of the current edit target"
            );
            return UsdGeomCamera();
        }
    }

    UsdGeomCamera camera = UsdGeomCamera::Define(stage, path);
    if (!camera)
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomCamera at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        return camera;
    }

    // Explicitly author the specifier and type name
    UsdPrim prim = camera.GetPrim();
    prim.SetSpecifier(SdfSpecifierDef);
    prim.SetTypeName(prim.GetTypeName());

    camera.SetFromCamera(cameraData, UsdTimeCode::Default());

    return camera;
}
} // namespace revit::usd_export::core

extern "C"
{
    REVIT_USD_EXPORT_API const char* revit_usd_export_core_defineCamera(const long int stage_id, const char* prim_path, const pxr::GfCamera* cameraData)
    {
        // Get the stage corresponding to uri from cache.
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdGeomCamera camera = revit::usd_export::core::defineCamera(stage, pxr::SdfPath(prim_path), *cameraData);
        if (!camera.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = camera.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    REVIT_USD_EXPORT_API const char* revit_usd_export_core_defineCameraEx(
        const long int stage_id,
        const char* prim_path,
        const double transform[4][4],
        const bool perspective,
        const float horizontalAperture,
        const float verticalAperture,
        const float horizontalApertureOffset,
        const float verticalApertureOffset,
        const float focalLength,
        const float clippingRangeNear,
        const float clippingRangeFar,
        const float fStop,
        const float focusDistance
    )
    {
        // Get the stage corresponding to uri from cache.
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        const pxr::GfMatrix4d m(transform);

        pxr::GfCamera cameraData(
            m,
            perspective ? (pxr::GfCamera::Projection::Perspective) : (pxr::GfCamera::Projection::Orthographic),
            horizontalAperture,
            verticalAperture,
            horizontalApertureOffset,
            verticalApertureOffset,
            focalLength,
            pxr::GfRange1f(clippingRangeNear, clippingRangeFar),
            std::vector<pxr::GfVec4f>(),
            fStop,
            focusDistance
        );

        pxr::UsdGeomCamera camera = revit::usd_export::core::defineCamera(stage, pxr::SdfPath(prim_path), cameraData);
        if (!camera.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = camera.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }
}

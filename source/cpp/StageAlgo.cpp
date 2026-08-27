// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "StageAlgo.h"

#include "LayerAlgo.h"
#include "Log.h"
#include "StageCache.h"
#include "XformAlgo.h"
#include "client.h"

#include <pxr/base/tf/stringUtils.h>
#include <pxr/base/vt/array.h>
#include <pxr/base/vt/value.h>
#include <pxr/usd/ar/resolver.h>
#include <pxr/usd/sdf/copyUtils.h>
#include <pxr/usd/usd/primCompositionQuery.h>
#include <pxr/usd/usd/primRange.h>
#include <pxr/usd/usdGeom/cylinder.h>
#include <pxr/usd/usdGeom/mesh.h>
#include <pxr/usd/usdGeom/metrics.h>
#include <pxr/usd/usdGeom/pointBased.h>
#include <pxr/usd/usdGeom/tokens.h>
#include <pxr/usd/usdPhysics/metrics.h>
#include <pxr/usd/usdGeom/xformCommonAPI.h>
#include <pxr/usd/usdGeom/xformable.h>
#include <pxr/usd/usdUtils/authoring.h>
#include <pxr/usd/usdUtils/stitch.h>


using namespace pxr;

namespace revit::usd_export::core
{
// clang-format off
    TF_DEFINE_PRIVATE_TOKENS(
        _tokens,
        ((y, "y"))
        ((z, "z"))
    );
// clang-format on

bool validateStageMetrics(const TfToken& upAxis, const double linearUnits, const double massUnits, std::string* reason)
{
    // Validate the up axis
    if (upAxis != UsdGeomTokens->z && upAxis != UsdGeomTokens->y)
    {
        // Also accept lower case "y" and "z" tokens
        // This avoid confusion for Python clients where TfToken is simply a string and it is common to confuse the required case
        if (upAxis != _tokens->z && upAxis != _tokens->y)
        {
            *reason = TfStringPrintf("Unsupported up axis value \"%s\"", upAxis.GetString().c_str());
            return false;
        }
    }

    // Validate the linear units
    if (linearUnits <= 0.0)
    {
        *reason = TfStringPrintf("Linear units value must be greater than zero, received %f", linearUnits);
        return false;
    }

    // Validate the mass units
    if (massUnits <= 0.0)
    {
        *reason = TfStringPrintf("Mass units value must be greater than zero, received %f", massUnits);
        return false;
    }

    return true;
}

// Business logic for defining the default prim and setting stage metrics without validation
// This avoids duplicate validation when configuring the stage within a function that has already validated the arguments
bool uncheckedConfigureStage(UsdStagePtr stage, const std::string& defaultPrimName, const TfToken& upAxis, const double linearUnits, const double massUnits)
{
    // Set stage metrics via the stage
    // The metadata will be authored on the root layer
    if (!UsdGeomSetStageMetersPerUnit(stage, linearUnits))
    {
        return false;
    }

    // Set stage mass units via the stage
    // The metadata will be authored on the root layer
    if (!UsdPhysicsSetStageKilogramsPerUnit(stage, massUnits))
    {
        return false;
    }

    // If a lower case "y" or "z" token was provided resolve it to the expected upper case token
    TfToken resolvedUpAxis = upAxis;
    if (resolvedUpAxis == _tokens->z)
    {
        resolvedUpAxis = UsdGeomTokens->z;
    }
    if (resolvedUpAxis == _tokens->y)
    {
        resolvedUpAxis = UsdGeomTokens->y;
    }
    if (!UsdGeomSetStageUpAxis(stage, resolvedUpAxis))
    {
        return false;
    }

    const TfToken defaultPrimToken = TfToken(defaultPrimName);
    const SdfPath defaultPrimPath = SdfPath::AbsoluteRootPath().AppendChild(defaultPrimToken);

    // Define a prim of type "Scope" at the default prim path if there is not already a prim specified
    // The specifier and type name are not set on existing prim specs so that it is possible to use configureStage in cases where a "class" or "over"
    // specifier is desired, or the type name is intentionally undefined.
    SdfLayerHandle layer = stage->GetRootLayer();
    if (!layer->GetPrimAtPath(defaultPrimPath))
    {
        SdfPrimSpecHandle primSpec = SdfCreatePrimInLayer(layer, defaultPrimPath);
        primSpec->SetSpecifier(SdfSpecifierDef);
        primSpec->SetTypeName("Scope");
    }

    // Set the default prim on the root layer
    layer->SetDefaultPrim(defaultPrimToken);

    return true;
}

UsdStageRefPtr createStage(const std::string& identifier, const std::string& defaultPrimName, const TfToken& upAxis, const double linearUnits, const SdfLayer::FileFormatArguments& fileFormatArgs)
{
    // Refuse remote schemes before any filesystem / Ar resolver open
    if (!detail::isLocalUri(identifier))
    {
        REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" because the identifier is not a local URI", identifier.c_str());
        return nullptr;
    }

    // Early out on an unsupported identifier
    if (identifier.empty() || !UsdStage::IsSupportedFile(identifier))
    {
        REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" due to an invalid identifier", identifier.c_str());
        return nullptr;
    }

    // Early out on an invalid default prim name
    if (!SdfPath::IsValidIdentifier(defaultPrimName))
    {
        REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" due to an invalid default prim name: \"%s\" is not a valid identifier", identifier.c_str(), defaultPrimName.c_str());
        return nullptr;
    }

    const double massUnits = UsdPhysicsMassUnits::kilograms;

    // Early out on invalid stage metrics
    std::string reason;
    if (!validateStageMetrics(upAxis, linearUnits, massUnits, &reason))
    {
        REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" due to invalid stage metrics: %s", identifier.c_str(), reason.c_str());
        return nullptr;
    }

    // Create the stage in memory to avoid adding the identifier to the registry in cases where failures occur
    UsdStageRefPtr stage = UsdStage::CreateInMemory(identifier);
    revit::usd_export::core::setLayerAuthoringMetadata(stage->GetRootLayer());

    // Configure the stage
    if (!uncheckedConfigureStage(stage, defaultPrimName, upAxis, linearUnits, massUnits))
    {
        return nullptr;
    }

    // Export the stage to the desired identifier
    const std::string comment = "";
    if (!stage->GetRootLayer()->Export(identifier, comment, fileFormatArgs))
    {
        return nullptr;
    }

    // If the layer is already loaded reload it and return a stage wrapping the layer
    // Without the reload the state of the layer will not reflect what was just exported
    if (SdfLayerHandle layer = SdfLayer::Find(identifier))
    {
        if (!layer->Reload(true))
        {
            return nullptr;
        }
        return UsdStage::Open(layer);
    }

    // Return a stage wrapping the exported layer
    return UsdStage::Open(identifier);
}

void saveStage(UsdStagePtr stage, const char* comment)
{
    SdfLayerHandleVector dirtyLayers = UsdUtilsGetDirtyLayers(stage);
    for (auto& layer : dirtyLayers)
    {
        if (!layer->IsAnonymous() && !revit::usd_export::core::hasLayerAuthoringMetadata(layer))
        {
            revit::usd_export::core::setLayerAuthoringMetadata(layer);
        }
    }

    if (comment == nullptr)
    {
        REVIT_LOG_INFO("Saving \"%s\"", UsdDescribe(stage).c_str());
        stage->Save();
    }
    else
    {
        REVIT_LOG_INFO("Saving \"%s\" with comment \"%s\"", UsdDescribe(stage).c_str(), comment);
        for (auto& layer : dirtyLayers)
        {
            if (!layer->IsAnonymous())
            {
                layer->SetComment(comment);
            }
        }
        stage->Save();
    }
}


// set metersPerUnit for the stage and scale elements in the stage accordingly.
bool convertMetersPerUnit(UsdStagePtr stage, const double metersPerUnit)
{
    if (!stage)
    {
        REVIT_LOG_ERROR("convertMetersPerUnit: Invalid stage provided");
        return false;
    }

    if (metersPerUnit <= 0.0)
    {
        REVIT_LOG_ERROR("convertMetersPerUnit: metersPerUnit must be greater than zero, received %f", metersPerUnit);
        return false;
    }

    // Get the original meters per unit
    double originalMetersPerUnit = UsdGeomGetStageMetersPerUnit(stage);

    // If the values are the same, no conversion needed
    if (std::abs(originalMetersPerUnit - metersPerUnit) < 1e-9)
    {
        return true;
    }

    // Calculate the scale factor
    double scaleFactor = originalMetersPerUnit / metersPerUnit;

    // Set the new meters per unit for the stage first
    if (!UsdGeomSetStageMetersPerUnit(stage, metersPerUnit))
    {
        REVIT_LOG_ERROR("convertMetersPerUnit: Failed to set meters per unit to %f", metersPerUnit);
        return false;
    }

    // Scale geometries/xforms in the stage
    for (UsdPrim prim : stage->Traverse())
    {
        if (prim.IsA<UsdGeomXformable>())
        {
            UsdGeomXformable xformable(prim);

            // Get existing transform components including pivot
            GfVec3d translation, pivot;
            GfVec3f rotation, scale;
            revit::usd_export::core::RotationOrder rotationOrder;
            revit::usd_export::core::getLocalTransformComponents(prim, translation, pivot, rotation, rotationOrder, scale, UsdTimeCode::Default());

            // Scale the translation and pivot by the scale factor
            translation *= scaleFactor;
            pivot *= scaleFactor;

            if (prim.IsInstanceable())
            {
                scale *= scaleFactor;
            }

            // Set the scaled transform components back
            revit::usd_export::core::setLocalTransform(prim, translation, pivot, rotation, rotationOrder, scale, UsdTimeCode::Default());
        }

        // Scale point-based geometry (meshes, curves, etc.)
        if (prim.IsA<UsdGeomPointBased>())
        {
            UsdGeomPointBased pointBased(prim);
            UsdAttribute pointsAttr = pointBased.GetPointsAttr();

            if (pointsAttr && pointsAttr.HasValue())
            {
                VtVec3fArray points;
                if (pointsAttr.Get(&points))
                {
                    for (auto& point : points)
                    {
                        point *= scaleFactor;
                    }
                    pointsAttr.Set(points);
                }
            }
        }

        // Scale extents for all boundable primitives
        if (prim.IsA<UsdGeomBoundable>())
        {
            UsdGeomBoundable boundable(prim);
            UsdAttribute extentAttr = boundable.GetExtentAttr();

            if (extentAttr && extentAttr.HasValue())
            {
                VtVec3fArray extent;
                if (extentAttr.Get(&extent))
                {
                    for (auto& ext : extent)
                    {
                        ext *= scaleFactor;
                    }
                    extentAttr.Set(extent);
                }
            }
        }
        // Scale cylinder
        if (prim.IsA<UsdGeomCylinder>())
        {
            UsdGeomCylinder cylinder(prim);
            UsdAttribute radiusAttr = cylinder.GetRadiusAttr();
            UsdAttribute heightAttr = cylinder.GetHeightAttr();

            if (radiusAttr && radiusAttr.HasValue())
            {
                double radius;
                if (radiusAttr.Get(&radius))
                {
                    radius *= scaleFactor;
                    radiusAttr.Set(radius);
                }
            }

            if (heightAttr && heightAttr.HasValue())
            {
                double height;
                if (heightAttr.Get(&height))
                {
                    height *= scaleFactor;
                    heightAttr.Set(height);
                }
            }
        }
    }

    REVIT_LOG_INFO("convertMetersPerUnit: Successfully converted stage to %f meters per unit", metersPerUnit);
    return true;
}

double getMetersPerUnitFromFile(const std::string& filePath)
{
    if (filePath.empty())
    {
        REVIT_LOG_ERROR("getMetersPerUnitFromFile: Empty file path provided");
        return -1.0;
    }

    if (!detail::isLocalUri(filePath))
    {
        REVIT_LOG_ERROR("getMetersPerUnitFromFile: Refusing non-local URI \"%s\"", filePath.c_str());
        return -1.0;
    }

    try
    {
        // Open the stage for reading only
        UsdStageRefPtr stage = UsdStage::Open(filePath, UsdStage::LoadNone);

        if (!stage)
        {
            REVIT_LOG_WARN("getMetersPerUnitFromFile: Failed to open USD file at \"%s\"", filePath.c_str());
            return -1.0;
        }

        // Get the metersPerUnit from the stage
        double metersPerUnit = UsdGeomGetStageMetersPerUnit(stage);

        REVIT_LOG_INFO("getMetersPerUnitFromFile: File \"%s\" has metersPerUnit = %f", filePath.c_str(), metersPerUnit);

        return metersPerUnit;
    }
    catch (const std::exception& e)
    {
        REVIT_LOG_ERROR("getMetersPerUnitFromFile: Exception while reading file \"%s\": %s", filePath.c_str(), e.what());
        return -1.0;
    }
}

} // namespace revit::usd_export::core

extern "C"
{
    REVIT_USD_EXPORT_API long int revit_usd_export_core_createStage(const char* identifier, const char* defaultPrimName, char* upAxis, const double linearUnits)
    {
        std::string _defaultPrimName = defaultPrimName;

        pxr::UsdStageRefPtr stage = revit::usd_export::core::createStage(identifier, std::string(defaultPrimName), pxr::TfToken(upAxis), linearUnits);
        if (stage == nullptr)
        {
            return 0;
        }

        // Obtain stage id from StageCache.
        return revit::usd_export::core::stageCache.add(stage);
    }

    REVIT_USD_EXPORT_API void revit_usd_export_core_saveStage(const long int stage_id, const char* commit)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        revit::usd_export::core::saveStage(stage, commit);
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_convertMetersPerUnit(const long int stage_id, const double metersPerUnit)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        return revit::usd_export::core::convertMetersPerUnit(stage, metersPerUnit);
    }

    REVIT_USD_EXPORT_API double revit_usd_export_core_getMetersPerUnitFromFile(const char* filePath)
    {
        if (filePath == nullptr)
        {
            return 0.0;
        }

        std::string path(filePath);
        return revit::usd_export::core::getMetersPerUnitFromFile(path);
    }
}

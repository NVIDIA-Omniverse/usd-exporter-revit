// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "StageAlgo.h"

#include "LayerAlgo.h"
#include "Log.h"
#include "StageCache.h"
#include "XformAlgo.h"
#include "client.h"

#include <pxr/base/arch/fileSystem.h>
#include <pxr/base/tf/stringUtils.h>
#include <pxr/base/vt/array.h>
#include <pxr/base/vt/value.h>
#include <pxr/usd/ar/resolver.h>
#include <pxr/usd/ar/resolverContextBinder.h>
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
#include <pxr/usd/usdUtils/dependencies.h>
#include <pxr/usd/usdUtils/stitch.h>

#include <filesystem>
#include <functional>
#include <sstream>
#include <system_error>


using namespace pxr;

namespace usd::exporter::revit::core
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
        USD_EXPORTER_REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" because the identifier is not a local URI", identifier.c_str());
        return nullptr;
    }

    // Early out on an unsupported identifier
    if (identifier.empty() || !UsdStage::IsSupportedFile(identifier))
    {
        USD_EXPORTER_REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" due to an invalid identifier", identifier.c_str());
        return nullptr;
    }

    // Early out on an invalid default prim name
    if (!SdfPath::IsValidIdentifier(defaultPrimName))
    {
        USD_EXPORTER_REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" due to an invalid default prim name: \"%s\" is not a valid identifier", identifier.c_str(), defaultPrimName.c_str());
        return nullptr;
    }

    const double massUnits = UsdPhysicsMassUnits::kilograms;

    // Early out on invalid stage metrics
    std::string reason;
    if (!validateStageMetrics(upAxis, linearUnits, massUnits, &reason))
    {
        USD_EXPORTER_REVIT_LOG_WARN("Unable to create UsdStage at \"%s\" due to invalid stage metrics: %s", identifier.c_str(), reason.c_str());
        return nullptr;
    }

    // Create the stage in memory to avoid adding the identifier to the registry in cases where failures occur
    UsdStageRefPtr stage = UsdStage::CreateInMemory(identifier);
    usd::exporter::revit::core::setLayerAuthoringMetadata(stage->GetRootLayer());

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
        if (!layer->IsAnonymous() && !usd::exporter::revit::core::hasLayerAuthoringMetadata(layer))
        {
            usd::exporter::revit::core::setLayerAuthoringMetadata(layer);
        }
    }

    if (comment == nullptr)
    {
        USD_EXPORTER_REVIT_LOG_INFO("Saving \"%s\"", UsdDescribe(stage).c_str());
        stage->Save();
    }
    else
    {
        USD_EXPORTER_REVIT_LOG_INFO("Saving \"%s\" with comment \"%s\"", UsdDescribe(stage).c_str(), comment);
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

namespace
{

// Owns a directory created for USDZ packaging and removes it (and everything staged under it)
// once packaging is done, on every return path.
class TempStagingDirectory
{
public:

    explicit TempStagingDirectory(std::filesystem::path path) : m_path(std::move(path))
    {
    }

    ~TempStagingDirectory()
    {
        std::error_code ec;
        std::filesystem::remove_all(m_path, ec);
    }

    TempStagingDirectory(const TempStagingDirectory&) = delete;
    TempStagingDirectory& operator=(const TempStagingDirectory&) = delete;

    const std::filesystem::path& path() const
    {
        return m_path;
    }

private:

    std::filesystem::path m_path;
};

// Creates a fresh, uniquely-named directory under the system temp directory. Returns an empty
// path on failure.
std::filesystem::path createUniqueTempDirectory()
{
    const std::string dir = ArchMakeTmpSubdir(ArchGetTmpDir(), "usd_exporter_revit_usdz_");
    return dir.empty() ? std::filesystem::path{} : std::filesystem::path(dir);
}

std::filesystem::path canonicalizePath(const std::string& originalPath)
{
    std::error_code ec;
    std::filesystem::path absPath = std::filesystem::weakly_canonical(originalPath, ec);
    if (ec || absPath.empty())
    {
        absPath = std::filesystem::absolute(originalPath, ec);
    }
    return absPath.lexically_normal();
}

// Maps a filesystem path into a unique location under `tempRoot`, preserving its structure
// relative to `basePath` so relative asset references between staged files still resolve.
// Staging relative to `basePath` rather than the full absolute path avoids exceeding Windows'
// MAX_PATH limit, since the export already writes under a deep per-export temp folder. Paths
// outside `basePath` are grouped into a short hashed bucket per source directory instead.
std::filesystem::path mapPathUnderTempRoot(const std::filesystem::path& tempRoot, const std::filesystem::path& basePath, const std::string& originalPath)
{
    const std::filesystem::path absPath = canonicalizePath(originalPath);

    const std::filesystem::path relative = absPath.lexically_relative(basePath);
    if (!relative.empty() && *relative.begin() != "..")
    {
        return tempRoot / relative;
    }

    const size_t hash = std::hash<std::string>{}(absPath.parent_path().generic_string());
    std::ostringstream bucket;
    bucket << std::hex << hash;
    return tempRoot / "ext" / bucket.str() / absPath.filename();
}

} // namespace

bool createUsdzPackage(const std::string& assetPath, const std::string& usdzFilePath)
{
    if (!detail::isLocalUri(assetPath) || !detail::isLocalUri(usdzFilePath))
    {
        USD_EXPORTER_REVIT_LOG_ERROR("USDZ packaging requires local paths: source \"%s\", output \"%s\"", assetPath.c_str(), usdzFilePath.c_str());
        return false;
    }

    if (assetPath.empty() || usdzFilePath.empty())
    {
        USD_EXPORTER_REVIT_LOG_ERROR("USDZ packaging requires non-empty source and output paths");
        return false;
    }

    ArResolverContextBinder resolverContext(ArGetResolver().CreateDefaultContextForAsset(assetPath));
    std::vector<SdfLayerRefPtr> layers;
    std::vector<std::string> assets;
    std::vector<std::string> unresolvedPaths;
    if (!UsdUtilsComputeAllDependencies(SdfAssetPath(assetPath), &layers, &assets, &unresolvedPaths))
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Failed to resolve the USDZ root layer \"%s\"", assetPath.c_str());
        return false;
    }

    for (const std::string& path : unresolvedPaths)
    {
        if (!TfStringEndsWith(TfStringToLower(path), ".mdl"))
        {
            USD_EXPORTER_REVIT_LOG_ERROR("USDZ dependency could not be resolved: \"%s\"", path.c_str());
            return false;
        }
    }

    // `layers` may include the user's original USD assets (e.g. referenced via family mappings),
    // not just layers produced for this export. Stage private copies under a temp directory and
    // mutate/save only those, so MDL stripping never touches files the user still owns.
    std::filesystem::path tempRoot = createUniqueTempDirectory();
    if (tempRoot.empty())
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Failed to create a temporary staging directory for USDZ packaging");
        return false;
    }
    TempStagingDirectory tempStagingDirectory(tempRoot);

    const std::string resolvedAssetPath = ArGetResolver().Resolve(assetPath);
    const std::filesystem::path basePath = canonicalizePath(resolvedAssetPath).parent_path();
    std::filesystem::path stagedAssetPath;

    for (const SdfLayerRefPtr& layer : layers)
    {
        std::filesystem::path copyPath = mapPathUnderTempRoot(tempRoot, basePath, layer->GetRealPath());
        if (layer->GetRealPath() == resolvedAssetPath)
        {
            stagedAssetPath = copyPath;
        }

        std::error_code ec;
        std::filesystem::create_directories(copyPath.parent_path(), ec);

        SdfLayerRefPtr copyLayer = SdfLayer::CreateNew(copyPath.generic_string());
        if (!copyLayer)
        {
            USD_EXPORTER_REVIT_LOG_ERROR("Failed to stage a copy of \"%s\" for USDZ packaging", layer->GetIdentifier().c_str());
            return false;
        }
        copyLayer->TransferContent(layer);

        UsdUtilsModifyAssetPaths(
            copyLayer,
            [](const std::string& path)
            {
                return TfStringEndsWith(TfStringToLower(path), ".mdl") ? std::string() : path;
            }
        );

        UsdStageRefPtr stage = UsdStage::Open(copyLayer);
        if (stage == nullptr)
        {
            USD_EXPORTER_REVIT_LOG_ERROR("Failed to open staged copy of \"%s\" for USDZ packaging", layer->GetIdentifier().c_str());
            return false;
        }
        SdfPathVector mdlShaderPaths;
        for (UsdPrim prim : stage->TraverseAll())
        {
            bool isMdlShader = false;
            for (const TfToken& propertyName : prim.GetPropertyNames())
            {
                if (TfStringStartsWith(propertyName.GetString(), "outputs:mdl:"))
                {
                    prim.RemoveProperty(propertyName);
                }
                else if (TfStringStartsWith(propertyName.GetString(), "info:mdl:"))
                {
                    // Identifies UsdShadeShader prims implementing the MDL render context (e.g. authored via
                    // UsdShadeShader::SetSourceAsset(..., "mdl")), regardless of the prim's name.
                    isMdlShader = true;
                }
            }

            if (isMdlShader)
            {
                mdlShaderPaths.push_back(prim.GetPath());
            }
        }
        for (const SdfPath& path : mdlShaderPaths)
        {
            stage->RemovePrim(path);
        }

        copyLayer->Save();
    }

    // Mirror non-layer dependencies (textures, etc.) alongside their staged layers so relative
    // references from the copies still resolve when the package is authored below. MDL assets
    // were stripped from the copies above, so they don't need to be staged.
    for (const std::string& assetDependency : assets)
    {
        if (TfStringEndsWith(TfStringToLower(assetDependency), ".mdl"))
        {
            continue;
        }

        std::error_code ec;
        if (!std::filesystem::is_regular_file(assetDependency, ec))
        {
            USD_EXPORTER_REVIT_LOG_WARN("Skipping USDZ dependency \"%s\": not a regular file (%s)", assetDependency.c_str(), ec.message().c_str());
            continue;
        }

        std::filesystem::path copyPath = mapPathUnderTempRoot(tempRoot, basePath, assetDependency);
        std::filesystem::create_directories(copyPath.parent_path(), ec);
        if (ec)
        {
            USD_EXPORTER_REVIT_LOG_ERROR("Failed to stage dependency \"%s\" for USDZ packaging: %s", assetDependency.c_str(), ec.message().c_str());
            return false;
        }
        std::filesystem::copy_file(assetDependency, copyPath, std::filesystem::copy_options::overwrite_existing, ec);
        if (ec)
        {
            USD_EXPORTER_REVIT_LOG_ERROR("Failed to stage dependency \"%s\" for USDZ packaging: %s", assetDependency.c_str(), ec.message().c_str());
            return false;
        }
    }

    if (stagedAssetPath.empty())
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Failed to locate the staged copy of the USDZ root layer \"%s\"", assetPath.c_str());
        return false;
    }

    if (!UsdUtilsCreateNewUsdzPackage(SdfAssetPath(stagedAssetPath.generic_string()), usdzFilePath))
    {
        USD_EXPORTER_REVIT_LOG_ERROR("Failed to create USDZ package \"%s\" from \"%s\". Verify that every referenced asset exists and can be resolved.", usdzFilePath.c_str(), assetPath.c_str());
        return false;
    }

    return true;
}


// set metersPerUnit for the stage and scale elements in the stage accordingly.
bool convertMetersPerUnit(UsdStagePtr stage, const double metersPerUnit)
{
    if (!stage)
    {
        USD_EXPORTER_REVIT_LOG_ERROR("convertMetersPerUnit: Invalid stage provided");
        return false;
    }

    if (metersPerUnit <= 0.0)
    {
        USD_EXPORTER_REVIT_LOG_ERROR("convertMetersPerUnit: metersPerUnit must be greater than zero, received %f", metersPerUnit);
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
        USD_EXPORTER_REVIT_LOG_ERROR("convertMetersPerUnit: Failed to set meters per unit to %f", metersPerUnit);
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
            usd::exporter::revit::core::RotationOrder rotationOrder;
            usd::exporter::revit::core::getLocalTransformComponents(prim, translation, pivot, rotation, rotationOrder, scale, UsdTimeCode::Default());

            // Scale the translation and pivot by the scale factor
            translation *= scaleFactor;
            pivot *= scaleFactor;

            if (prim.IsInstanceable())
            {
                scale *= scaleFactor;
            }

            // Set the scaled transform components back
            usd::exporter::revit::core::setLocalTransform(prim, translation, pivot, rotation, rotationOrder, scale, UsdTimeCode::Default());
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

    USD_EXPORTER_REVIT_LOG_INFO("convertMetersPerUnit: Successfully converted stage to %f meters per unit", metersPerUnit);
    return true;
}

double getMetersPerUnitFromFile(const std::string& filePath)
{
    if (filePath.empty())
    {
        USD_EXPORTER_REVIT_LOG_ERROR("getMetersPerUnitFromFile: Empty file path provided");
        return -1.0;
    }

    if (!detail::isLocalUri(filePath))
    {
        USD_EXPORTER_REVIT_LOG_ERROR("getMetersPerUnitFromFile: Refusing non-local URI \"%s\"", filePath.c_str());
        return -1.0;
    }

    try
    {
        // Open the stage for reading only
        UsdStageRefPtr stage = UsdStage::Open(filePath, UsdStage::LoadNone);

        if (!stage)
        {
            USD_EXPORTER_REVIT_LOG_WARN("getMetersPerUnitFromFile: Failed to open USD file at \"%s\"", filePath.c_str());
            return -1.0;
        }

        // Get the metersPerUnit from the stage
        double metersPerUnit = UsdGeomGetStageMetersPerUnit(stage);

        USD_EXPORTER_REVIT_LOG_INFO("getMetersPerUnitFromFile: File \"%s\" has metersPerUnit = %f", filePath.c_str(), metersPerUnit);

        return metersPerUnit;
    }
    catch (const std::exception& e)
    {
        USD_EXPORTER_REVIT_LOG_ERROR("getMetersPerUnitFromFile: Exception while reading file \"%s\": %s", filePath.c_str(), e.what());
        return -1.0;
    }
}

} // namespace usd::exporter::revit::core

extern "C"
{
    USD_EXPORTER_REVIT_API long int usd_exporter_revit_core_createStage(const char* identifier, const char* defaultPrimName, char* upAxis, const double linearUnits)
    {
        std::string _defaultPrimName = defaultPrimName;

        pxr::UsdStageRefPtr stage = usd::exporter::revit::core::createStage(identifier, std::string(defaultPrimName), pxr::TfToken(upAxis), linearUnits);
        if (stage == nullptr)
        {
            return 0;
        }

        // Obtain stage id from StageCache.
        return usd::exporter::revit::core::stageCache.add(stage);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_saveStage(const long int stage_id, const char* commit)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        usd::exporter::revit::core::saveStage(stage, commit);
    }

    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createUsdzPackage(const char* assetPath, const char* usdzFilePath)
    {
        if (assetPath == nullptr || usdzFilePath == nullptr)
        {
            return false;
        }

        return usd::exporter::revit::core::createUsdzPackage(assetPath, usdzFilePath);
    }

    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_convertMetersPerUnit(const long int stage_id, const double metersPerUnit)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        return usd::exporter::revit::core::convertMetersPerUnit(stage, metersPerUnit);
    }

    USD_EXPORTER_REVIT_API double usd_exporter_revit_core_getMetersPerUnitFromFile(const char* filePath)
    {
        if (filePath == nullptr)
        {
            return 0.0;
        }

        std::string path(filePath);
        return usd::exporter::revit::core::getMetersPerUnitFromFile(path);
    }
}

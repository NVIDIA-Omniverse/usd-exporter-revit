// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "MeshAlgo.h"

#include "Log.h"
#include "MemoryUtils.h"
#include "SdfUtils.h"
#include "StageCache.h"
#include "UsdUtils.h"

#include <optional>

#include <pxr/usd/usdGeom/mesh.h>
#include <pxr/usd/usdGeom/primvarsAPI.h>
#include <pxr/usd/usdUtils/pipeline.h>

#include <vector>

using namespace pxr;

namespace
{

// Validate the topology of a mesh to ensure that it is not unused.
bool validateUnusedMeshTopology(
    const VtIntArray& faceVertexCounts,
    const VtIntArray& faceVertexIndices,
    const VtVec3fArray& points,
    std::optional<revit::usd_export::core::Vec3fPrimvarData> normals,
    std::optional<revit::usd_export::core::Vec2fPrimvarData> uvs,
    std::optional<revit::usd_export::core::Vec3fPrimvarData> displayColor,
    std::optional<revit::usd_export::core::FloatPrimvarData> displayOpacity,
    std::string* reason
)
{
    std::vector<bool> usedVertices(points.size(), false);

    // Mark used vertices
    size_t vertexIndex = 0;
    for (size_t faceIndex = 0; faceIndex < faceVertexCounts.size(); ++faceIndex)
    {
        const int vertexCount = faceVertexCounts[faceIndex];
        for (int i = 0; i < vertexCount; ++i)
        {
            usedVertices[faceVertexIndices[vertexIndex + i]] = true;
        }
        vertexIndex += vertexCount;
    }

    // Check if any vertices are not referenced by the faces
    for (size_t i = 0; i < usedVertices.size(); ++i)
    {
        if (!usedVertices[i])
        {
            *reason = TfStringPrintf("Some points are not referenced by the faces");
            return false;
        }
    }

    if (normals.has_value() && normals.value().hasUnindexedValues())
    {
        *reason = TfStringPrintf("There are values that are not referenced by the indices (normals)");
        return false;
    }

    if (uvs.has_value() && uvs.value().hasUnindexedValues())
    {
        *reason = TfStringPrintf("There are values that are not referenced by the indices (uvs)");
        return false;
    }

    if (displayColor.has_value() && displayColor.value().hasUnindexedValues())
    {
        *reason = TfStringPrintf("There are values that are not referenced by the indices (displayColor)");
        return false;
    }

    if (displayOpacity.has_value() && displayOpacity.value().hasUnindexedValues())
    {
        *reason = TfStringPrintf("There are values that are not referenced by the indices (displayOpacity)");
        return false;
    }

    return true;
}

} // namespace

namespace revit::usd_export::core
{

// Validate the interpolation given the topology information
template <typename T>
bool validatePrimvarInterpolation(const revit::usd_export::core::PrimvarData<T>& primvar, const TfTokenVector& interpolations, const VtArray<int>& faceVertexCounts, const VtArray<int>& faceVertexIndices, const VtArray<GfVec3f>& points)
{
    if (std::find(interpolations.begin(), interpolations.end(), primvar.interpolation()) == interpolations.end())
    {
        return false;
    }

    size_t size = primvar.effectiveSize();

    // Constant interpolation requires a single value
    if (primvar.interpolation() == UsdGeomTokens->constant && size == 1)
    {
        return true;
    }

    // Uniform interpolation requires a value for every face on the mesh
    if (primvar.interpolation() == UsdGeomTokens->uniform && size == faceVertexCounts.size())
    {
        return true;
    }

    // Vertex interpolation requires a value for every point in the mesh
    if (primvar.interpolation() == UsdGeomTokens->vertex && size == points.size())
    {
        return true;
    }

    // Face varying interpolation requires a value for every face vertex in the mesh
    if (primvar.interpolation() == UsdGeomTokens->faceVarying && size == faceVertexIndices.size())
    {
        return true;
    }

    return false;
}

// Validate a primvar intended for a mesh.
// Accepts a vector of allowed interpolations and returns false if the PrimvarData is not within these allowed values.
// Validates that a valid interpolation was found and that indices (if provided) fit inside the value range.
// If the primvar is invalid and reason is non-null, an error message describing the validation error will be set.
template <typename T>
bool validatePrimvar(const revit::usd_export::core::PrimvarData<T>& primvar, const TfTokenVector& interpolations, const VtArray<int>& faceVertexCounts, const VtArray<int>& faceVertexIndices, const VtArray<GfVec3f>& points, std::string* reason)
{
    if (!primvar.isValid())
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("The primvar data is invalid.");
        }
        return false;
    }

    if (!validatePrimvarInterpolation<T>(primvar, interpolations, faceVertexCounts, faceVertexIndices, points))
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("The interpolation \"%s\" is not valid for %zu %s", primvar.interpolation().GetText(), primvar.effectiveSize(), primvar.hasIndices() ? "indices" : "values");
        }
        return false;
    }

    return true;
}

UsdGeomMesh definePolyMesh(
    UsdStagePtr stage,
    const SdfPath& path,
    const VtIntArray& faceVertexCounts,
    const VtIntArray& faceVertexIndices,
    const VtVec3fArray& points,
    std::optional<revit::usd_export::core::Vec3fPrimvarData> normals,
    std::optional<revit::usd_export::core::Vec2fPrimvarData> uvs,
    std::optional<revit::usd_export::core::Vec3fPrimvarData> displayColor,
    std::optional<revit::usd_export::core::FloatPrimvarData> displayOpacity
)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(stage, path, &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomMesh due to an invalid location: %s", reason.c_str());
        return UsdGeomMesh();
    }

    // Early out if the points are empty
    if (points.empty())
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid points: Empty array", revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdGeomMesh();
    }

    // Early out if the topology is not valid
    if (!UsdGeomMesh::ValidateTopology(faceVertexIndices, faceVertexCounts, points.size(), &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid topology: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdGeomMesh();
    }

    // Early out if normals were specified but not valid
    if (normals.has_value())
    {
        static const TfTokenVector validInterpolations = { UsdGeomTokens->uniform, UsdGeomTokens->vertex, UsdGeomTokens->faceVarying };
        if (!validatePrimvar(normals.value(), validInterpolations, faceVertexCounts, faceVertexIndices, points, &reason))
        {
            REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid normals: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
            return UsdGeomMesh();
        }
    }

    // Early out if uvs were specified but not valid
    if (uvs.has_value())
    {
        static const TfTokenVector validInterpolations = { UsdGeomTokens->vertex, UsdGeomTokens->faceVarying };
        if (!validatePrimvar(uvs.value(), validInterpolations, faceVertexCounts, faceVertexIndices, points, &reason))
        {
            REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid uvs: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
            return UsdGeomMesh();
        }
    }

    // All interpolations are valid by default
    static const TfTokenVector s_allValidInterpolations = { UsdGeomTokens->constant, UsdGeomTokens->uniform, UsdGeomTokens->varying, UsdGeomTokens->vertex, UsdGeomTokens->faceVarying };

    // Early out if displayColor was specified but not valid
    if (displayColor.has_value())
    {
        if (!validatePrimvar(displayColor.value(), s_allValidInterpolations, faceVertexCounts, faceVertexIndices, points, &reason))
        {
            REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid display color: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
            return UsdGeomMesh();
        }
    }

    // Early out if displayOpacity was specified but not valid
    if (displayOpacity.has_value())
    {
        if (!validatePrimvar(displayOpacity.value(), s_allValidInterpolations, faceVertexCounts, faceVertexIndices, points, &reason))
        {
            REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid display opacity: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
            return UsdGeomMesh();
        }
    }

    // Validation if there are unused references.
    if (!validateUnusedMeshTopology(faceVertexCounts, faceVertexIndices, points, normals, uvs, displayColor, displayOpacity, &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\" due to invalid topology: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdGeomMesh();
    }

    // Define the Mesh and check that this was successful
    UsdGeomMesh mesh = UsdGeomMesh::Define(stage, path);
    if (!mesh)
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomMesh at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdGeomMesh();
    }

    // Explicitly author the specifier and type name
    UsdPrim prim = mesh.GetPrim();
    prim.SetSpecifier(SdfSpecifierDef);
    prim.SetTypeName(prim.GetTypeName());

    // Author opinions on Mesh attributes
    mesh.CreateOrientationAttr().Set(UsdGeomTokens->rightHanded);
    mesh.CreateSubdivisionSchemeAttr().Set(UsdGeomTokens->none);

    // Create and set required topology attributes
    mesh.CreateFaceVertexCountsAttr().Set(faceVertexCounts);
    mesh.CreateFaceVertexIndicesAttr().Set(faceVertexIndices);
    mesh.CreatePointsAttr().Set(points);

    // Compute an extent from the points so there is a guarantee that the extent will be correct and authored in all cases.
    VtArray<GfVec3f> extent;
    UsdGeomPointBased::ComputeExtent(points, &extent);
    mesh.CreateExtentAttr().Set(extent);

    // Optionally author normals
    if (normals.has_value())
    {
        // Define the normals primvar
        const TfToken& name = UsdGeomTokens->normals;
        const SdfValueTypeName& typeName = SdfValueTypeNames->Normal3fArray;
        UsdGeomPrimvar primvar = UsdGeomPrimvarsAPI(mesh.GetPrim()).CreatePrimvar(name, typeName);
        if (!normals.value().setPrimvar(primvar))
        {
            REVIT_LOG_WARN("Failed to set normals primvar for UsdGeomMesh at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        }
    }

    // Optionally author the primary UV set
    if (uvs.has_value())
    {
        const TfToken& name = UsdUtilsGetPrimaryUVSetName();
        const SdfValueTypeName& typeName = SdfValueTypeNames->TexCoord2fArray;
        UsdGeomPrimvar primvar = UsdGeomPrimvarsAPI(mesh.GetPrim()).CreatePrimvar(name, typeName);
        if (!uvs.value().setPrimvar(primvar))
        {
            REVIT_LOG_WARN("Failed to set uvs primvar for UsdGeomMesh at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        }
    }

    // Optionally author display color
    if (displayColor.has_value())
    {
        UsdGeomPrimvar primvar = mesh.CreateDisplayColorPrimvar();
        if (!displayColor.value().setPrimvar(primvar))
        {
            REVIT_LOG_WARN("Failed to set display color primvar for UsdGeomMesh at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        }
    }

    // Optionally author display opacity
    if (displayOpacity.has_value())
    {
        UsdGeomPrimvar primvar = mesh.CreateDisplayOpacityPrimvar();
        if (!displayOpacity.value().setPrimvar(primvar))
        {
            REVIT_LOG_WARN("Failed to set display opacity primvar for UsdGeomMesh at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        }
    }

    return mesh;
}

/**
 * Converted to a style that stores arrays used in mesh.
 */
void convertStorageFromMeshArray(
    const int faceVertexCounts[],
    const size_t faceVertexCountsCount,
    const int faceVertexIndices[],
    const size_t faceVertexIndicesCount,
    const pxr::GfVec3f points[],
    const size_t pointsCount,
    const char* normalsInterporation,
    const pxr::GfVec3f normals[],
    const size_t normalsCount,
    const int normalsIndices[],
    const size_t normalsIndicesCount,
    const char* uvsInterporation,
    const pxr::GfVec2f uvs[],
    const size_t uvsCount,
    const int uvsIndices[],
    const size_t uvsIndicesCount,
    const char* displayColorInterporation,
    const pxr::GfVec3f displayColor[],
    const size_t displayColorCount,
    const int displayColorIndices[],
    const size_t displayColorIndicesCount,
    const char* displayOpacityInterporation,
    const float displayOpacity[],
    const size_t displayOpacityCount,
    const int displayOpacityIndices[],
    const size_t displayOpacityIndicesCount,
    pxr::VtIntArray& _faceVertexCounts,
    pxr::VtIntArray& _faceVertexIndices,
    pxr::VtVec3fArray& _points,
    std::optional<revit::usd_export::core::Vec3fPrimvarData>& _normals,
    std::optional<revit::usd_export::core::Vec2fPrimvarData>& _uvs,
    std::optional<revit::usd_export::core::Vec3fPrimvarData>& _displayColor,
    std::optional<revit::usd_export::core::FloatPrimvarData>& _displayOpacity
)
{
    _faceVertexCounts.clear();
    _faceVertexIndices.clear();
    _points.clear();
    _normals = std::nullopt;
    _uvs = std::nullopt;
    _displayColor = std::nullopt;
    _displayOpacity = std::nullopt;

    if (faceVertexCountsCount > 0 && faceVertexCounts != nullptr)
    {
        _faceVertexCounts.resize(faceVertexCountsCount);
        copyMemoryBuffer(faceVertexCounts, faceVertexCounts + faceVertexCountsCount, _faceVertexCounts.begin(), sizeof(int) * faceVertexCountsCount, sizeof(int) * faceVertexCountsCount);
    }
    if (faceVertexIndicesCount > 0 && faceVertexIndices != nullptr)
    {
        _faceVertexIndices.resize(faceVertexIndicesCount);
        copyMemoryBuffer(faceVertexIndices, faceVertexIndices + faceVertexIndicesCount, _faceVertexIndices.begin(), sizeof(int) * faceVertexIndicesCount, sizeof(int) * faceVertexIndicesCount);
    }
    if (pointsCount > 0 && points != nullptr)
    {
        _points.resize(pointsCount);
        copyMemoryBuffer(points, points + pointsCount, _points.begin(), sizeof(float) * 3 * pointsCount, sizeof(float) * 3 * pointsCount);
    }

    if (normalsCount > 0 && normals != nullptr)
    {
        pxr::VtArray<int> indices;
        if (normalsIndicesCount > 0 && normalsIndices != nullptr)
        {
            indices.resize(normalsIndicesCount);
            copyMemoryBuffer(normalsIndices, normalsIndices + normalsIndicesCount, indices.begin(), sizeof(int) * normalsIndicesCount, sizeof(int) * normalsIndicesCount);
        }
        pxr::VtArray<pxr::GfVec3f> values;
        values.resize(normalsCount);
        copyMemoryBuffer(normals, normals + normalsCount, values.begin(), sizeof(float) * 3 * normalsCount, sizeof(float) * 3 * normalsCount);
        _normals = revit::usd_export::core::Vec3fPrimvarData(pxr::TfToken((normalsInterporation == nullptr) ? "" : normalsInterporation), values, indices);
    }

    if (uvsCount > 0 && uvs != nullptr)
    {
        pxr::VtArray<int> indices;
        if (uvsIndicesCount > 0 && uvsIndices != nullptr)
        {
            indices.resize(uvsIndicesCount);
            copyMemoryBuffer(uvsIndices, uvsIndices + uvsIndicesCount, indices.begin(), sizeof(int) * uvsIndicesCount, sizeof(int) * uvsIndicesCount);
        }
        pxr::VtArray<pxr::GfVec2f> values;
        values.resize(uvsCount);
        copyMemoryBuffer(uvs, uvs + uvsCount, values.begin(), sizeof(float) * 2 * uvsCount, sizeof(float) * 2 * uvsCount);

        _uvs = revit::usd_export::core::Vec2fPrimvarData(pxr::TfToken((uvsInterporation == nullptr) ? "" : uvsInterporation), values, indices);
    }

    if (displayColorCount > 0 && displayColor != nullptr)
    {
        pxr::VtArray<int> indices;
        if (displayColorIndicesCount > 0 && displayColorIndices != nullptr)
        {
            indices.resize(displayColorIndicesCount);
            copyMemoryBuffer(displayColorIndices, displayColorIndices + displayColorIndicesCount, indices.begin(), sizeof(int) * displayColorIndicesCount, sizeof(int) * displayColorIndicesCount);
        }
        pxr::VtArray<pxr::GfVec3f> values;
        values.resize(displayColorCount);
        copyMemoryBuffer(displayColor, displayColor + displayColorCount, values.begin(), sizeof(float) * 3 * displayColorCount, sizeof(float) * 3 * displayColorCount);
        _displayColor = revit::usd_export::core::Vec3fPrimvarData(pxr::TfToken((displayColorInterporation == nullptr) ? "" : displayColorInterporation), values, indices);
    }

    if (displayOpacityCount > 0 && displayOpacity != nullptr)
    {
        pxr::VtArray<int> indices;
        if (displayOpacityIndicesCount > 0 && displayOpacityIndices != nullptr)
        {
            indices.resize(displayOpacityIndicesCount);
            copyMemoryBuffer(displayOpacityIndices, displayOpacityIndices + displayOpacityIndicesCount, indices.begin(), sizeof(int) * displayOpacityIndicesCount, sizeof(int) * displayOpacityIndicesCount);
        }
        pxr::VtArray<float> values;
        values.resize(displayOpacityCount);
        copyMemoryBuffer(displayOpacity, displayOpacity + displayOpacityCount, values.begin(), sizeof(float) * displayOpacityCount, sizeof(float) * displayOpacityCount);
        _displayOpacity = revit::usd_export::core::FloatPrimvarData(pxr::TfToken((displayOpacityInterporation == nullptr) ? "" : displayOpacityInterporation), values, indices);
    }
}
} // namespace revit::usd_export::core

extern "C"
{
    REVIT_USD_EXPORT_API const char* revit_usd_export_core_definePolyMesh(
        const long int stage_id,
        const char* prim_path,
        const int faceVertexCounts[],
        const size_t faceVertexCountsCount,
        const int faceVertexIndices[],
        const size_t faceVertexIndicesCount,
        const pxr::GfVec3f points[],
        const size_t pointsCount,
        const char* normalsInterporation,
        const pxr::GfVec3f normals[],
        const size_t normalsCount,
        const int normalsIndices[],
        const size_t normalsIndicesCount,
        const char* uvsInterporation,
        const pxr::GfVec2f uvs[],
        const size_t uvsCount,
        const int uvsIndices[],
        const size_t uvsIndicesCount,
        const char* displayColorInterporation,
        const pxr::GfVec3f displayColor[],
        const size_t displayColorCount,
        const int displayColorIndices[],
        const size_t displayColorIndicesCount,
        const char* displayOpacityInterporation,
        const float displayOpacity[],
        const size_t displayOpacityCount,
        const int displayOpacityIndices[],
        const size_t displayOpacityIndicesCount
    )
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        // Converted to a style that stores arrays used in mesh.
        pxr::VtIntArray _faceVertexCounts;
        pxr::VtIntArray _faceVertexIndices;
        pxr::VtVec3fArray _points;
        std::optional<revit::usd_export::core::Vec3fPrimvarData> _normals;
        std::optional<revit::usd_export::core::Vec2fPrimvarData> _uvs;
        std::optional<revit::usd_export::core::Vec3fPrimvarData> _displayColor;
        std::optional<revit::usd_export::core::FloatPrimvarData> _displayOpacity;
        convertStorageFromMeshArray(
            faceVertexCounts,
            faceVertexCountsCount,
            faceVertexIndices,
            faceVertexIndicesCount,
            points,
            pointsCount,
            normalsInterporation,
            normals,
            normalsCount,
            normalsIndices,
            normalsIndicesCount,
            uvsInterporation,
            uvs,
            uvsCount,
            uvsIndices,
            uvsIndicesCount,
            displayColorInterporation,
            displayColor,
            displayColorCount,
            displayColorIndices,
            displayColorIndicesCount,
            displayOpacityInterporation,
            displayOpacity,
            displayOpacityCount,
            displayOpacityIndices,
            displayOpacityIndicesCount,
            _faceVertexCounts,
            _faceVertexIndices,
            _points,
            _normals,
            _uvs,
            _displayColor,
            _displayOpacity
        );

        pxr::UsdGeomMesh mesh = definePolyMesh(stage, pxr::SdfPath(prim_path), _faceVertexCounts, _faceVertexIndices, _points, _normals, _uvs, _displayColor, _displayOpacity);
        if (!mesh.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = mesh.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }
}

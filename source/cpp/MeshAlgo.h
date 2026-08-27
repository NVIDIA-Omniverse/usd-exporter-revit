// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include "PrimvarData.h"

#include <optional>

#include <pxr/base/gf/vec2f.h>
#include <pxr/base/gf/vec3f.h>
#include <pxr/usd/usd/stage.h>
#include <pxr/usd/usdGeom/mesh.h>

namespace revit::usd_export::core
{
//! Defines a basic polygon mesh on the stage.
//!
//! Attribute values will be validated and in the case of invalid data the Mesh will not be defined.
//! An invalid [UsdGeomMesh](https://openusd.org/release/api/class_usd_geom_mesh.html) object will be returned in this case.
//!
//! A "Subdivision Scheme" of "None" is authored to ensure that the Mesh is not treated as a subdivision surface.
//! For this reason there is no support for authoring subdivision surface attributes during definition.
//!
//! Values will be authored for all attributes required to completely describe the Mesh, even if weaker matching opinions already exist.
//!
//! - Face Vertex Counts
//! - Face Vertex Indices
//! - Points
//! - Extent
//!
//! The orientation of the Mesh is assumed to be "Right Handed". The winding order of the data should be reversed in advance if that is not the case.
//!
//! The "extent" of the Mesh will be computed and authored based on the `points` provided.
//!
//! The following common primvars can optionally be authored at the same time using a `PrimvarData` to specify interpolation, data,
//! and optionally indices or elementSize.
//!
//! - Normals
//! - Primary UV Set
//! - Display Color
//! - Display Opacity
//!
//! Normals are authored as `primvars:normals` so that indexing is possible and to ensure that the value takes precedence in cases where both
//! `normals` and `primvars:normals` are authored.
//! See [UsdGeomPointBased](https://openusd.org/release/api/class_usd_geom_point_based.html#ac9a057e1f221d9a20b99887f35f84480) for details.
//!
//! The primary uv set will be named based on the result of
//! [UsdUtilsGetPrimaryUVSetName()](https://openusd.org/release/api/pipeline_8h.html#aaba37cce54b9db62e0813003dc61cd07).
//! By default the name is "st" but can be configured by extension.
//! See [UsdUtils Pipeline](https://openusd.org/release/api/pipeline_8h.html#details) for details.
//!
//! @param stage The stage on which to define the mesh
//! @param path The absolute prim path at which to define the mesh
//! @param faceVertexCounts The number of vertices in each face of the mesh
//! @param faceVertexIndices Indices of the positions from the `points` to use for each face vertex
//! @param points Vertex positions for the mesh described in local space
//! @param normals Values to be authored for the normals primvar
//! @param uvs Values to be authored for the uv primvar
//! @param displayColor Values to be authored for the display color primvar
//! @param displayOpacity Values to be authored for the display opacity primvar
//! @returns UsdGeomMesh schema wrapping the defined UsdPrim
REVIT_USD_EXPORT_API pxr::UsdGeomMesh definePolyMesh(
    pxr::UsdStagePtr stage,
    const pxr::SdfPath& path,
    const pxr::VtIntArray& faceVertexCounts,
    const pxr::VtIntArray& faceVertexIndices,
    const pxr::VtVec3fArray& points,
    std::optional<revit::usd_export::core::Vec3fPrimvarData> normals = std::nullopt,
    std::optional<revit::usd_export::core::Vec2fPrimvarData> uvs = std::nullopt,
    std::optional<revit::usd_export::core::Vec3fPrimvarData> displayColor = std::nullopt,
    std::optional<revit::usd_export::core::FloatPrimvarData> displayOpacity = std::nullopt
);
} // namespace revit::usd_export::core

extern "C"
{
    /**
     * Defines a basic polygon mesh on the stage.
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path.
     * @param[in] faceVertexCounts, faceVertexCountsCount   The number of vertices in each face of the mesh.
     * @param[in] faceVertexIndices, faceVertexIndicesCount Indices of the positions from the `points` to use for each face vertex
     * @param[in] points, pointsCount  Vertex positions for the mesh described in local space
     * @param[in] normalsInterporation, normals, normalsCount, normalsIndices, normalsIndicesCount  Values to be authored for the normals primvar
     * @param[in] uvsInterporation, uvs, uvsCount, uvsIndices, uvsIndicesCount  Values to be authored for the uv primvar
     * @param[in] displayColorInterporation, displayColor, displayColorCount, displayColorIndices, displayColorIndicesCount  Values to be authored for the display color primvar
     * @param[in] displayOpacityInterporation, displayOpacity, displayOpacityCount, displayOpacityIndices, displayOpacityIndicesCount  Values to be authored for the display opacity primvar
     * @return  If successful, the mesh's Prim path is returned.
     */
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
    );
}

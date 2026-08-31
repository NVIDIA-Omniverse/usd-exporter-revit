// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include "Types.h"

#include <optional>

#include <pxr/base/gf/matrix4d.h>
#include <pxr/base/gf/transform.h>
#include <pxr/base/gf/vec3d.h>
#include <pxr/usd/sdf/path.h>
#include <pxr/usd/usd/prim.h>
#include <pxr/usd/usd/stage.h>
#include <pxr/usd/usd/timeCode.h>
#include <pxr/usd/usdGeom/xform.h>

namespace usd::exporter::revit::core
{
//! Get the local transform of a prim at a given time in the form of common transform components.
//!
//! @param prim The prim to get local transform from.
//! @param translation Translation result.
//! @param pivot Pivot position result.
//! @param rotation Rotation result in degrees.
//! @param rotationOrder Rotation order the rotation result.
//! @param scale Scale result.
//! @param time Time at which to query the value.
USD_EXPORTER_REVIT_API void getLocalTransformComponents(
    const pxr::UsdPrim& prim,
    pxr::GfVec3d& translation,
    pxr::GfVec3d& pivot,
    pxr::GfVec3f& rotation,
    usd::exporter::revit::core::RotationOrder& rotationOrder,
    pxr::GfVec3f& scale,
    pxr::UsdTimeCode time = pxr::UsdTimeCode::Default()
);

//! Set the local transform of a prim.
//!
//! @param prim The prim to set local transform on.
//! @param transform The transform value to set.
//! @param time Time at which to write the value.
//! @returns A bool indicating if the local transform was set.
USD_EXPORTER_REVIT_API bool setLocalTransform(pxr::UsdPrim prim, const pxr::GfTransform& transform, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Set the local transform of a prim from a 4x4 matrix.
//!
//! @param prim The prim to set local transform on.
//! @param matrix The matrix value to set.
//! @param time Time at which to write the value.
//! @returns A bool indicating if the local transform was set.
USD_EXPORTER_REVIT_API bool setLocalTransform(pxr::UsdPrim prim, const pxr::GfMatrix4d& matrix, pxr::UsdTimeCode time = pxr::UsdTimeCode::Default());

//! Set the local transform of a prim from common transform components.
//!
//! @param prim The prim to set local transform on.
//! @param translation The translation value to set.
//! @param pivot The pivot position value to set.
//! @param rotation The rotation value to set in degrees.
//! @param rotationOrder The rotation order of the rotation value.
//! @param scale The scale value to set.
//! @param time Time at which to write the values.
//! @returns True if the transform was set successfully.
USD_EXPORTER_REVIT_API bool setLocalTransform(
    pxr::UsdPrim prim,
    const pxr::GfVec3d& translation,
    const pxr::GfVec3d& pivot,
    const pxr::GfVec3f& rotation,
    const usd::exporter::revit::core::RotationOrder rotationOrder,
    const pxr::GfVec3f& scale,
    pxr::UsdTimeCode time = pxr::UsdTimeCode::Default()
);

//! Defines an xform on the stage.
//!
//! @param stage The stage on which to define the xform
//! @param path The absolute prim path at which to define the xform
//! @param transform Optional local transform to set
//!
//! @returns UsdGeomXform schema wrapping the defined UsdPrim. Returns an invalid schema on error.
USD_EXPORTER_REVIT_API pxr::UsdGeomXform defineXform(pxr::UsdStagePtr stage, const pxr::SdfPath& path, std::optional<pxr::GfTransform> transform = std::nullopt);
} // namespace usd::exporter::revit::core

extern "C"
{
    /**
     * Set the local transform of a prim.
     * @param[in] stage_id   Stage Id.
     * @param[in] prim_path  The absolute prim path.
     * @param[in] transform  The transform value to set.
     * @return A bool indicating if the local transform was set.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_setLocalTransform(const long int stage_id, const char* prim_path, const double transform[4][4]);

    /**
     * Set the local transform of a prim with a pivot.
     * @param[in] stage_id   Stage Id.
     * @param[in] prim_path  The absolute prim path.
     * @param[in] transform  The transform value to set.
     * @param[in] pivot      The pivot position to set.
     * @return A bool indicating if the local transform was set.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_setLocalTransformPivot(const long int stage_id, const char* prim_path, const double transform[4][4], const pxr::GfVec3d pivot);
    /**
     * Set the local transform of a prim.
     * @param[in] stage_id   Stage Id.
     * @param[in] prim_path  The absolute prim path.
     * @param[in] matrix     The matrix value to set.
     * @return A bool indicating if the local transform was set.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_setLocalTransformMatrix(const long int stage_id, const char* prim_path, const double matrix[4][4]);

    /**
     * Get the local transform of a prim at a given time in the form of common transform components.
     * @param[in] stage_id       Stage Id.
     * @param[in] prim_path      The absolute prim path.
     * @param[out] translation   Translation result.
     * @param[out] pivot         Pivot position result.
     * @param[out] rotation      Rotation result in degrees.
     * @param[out] rotationOrder Rotation order the rotation result.
     * @param[out] scale         Scale result.
     */
    USD_EXPORTER_REVIT_API void
    usd_exporter_revit_core_getLocalTransformComponents(const long int stage_id, const char* prim_path, double* translation[3], double* pivot[3], double* rotation[3], usd::exporter::revit::core::RotationOrder* rotationOrder, double* scale[3]);

    /**
     * Defines an xform on the stage.
     * @param[in] stage_id       Stage Id.
     * @param[in] prim_path      The absolute prim path.
     * @return  If successful, Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_defineXform(const long int stage_id, const char* prim_path);
}

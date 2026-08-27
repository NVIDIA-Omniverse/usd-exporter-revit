// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "XformAlgo.h"

#include "Log.h"
#include "SdfUtils.h"
#include "StageCache.h"
#include "UsdUtils.h"

#include <pxr/base/gf/matrix4d.h>
#include <pxr/base/gf/transform.h>
#include <pxr/base/tf/token.h>
#include <pxr/usd/usdGeom/xform.h>
#include <pxr/usd/usdGeom/xformCommonAPI.h>
#include <pxr/usd/usdGeom/xformOp.h>
#include <pxr/usd/usdGeom/xformable.h>

using namespace pxr;

namespace revit::usd_export::core
{
static const GfRotation g_identityRotation = GfRotation().SetIdentity();
static const GfVec3d g_identityTranslation = GfVec3d(0.0, 0.0, 0.0);

template <class HalfType, class FloatType, class DoubleType, class ValueType>
bool setValueWithPrecision(UsdGeomXformOp& xformOp, const ValueType& value, const UsdTimeCode& time)
{
    switch (xformOp.GetPrecision())
    {
        case UsdGeomXformOp::PrecisionHalf:
        {
            return xformOp.Set(HalfType(FloatType(value)), time);
        }
        case UsdGeomXformOp::PrecisionFloat:
        {
            return xformOp.Set(FloatType(value), time);
        }
        case UsdGeomXformOp::PrecisionDouble:
        {
            return xformOp.Set(DoubleType(value), time);
        }
    }
    return false;
}

// Returns true if the transform has a non-identity pivot orientation
bool hasPivotOrientation(const GfTransform& transform)
{
    return transform.GetPivotOrientation() != g_identityRotation;
}

// Returns true if the transform has a non-identity pivot position
bool hasPivotPosition(const GfTransform& transform)
{
    return transform.GetPivotPosition() != g_identityTranslation;
}

// Ensure that there is an opinion about the xformOpOrder value in the current edit target layer
void ensureXformOpOrderExplicitlyAuthored(UsdGeomXformable& xformable)
{
    UsdAttribute attr = xformable.GetXformOpOrderAttr();
    SdfLayerHandle layer = xformable.GetPrim().GetStage()->GetEditTarget().GetLayer();

    if (!layer->HasSpec(attr.GetPath()))
    {
        VtArray<TfToken> value;
        if (attr.Get(&value))
        {
            attr.Set(value);
        }
    }
}

// Remove all unused xformOps from a prim
void removeUnusedXformOps(UsdGeomXformable& xformable)
{
    UsdPrim prim = xformable.GetPrim();

    bool resetsXformStack;
    std::vector<UsdGeomXformOp> usedXformOps = xformable.GetOrderedXformOps(&resetsXformStack);

    // Get all authored property names and remove xformOp properties
    std::vector<TfToken> propertiesToRemove;
    for (const TfToken& propName : prim.GetAuthoredPropertyNames())
    {
        // Remove all xformOp properties (those starting with "xformOp:")
        if (UsdGeomXformOp::IsXformOp(propName))
        {
            // Check if this xformOp is in the usedXformOps list
            bool isUsed = false;
            for (const UsdGeomXformOp& usedOp : usedXformOps)
            {
                if (usedOp.GetName() == propName)
                {
                    isUsed = true;
                    break;
                }
            }

            // Only add to removal list if not used
            if (!isUsed)
            {
                propertiesToRemove.push_back(propName);
            }
        }
    }

    // Remove the collected properties
    for (const TfToken& propName : propertiesToRemove)
    {
        prim.RemoveProperty(propName);
    }
}

// Compute the XYZ rotation values from a Rotation object via decomposition.
GfVec3d computeXyzRotationsFromRotation(const GfRotation& rotate)
{
    const GfVec3d angles = rotate.Decompose(GfVec3d::ZAxis(), GfVec3d::YAxis(), GfVec3d::XAxis());
    return GfVec3d(angles[2], angles[1], angles[0]);
}

GfVec3i getAxisIndices(const revit::usd_export::core::RotationOrder& rotationOrder)
{
    switch (rotationOrder)
    {
        case revit::usd_export::core::RotationOrder_eXyz:
            return GfVec3i(0, 1, 2);
        case revit::usd_export::core::RotationOrder_eXzy:
            return GfVec3i(0, 2, 1);
        case revit::usd_export::core::RotationOrder_eYxz:
            return GfVec3i(1, 0, 2);
        case revit::usd_export::core::RotationOrder_eYzx:
            return GfVec3i(1, 2, 0);
        case revit::usd_export::core::RotationOrder_eZxy:
            return GfVec3i(2, 0, 1);
        case revit::usd_export::core::RotationOrder_eZyx:
            return GfVec3i(2, 1, 0);
        default:
            // Default rotation order is XYZ.
            return GfVec3i(0, 1, 2);
    }
}

GfRotation computeRotation(const GfVec3f& rotations, const revit::usd_export::core::RotationOrder rotationOrder)
{
    static const GfVec3d xyzAxes[] = { GfVec3d::XAxis(), GfVec3d::YAxis(), GfVec3d::ZAxis() };
    const GfVec3i indices = getAxisIndices(rotationOrder);

    GfRotation rotation = GfRotation(xyzAxes[indices[0]], rotations[indices[0]]);
    if (rotations[indices[1]] != 0.0)
    {
        rotation = rotation * GfRotation(xyzAxes[indices[1]], rotations[indices[1]]);
    }
    if (rotations[indices[2]] != 0.0)
    {
        rotation = rotation * GfRotation(xyzAxes[indices[2]], rotations[indices[2]]);
    }
    return rotation;
}

GfTransform computeTransformFromComponents(const GfVec3d& translation, const GfVec3d& pivot, const GfVec3f& rotation, const revit::usd_export::core::RotationOrder rotationOrder, const GfVec3f& scale)
{
    // TODO: Refactor this to retain rotations greater than 360 degrees.
    // Right now a rotation greater than 360 will only be retained if it is in the first position and the remaining two are zero
    // otherwise the multiply function will compute a new rotation in a lossy manner.

    // Compute a rotation from the rotation vector and rotation order
    GfRotation rotate = computeRotation(rotation, rotationOrder);

    // Build a transform from the components and computed rotation
    GfTransform transform = GfTransform();
    transform.SetTranslation(translation);
    transform.SetPivotPosition(pivot);
    transform.SetRotation(rotate);
    transform.SetScale(GfVec3d(scale));

    return transform;
}

GfMatrix4d computeMatrixFromComponents(const GfVec3d& translation, const GfVec3d& pivot, const GfVec3f& rotation, const revit::usd_export::core::RotationOrder rotationOrder, const GfVec3f& scale)
{
    // Build a transform from the components and return it's internal matrix
    const GfTransform transform = computeTransformFromComponents(translation, pivot, rotation, rotationOrder, scale);
    return transform.GetMatrix();
}

UsdGeomXformCommonAPI::RotationOrder convertRotationOrder(const revit::usd_export::core::RotationOrder& rotationOrder)
{
    switch (rotationOrder)
    {
        case revit::usd_export::core::RotationOrder_eXyz:
            return UsdGeomXformCommonAPI::RotationOrderXYZ;
        case revit::usd_export::core::RotationOrder_eXzy:
            return UsdGeomXformCommonAPI::RotationOrderXZY;
        case revit::usd_export::core::RotationOrder_eYxz:
            return UsdGeomXformCommonAPI::RotationOrderYXZ;
        case revit::usd_export::core::RotationOrder_eYzx:
            return UsdGeomXformCommonAPI::RotationOrderYZX;
        case revit::usd_export::core::RotationOrder_eZxy:
            return UsdGeomXformCommonAPI::RotationOrderZXY;
        case revit::usd_export::core::RotationOrder_eZyx:
            return UsdGeomXformCommonAPI::RotationOrderZYX;
        default:
            // Default rotation order is XYZ.
            return UsdGeomXformCommonAPI::RotationOrderXYZ;
    }
}

// Returns whether the authored xformOps are compatible with a matrix value
// The "transformOp" argument will be populated with the existing xformOp if one is authored
bool getMatrixXformOp(const std::vector<UsdGeomXformOp>& xformOps, UsdGeomXformOp* transformOp)
{
    // If there are no existing xformOps then it is compatible
    if (xformOps.empty())
    {
        return true;
    }

    // If there is more than one xformOp then it is not compatible
    if (xformOps.size() > 1)
    {
        return false;
    }

    // The xformOp it must be of type transform but not and inverse op to be compatible
    if (xformOps[0].GetOpType() == UsdGeomXformOp::TypeTransform && !xformOps[0].IsInverseOp())
    {
        *transformOp = std::move(xformOps[0]);
        return true;
    }

    return false;
}

revit::usd_export::core::RotationOrder convertRotationOrder(const UsdGeomXformCommonAPI::RotationOrder& rotationOrder)
{
    switch (rotationOrder)
    {
        case UsdGeomXformCommonAPI::RotationOrderXYZ:
            return revit::usd_export::core::RotationOrder_eXyz;
        case UsdGeomXformCommonAPI::RotationOrderXZY:
            return revit::usd_export::core::RotationOrder_eXzy;
        case UsdGeomXformCommonAPI::RotationOrderYXZ:
            return revit::usd_export::core::RotationOrder_eYxz;
        case UsdGeomXformCommonAPI::RotationOrderYZX:
            return revit::usd_export::core::RotationOrder_eYzx;
        case UsdGeomXformCommonAPI::RotationOrderZXY:
            return revit::usd_export::core::RotationOrder_eZxy;
        case UsdGeomXformCommonAPI::RotationOrderZYX:
            return revit::usd_export::core::RotationOrder_eZyx;
        default:
            // Default rotation order is XYZ.
            return revit::usd_export::core::RotationOrder_eXyz;
    }
}

// Overloaded version of UsdGeomXformCommonAPI::GetXformVectorsByAccumulation which treats pivot as a double
void getXformVectorsByAccumulation(const UsdGeomXformCommonAPI& xformCommonAPI, GfVec3d* translation, GfVec3d* pivot, GfVec3f* rotation, revit::usd_export::core::RotationOrder* rotationOrder, GfVec3f* scale, const UsdTimeCode time)
{
    // Get the xform vectors in the types expected by the xformCommonAPI
    GfVec3f pivotFloat;
    UsdGeomXformCommonAPI::RotationOrder rotOrder;
    xformCommonAPI.GetXformVectors(translation, rotation, scale, &pivotFloat, &rotOrder, time);

    pivot->Set(pivotFloat[0], pivotFloat[1], pivotFloat[2]);
    *rotationOrder = convertRotationOrder(rotOrder);
}

// Given a 4x4 matrix compute the values of common components
void computeComponentsFromMatrix(const GfMatrix4d& matrix, GfVec3d& translation, GfVec3d& pivot, GfVec3f& rotation, revit::usd_export::core::RotationOrder& rotationOrder, GfVec3f& scale)
{
    // Get the components from the transform and cast to the expected precision
    const GfTransform transform = GfTransform(matrix);
    translation = transform.GetTranslation();
    pivot = transform.GetPivotPosition();

    // Decompose rotation into a rotationOrder of XYZ and convert from double to float
    const GfVec3d rotationDouble = computeXyzRotationsFromRotation(transform.GetRotation());
    rotation.Set(rotationDouble[0], rotationDouble[1], rotationDouble[2]);
    rotationOrder = revit::usd_export::core::RotationOrder_eXyz;

    // Convert scale from double to float
    const GfVec3d scaleDouble = transform.GetScale();
    scale.Set(scaleDouble[0], scaleDouble[1], scaleDouble[2]);
}

void getLocalTransformComponents(const UsdPrim& prim, GfVec3d& translation, GfVec3d& pivot, GfVec3f& rotation, revit::usd_export::core::RotationOrder& rotationOrder, GfVec3f& scale, UsdTimeCode time)
{
    // Initialize as identity
    translation.Set(0.0, 0.0, 0.0);
    pivot.Set(0.0, 0.0, 0.0);
    rotation.Set(0.0, 0.0, 0.0);
    rotationOrder = revit::usd_export::core::RotationOrder_eXyz;
    scale.Set(1.0, 1.0, 1.0);

    // Early out if the prim is not xformable
    UsdGeomXformable xformable(prim);
    if (!xformable)
    {
        return;
    }

    // Attempt to extract existing xformOp values
    UsdGeomXformCommonAPI xformCommonAPI = UsdGeomXformCommonAPI(prim);
    if (xformCommonAPI)
    {
        // Extract transform components
        getXformVectorsByAccumulation(xformCommonAPI, &translation, &pivot, &rotation, &rotationOrder, &scale, time);
        return;
    }

    // Compute the local transform matrix and populate the result from that
    GfMatrix4d matrix;
    bool resetsXformStack;
    if (xformable.GetLocalTransformation(&matrix, &resetsXformStack, time))
    {
        computeComponentsFromMatrix(matrix, translation, pivot, rotation, rotationOrder, scale);
        return;
    }
}

bool setLocalTransform(UsdPrim prim, const GfTransform& transform, UsdTimeCode time)
{
    // Early out with a failure return if the prim is not xformable
    UsdGeomXformable xformable(prim);
    if (!xformable)
    {
        return false;
    }

    // Assuming there is no existing compatible xformOpOrder inspect the transform to identify the most expressive xformOpOrder to use.
    // For performance reasons we want to use a single transform xformOp. See: https://groups.google.com/g/usd-interest/c/MR5DFhQEYSE/m/o7bSnWwNAgAJ

    // However we would ideally retain pivot position so if authored prefer the XformCommonAPI.
    // The XformCommonAPI cannot express pivotOrientation so if it has a non-identity value we need to use a transform xformOp.
    bool needsXformCommonAPI = (hasPivotPosition(transform) && !hasPivotOrientation(transform));

    // Get the existing xformOps and attempt to reuse them if compatible
    bool resetsXformStack;
    std::vector<UsdGeomXformOp> xformOps = xformable.GetOrderedXformOps(&resetsXformStack);
    if (!xformOps.empty())
    {
        // Only try to reuse the matrix xform op if the transform does not need the xformCommonAPI to express it's value
        if (!needsXformCommonAPI)
        {
            // Set the value on an existing transform xformOp if one is already authored
            UsdGeomXformOp transformXformOp;
            if (getMatrixXformOp(xformOps, &transformXformOp) && transformXformOp.IsDefined())
            {
                const GfMatrix4d matrix = transform.GetMatrix();
                transformXformOp.Set(matrix, time);
                removeUnusedXformOps(xformable);
                ensureXformOpOrderExplicitlyAuthored(xformable);

                return true;
            }
        }

        // TODO: Attempt to reuse existing UsdGeomXformCommonAPI xformOps
    }

    // Author using UsdGeomXformCommonAPI if appropriate
    if (needsXformCommonAPI)
    {
        // Modify the xformOpOrder and set xformOp values to achieve the transform
        if (!UsdGeomXformCommonAPI(prim))
        {
            xformable.ClearXformOpOrder();
        }

        const GfVec3d rotation = computeXyzRotationsFromRotation(transform.GetRotation());

        // Get or create the UsdGeomXformCommonAPI xformOps
        UsdGeomXformCommonAPI xformCommonAPI = UsdGeomXformCommonAPI(prim);
        UsdGeomXformCommonAPI::Ops commonXformOps = xformCommonAPI.CreateXformOps(UsdGeomXformCommonAPI::RotationOrderXYZ, UsdGeomXformCommonAPI::OpTranslate, UsdGeomXformCommonAPI::OpPivot, UsdGeomXformCommonAPI::OpRotate, UsdGeomXformCommonAPI::OpScale);

        // Set the UsdGeomXformCommonAPI xformOp values allowing setValueWithPrecision to handle any value type conversions
        setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3d>(commonXformOps.translateOp, transform.GetTranslation(), time);
        setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3d>(commonXformOps.pivotOp, transform.GetPivotPosition(), time);
        setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3d>(commonXformOps.rotateOp, rotation, time);
        setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3d>(commonXformOps.scaleOp, transform.GetScale(), time);
        removeUnusedXformOps(xformable);
        ensureXformOpOrderExplicitlyAuthored(xformable);

        return true;
    }

    // Modify the xformOpOrder and set xformOp values to achieve the transform
    const GfMatrix4d matrix = transform.GetMatrix();
    UsdGeomXformOp transformXformOp = xformable.MakeMatrixXform();
    transformXformOp.Set(matrix, time);
    removeUnusedXformOps(xformable);
    ensureXformOpOrderExplicitlyAuthored(xformable);

    return true;
}

bool setLocalTransform(UsdPrim prim, const GfMatrix4d& matrix, UsdTimeCode time)
{
    // Early out with a failure return if the prim is not xformable
    UsdGeomXformable xformable(prim);
    if (!xformable)
    {
        return false;
    }

    // Get the existing xformOps and attempt to reuse them if compatible
    bool resetsXformStack;
    std::vector<UsdGeomXformOp> xformOps = xformable.GetOrderedXformOps(&resetsXformStack);
    if (!xformOps.empty())
    {
        // Set the value on an existing transform xformOp if one is already authored
        UsdGeomXformOp transformXformOp;
        if (getMatrixXformOp(xformOps, &transformXformOp) && transformXformOp.IsDefined())
        {
            transformXformOp.Set(matrix, time);
            removeUnusedXformOps(xformable);
            ensureXformOpOrderExplicitlyAuthored(xformable);

            return true;
        }

        // TODO: Attempt to reuse existing UsdGeomXformCommonAPI xformOps
    }

    // Assuming there is no existing compatible xformOpOrder
    // Modify the xformOpOrder to use the most expressive xformOp stack and set xformOp values to achieve the transform
    UsdGeomXformOp transformXformOp = xformable.MakeMatrixXform();
    transformXformOp.Set(matrix, time);
    removeUnusedXformOps(xformable);
    ensureXformOpOrderExplicitlyAuthored(xformable);

    return true;
}

bool setLocalTransform(UsdPrim prim, const GfVec3d& translation, const GfVec3d& pivot, const GfVec3f& rotation, const revit::usd_export::core::RotationOrder rotationOrder, const GfVec3f& scale, UsdTimeCode time)
{
    // Early out with a failure return if the prim is not xformable
    UsdGeomXformable xformable(prim);
    if (!xformable)
    {
        return false;
    }

    // We would ideally retain pivot position so if it is non-identity prefer the XformCommonAPI.
    bool needsXformCommonAPI = (pivot != g_identityTranslation);

    // Get the existing xformOps and attempt to reuse them if compatible
    bool resetsXformStack;
    std::vector<UsdGeomXformOp> xformOps = xformable.GetOrderedXformOps(&resetsXformStack);
    if (!xformOps.empty())
    {
        // Only try to reuse the matrix xform op if the transform does not need the xformCommonAPI to express it's value
        if (!needsXformCommonAPI)
        {
            // Set the value on an existing transform xformOp if one is already authored
            UsdGeomXformOp transformXformOp;
            if (getMatrixXformOp(xformOps, &transformXformOp) && transformXformOp.IsDefined())
            {
                const GfMatrix4d matrix = computeMatrixFromComponents(translation, pivot, rotation, rotationOrder, scale);
                transformXformOp.Set(matrix, time);
                ensureXformOpOrderExplicitlyAuthored(xformable);

                return true;
            }
        }

        // TODO: Attempt to reuse existing UsdGeomXformCommonAPI xformOps
    }

    // Modify the xformOpOrder and set xformOp values to achieve the transform
    if (!UsdGeomXformCommonAPI(prim))
    {
        xformable.ClearXformOpOrder();
    }

    const UsdGeomXformCommonAPI::RotationOrder rotationOrderEnum = convertRotationOrder(rotationOrder);

    // Get or create the UsdGeomXformCommonAPI xformOps
    UsdGeomXformCommonAPI xformCommonAPI = UsdGeomXformCommonAPI(prim);
    UsdGeomXformCommonAPI::Ops commonXformOps = xformCommonAPI.CreateXformOps(rotationOrderEnum, UsdGeomXformCommonAPI::OpTranslate, UsdGeomXformCommonAPI::OpPivot, UsdGeomXformCommonAPI::OpRotate, UsdGeomXformCommonAPI::OpScale);

    // Set the UsdGeomXformCommonAPI xformOp values allowing setValueWithPrecision to handle any value type conversions
    setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3d>(commonXformOps.translateOp, translation, time);
    setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3d>(commonXformOps.pivotOp, pivot, time);
    setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3f>(commonXformOps.rotateOp, rotation, time);
    setValueWithPrecision<GfVec3h, GfVec3f, GfVec3d, GfVec3f>(commonXformOps.scaleOp, scale, time);

    removeUnusedXformOps(xformable);
    ensureXformOpOrderExplicitlyAuthored(xformable);

    return true;
}

UsdGeomXform defineXform(UsdStagePtr stage, const SdfPath& path, std::optional<pxr::GfTransform> transform)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(stage, path, &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomXform due to an invalid location: %s", reason.c_str());
        return UsdGeomXform();
    }

    // Define the Xform and check that this was successful
    UsdGeomXform xform = UsdGeomXform::Define(stage, path);
    if (!xform)
    {
        REVIT_LOG_ERROR("Unable to define UsdGeomXform at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdGeomXform();
    }

    // Explicitly author the specifier and type name
    UsdPrim prim = xform.GetPrim();
    prim.SetSpecifier(SdfSpecifierDef);
    prim.SetTypeName(prim.GetTypeName());

    // Set the local transform if one was supplied
    if (transform.has_value())
    {
        setLocalTransform(prim, transform.value(), UsdTimeCode::Default());
    }

    return xform;
}
} // namespace revit::usd_export::core

extern "C"
{
    REVIT_USD_EXPORT_API bool revit_usd_export_core_setLocalTransform(const long int stage_id, const char* prim_path, const double transform[4][4])
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return false;
        }

        const pxr::GfMatrix4d m(transform);
        const pxr::GfTransform _tTransform(m);
        return revit::usd_export::core::setLocalTransform(prim, _tTransform);
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_setLocalTransformPivot(const long int stage_id, const char* prim_path, const double transform[4][4], const pxr::GfVec3d pivot)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return false;
        }

        const pxr::GfMatrix4d m(transform);
        pxr::GfTransform _tTransform(m);
        _tTransform.SetPivotPosition(pivot);
        return revit::usd_export::core::setLocalTransform(prim, _tTransform);
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_setLocalTransformMatrix(const long int stage_id, const char* prim_path, const double matrix[4][4])
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return false;
        }

        const pxr::GfMatrix4d m(matrix);
        return revit::usd_export::core::setLocalTransform(prim, m);
    }

    REVIT_USD_EXPORT_API void revit_usd_export_core_getLocalTransformComponents(const long int stage_id, const char* prim_path, double* translation[3], double* pivot[3], double* rotation[3], revit::usd_export::core::RotationOrder* rotationOrder, double* scale[3])
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return;
        }

        pxr::GfVec3d _translation;
        pxr::GfVec3d _pivot;
        pxr::GfVec3f _rotation;
        revit::usd_export::core::RotationOrder _rotationOrder;
        pxr::GfVec3f _scale;
        revit::usd_export::core::getLocalTransformComponents(prim, _translation, _pivot, _rotation, _rotationOrder, _scale);

        revit::usd_export::core::CacheTransformData tempTransformData;
        tempTransformData.translation = pxr::GfVec3d(_translation[0], _translation[1], _translation[2]);
        tempTransformData.pivot = pxr::GfVec3d(_pivot[0], _pivot[1], _pivot[2]);
        tempTransformData.rotation = pxr::GfVec3d((double)_rotation[0], (double)_rotation[1], (double)_rotation[2]);
        tempTransformData.scale = pxr::GfVec3d((double)_scale[0], (double)_scale[1], (double)_scale[2]);
        tempTransformData.rotationOrder = _rotationOrder;

        // Returns a temporary buffer for each stage (thread-safe).
        revit::usd_export::core::CacheTransformData& transformData = revit::usd_export::core::stageCache.getTempData(stage_id, tempTransformData);

        if (translation != nullptr)
        {
            *translation = &transformData.translation[0];
        }
        if (pivot != nullptr)
        {
            *pivot = &transformData.pivot[0];
        }
        if (rotation != nullptr)
        {
            *rotation = &transformData.rotation[0];
        }
        if (scale != nullptr)
        {
            *scale = &transformData.scale[0];
        }
        if (rotationOrder != nullptr)
        {
            *rotationOrder = transformData.rotationOrder;
        }
    }

    REVIT_USD_EXPORT_API const char* revit_usd_export_core_defineXform(const long int stage_id, const char* prim_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdGeomXform _transform = revit::usd_export::core::defineXform(stage, pxr::SdfPath(prim_path));

        if (!_transform.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = _transform.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }
}

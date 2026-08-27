// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "UsdGeom.h"

#include "StageCache.h"
#include "Types.h"
#include "XformAlgo.h"

#include <pxr/usd/usdGeom/cylinder.h>
#include <pxr/usd/usdGeom/metrics.h>
#include <pxr/usd/usdGeom/scope.h>

extern "C"
{
    REVIT_USD_EXPORT_API const char* pxr_usd_defineCylinder(const long stage_id, const char* parent_path, const char* name, const pxr::GfVec3f start, const pxr::GfVec3f end, const double radius)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim parentPrim = stage->GetPrimAtPath(pxr::SdfPath(parent_path));
        if (!parentPrim)
        {
            return nullptr;
        }

        pxr::SdfPath cylinderPath = parentPrim.GetPath().AppendChild(pxr::TfToken(name));
        pxr::UsdGeomCylinder cylinder = pxr::UsdGeomCylinder::Define(stage, cylinderPath);
        const std::string newPath = cylinder.GetPath().GetAsString();
        pxr::UsdPrim prim = cylinder.GetPrim();

        if (prim)
        {
            const float tolerance = 0.1f;

            float p1x = start[0];
            float p1y = start[1];
            float p1z = start[2];
            // this tolerance adjustment helps how?
            float p2x = (p1x - end[0] < tolerance && p1x - end[0] > -tolerance) ? start[0] : end[0];
            float p2y = (p1y - end[1] < tolerance && p1y - end[1] > -tolerance) ? start[1] : end[1];
            float p2z = (p1z - end[2] < tolerance && p1z - end[2] > -tolerance) ? start[2] : end[2];

            // calc length of line or "height" of the cylinder
            const double height = std::pow((std::pow((p2x - p1x), 2) + std::pow((p2y - p1y), 2) + std::pow((p2z - p1z), 2)), 0.5);

            cylinder.CreateHeightAttr(pxr::VtValue(height));
            cylinder.CreateRadiusAttr(pxr::VtValue(radius));

            pxr::TfToken upAxis = pxr::UsdGeomGetStageUpAxis(stage);
            bool isVertical = (upAxis == pxr::UsdGeomTokens->z) ? p1x == p2x && p1y == p2y : p1x == p2x && p1z == p2z;
            bool isHorizontal = (upAxis == pxr::UsdGeomTokens->z) ? p1z == p2z : p1y == p2y;

            // transform it into place

            // calc midpoint for translation from origin
            const pxr::GfVec3d translation((p1x + p2x) / 2.0, (p1y + p2y) / 2.0, (p1z + p2z) / 2.0);
            const pxr::GfVec3d pivot(0, 0, 0);

            // calc rotation
            double rX, rY, rZ;
            if (isHorizontal || isVertical)
            {
                if (upAxis == pxr::UsdGeomTokens->z)
                {
                    // up down rotation - x axis
                    // line: (p1.y, p1.z), (p2.y, p2.z)
                    rX = (p2y != p1y) ? (std::atan((p2z - p1z) / (p2y - p1y)) * (180.0 / M_PI)) + 90 : 0;
                    // also up down rotation? - y axis
                    // line: (p1.x, p1,z), (p2.x, p2.z)
                    rY = (p2x != p1x) ? (std::atan((p2z - p1z) / (p2x - p1x)) * (180.0 / M_PI)) + 90 : 0;
                    // left right rotaion - z axis
                    // line: (p1.x, p1.y), (p2.x, p2.y)
                    rZ = (p2x != p1x) ? (std::atan((p2y - p1y) / (p2x - p1x)) * (180.0 / M_PI)) + 90 : 0;
                }
                else
                {
                    if (isVertical)
                    {
                        rX = 90;
                        rY = 0;
                        rZ = 0;
                    }
                    else
                    {
                        // up down rotation - x axis
                        // line: (p1.y, p1.z), (p2.y, p2.z)
                        rX = (p2y != p1y) ? (std::atan((p2z - p1z) / (p2y - p1y)) * (180.0 / M_PI)) : 0;
                        // also up down rotation? - y axis
                        // line: (p1.x, p1,z), (p2.x, p2.z)
                        rY = (p2x != p1x) ? -(std::atan((p2z - p1z) / (p2x - p1x)) * (180.0 / M_PI)) - 90 : 0;
                        // left right rotaion - z axis
                        // line: (p1.x, p1.y), (p2.x, p2.y)
                        rZ = (p2x != p1x) ? (std::atan((p2y - p1y) / (p2x - p1x)) * (180.0 / M_PI)) : 0;
                    }
                }
            }
            else
            {
                double xySlope = (p2x != p1x) ? ((p2y - p1y) / (p2x - p1x)) : M_PI / 2;
                double planRotationRadians = std::atan(xySlope) + (M_PI / 2);
                double planCos = std::cos(-planRotationRadians);
                double planSin = std::sin(-planRotationRadians);

                // up down rotation - x axis
                // line: (p1.y, p1.z), (p2.y, p2.z)
                double r1y, r2y;
                r1y = (p1y * planCos) + (p1x * planSin);
                r2y = (p2y * planCos) + (p2x * planSin);
                if (p1x != p2x)
                {
                    rX = (r2y == r1y) ? 0 : std::atan((p2z - p1z) / (r2y - r1y)) * (180.0 / M_PI) + 90;
                }
                else
                {
                    rX = (p2y == p1y) ? 0 : (std::atan((p2z - p1z) / (p2y - p1y)) * (180.0 / M_PI)) + 90;
                }
                rY = 0;
                // left right rotaion - z axis
                // line: (p1.x, p1.y), (p2.x, p2.y)
                rZ = (p2x != p1x) ? (planRotationRadians * (180.0 / M_PI)) : 0;
            }
            rX = (std::isfinite(rX)) ? rX : 0;
            rY = (std::isfinite(rY)) ? rY : 0;
            rZ = (std::isfinite(rZ)) ? rZ : 0;

            if (rX == 0.0 && (rY < 91.0 && rY > 89.0) && (rZ < 91.0 && rZ > 89.0))
            {
                rX = 90.0;
            }
            const pxr::GfVec3f rotation((float)rX, (float)rY, (float)rZ);
            const revit::usd_export::core::RotationOrder rotationOrder = revit::usd_export::core::RotationOrder_eXyz;
            const pxr::GfVec3f scale(1.0f, 1.0f, 1.0f);

            if (revit::usd_export::core::setLocalTransform(prim, translation, pivot, rotation, rotationOrder, scale))
            {
                pxr::VtVec3fArray extent;
                cylinder.ComputeExtent(height, radius, upAxis, &extent);
                cylinder.CreateExtentAttr(pxr::VtValue(extent));
                std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
                return buff.c_str();
            }
        }
        return nullptr;
    }

    REVIT_USD_EXPORT_API const char* pxr_usd_defineScope(const long int stage_id, const char* parent_path, const char* name)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim parentPrim = stage->GetPrimAtPath(pxr::SdfPath(parent_path));
        if (!parentPrim)
        {
            return nullptr;
        }

        pxr::SdfPath scopePath = parentPrim.GetPath().AppendChild(pxr::TfToken(name));
        pxr::UsdGeomScope scope = pxr::UsdGeomScope::Define(stage, scopePath);
        const std::string newPath = scope.GetPath().GetAsString();

        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);

        return buff.c_str();
    }
}

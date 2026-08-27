// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "UsdVariantSet.h"

#include "StageCache.h"

#include <pxr/usd/usd/variantSets.h>

extern "C"
{
    REVIT_USD_EXPORT_API void pxr_usd_addVariantSet(const long stage_id, const char* prim_path, const char* set_name)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim)
        {
            return;
        }

        if (!prim.GetVariantSet(set_name))
        {
            prim.GetVariantSets().AddVariantSet(set_name);
        }
    }

    REVIT_USD_EXPORT_API void pxr_usd_addVariantOption(const long stage_id, const char* prim_path, const char* set_name, const char* option_name)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim)
        {
            return;
        }

        pxr::UsdVariantSet set = prim.GetVariantSet(set_name);
        if (set)
        {
            if (!set.HasAuthoredVariant(option_name))
            {
                set.AddVariant(option_name);
            }
            set.SetVariantSelection(option_name);
        }
    }

    REVIT_USD_EXPORT_API void pxr_usd_setVariantSelection(const long stage_id, const char* prim_path, const char* set_name, const char* option_name)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim)
        {
            return;
        }

        pxr::UsdVariantSet set = prim.GetVariantSet(set_name);
        if (set)
        {
            if (set.HasAuthoredVariant(option_name))
            {
                set.SetVariantSelection(option_name);
            }
        }
    }
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "UsdStage.h"

#include "StageCache.h"

#include <pxr/usd/usd/variantSets.h>

extern "C"
{
    USD_EXPORTER_REVIT_API bool pxr_usd_stageSetVariantEditTarget(const long stage_id, const char* prim_path, const char* set_name, const char* option_name)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim)
        {
            return false;
        }

        pxr::UsdVariantSet set = prim.GetVariantSet(set_name);
        if (set)
        {
            set.SetVariantSelection(option_name);
            pxr::UsdEditTarget target = set.GetVariantEditTarget();
            stage->SetEditTarget(target);
            return true;
        }
        return false;
    }

    USD_EXPORTER_REVIT_API void pxr_usd_stageSetRootEditTarget(const long stage_id)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::SdfLayerHandle layer = stage->GetRootLayer();
        pxr::UsdEditTarget target = pxr::UsdEditTarget(layer);
        stage->SetEditTarget(target);
    }

    USD_EXPORTER_REVIT_API void pxr_usd_stageSetSessionEditTarget(const long stage_id)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::SdfLayerHandle layer = stage->GetSessionLayer();
        pxr::UsdEditTarget target = pxr::UsdEditTarget(layer);
        stage->SetEditTarget(target);
    }
}

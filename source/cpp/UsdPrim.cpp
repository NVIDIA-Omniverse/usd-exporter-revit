// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "UsdPrim.h"

#include "Log.h"
#include "StageCache.h"

#include <pxr/usd/kind/registry.h>
#include <pxr/usd/usd/modelAPI.h>
#include <pxr/usd/usd/payloads.h>
#include <pxr/usd/usd/tokens.h>
#include <pxr/usd/usdGeom/gprim.h>
#include <pxr/usd/usdGeom/primvar.h>
#include <pxr/usd/usdGeom/primvarsAPI.h>

namespace
{
static const pxr::TfToken kClassToken{ "Class" };
static const pxr::TfToken kDoNotCastShadowsToken{ "doNotCastShadows" };

pxr::TfToken convertKindToToken(const revit::usd_export::core::Kind kind)
{
    switch (kind)
    {
        case revit::usd_export::core::Kind::Kind_eAssembly:
            return pxr::KindTokens->assembly;
            break;
        case revit::usd_export::core::Kind::Kind_eComponent:
            return pxr::KindTokens->component;
            break;
        case revit::usd_export::core::Kind::Kind_eGroup:
            return pxr::KindTokens->group;
            break;
        case revit::usd_export::core::Kind::Kind_eModel:
            return pxr::KindTokens->model;
            break;
        case revit::usd_export::core::Kind::Kind_eSubcomponent:
            return pxr::KindTokens->subcomponent;
            break;
    }
    return pxr::KindTokens->component;
}
} // namespace

extern "C"
{
    REVIT_USD_EXPORT_API const char* pxr_usd_defineClass(const long stage_id, const char* parent_path, const char* name)
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

        pxr::SdfPath classPath = parentPrim.GetPath().AppendChild(pxr::TfToken(name));
        pxr::UsdPrim prim = stage->DefinePrim(classPath);
        if (!prim)
        {
            return nullptr;
        }

        prim.SetSpecifier(pxr::SdfSpecifierClass);

        const std::string newPath = prim.GetPath().GetAsString();
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    REVIT_USD_EXPORT_API void pxr_usd_setInstanceable(const long stage_id, const char* prim_path, bool value)
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

        prim.SetInstanceable(value);
    }

    REVIT_USD_EXPORT_API void pxr_usd_setVisibility(const long stage_id, const char* prim_path, bool value)
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

        pxr::UsdGeomImageable imageable(prim);
        if (!imageable)
        {
            return;
        }

        if (value)
        {
            imageable.MakeVisible();
        }
        else
        {
            imageable.MakeInvisible();
        }
    }

    REVIT_USD_EXPORT_API void pxr_usd_setDoNotCastShadows(const long stage_id, const char* gprim_path, bool value)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(gprim_path));
        if (!prim)
        {
            return;
        }

        if (prim.IsA<pxr::UsdGeomGprim>())
        {
            pxr::UsdGeomGprim gprim = pxr::UsdGeomGprim(prim);
            pxr::UsdGeomPrimvar noShadows = pxr::UsdGeomPrimvarsAPI(gprim).CreatePrimvar(kDoNotCastShadowsToken, pxr::SdfValueTypeNames->Bool);
            if (noShadows)
            {
                noShadows.Set(value);
            }
        }
    }

    REVIT_USD_EXPORT_API void pxr_usd_setKind(const long stage_id, const char* prim_path, const revit::usd_export::core::Kind kind)
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

        pxr::UsdModelAPI modelApi(prim);
        pxr::TfToken token = convertKindToToken(kind);
        modelApi.SetKind(token);
    }

    REVIT_USD_EXPORT_API void pxr_usd_createStringAttribute(const long stage_id, const char* prim_path, const char* name, const char* value)
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

        prim.CreateAttribute(pxr::TfToken(name), pxr::SdfValueTypeNames->String).Set(value);
    }

    REVIT_USD_EXPORT_API void pxr_usd_setAttributeDisplayName(const long stage_id, const char* prim_path, const char* attr_name, const char* display_name)
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

        pxr::UsdAttribute attr = prim.GetAttribute(pxr::TfToken(attr_name));
        if (!attr)
        {
            return;
        }

        attr.SetDisplayName(display_name);
    }

    REVIT_USD_EXPORT_API void pxr_usd_addPayload(const long stage_id, const char* prim_path, const char* payload_path)
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

        prim.GetPayloads().AddPayload(payload_path);
    }

    REVIT_USD_EXPORT_API void pxr_usd_addReference(const long stage_id, const char* prim_path, const char* reference_path)
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

        prim.GetReferences().AddReference(reference_path);
    }

    REVIT_USD_EXPORT_API void pxr_usd_addInternalReference(const long stage_id, const char* prim_path, const char* reference_path)
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

        prim.GetReferences().AddInternalReference(pxr::SdfPath(reference_path));
    }

    REVIT_USD_EXPORT_API void pxr_usd_setPrimToOver(long stage_id, const char* prim_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage)
        {
            std::string pathString(prim_path);
            if (pxr::SdfPath::IsValidPathString(pathString))
            {
                pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(pathString));
                if (prim)
                {
                    prim.SetSpecifier(pxr::SdfSpecifierOver);
                    prim.ClearTypeName();
                }
                else
                {
                    REVIT_LOG_WARN("setPrimToOver -> invalid prim at path: %s", prim_path);
                }
            }
            else
            {
                REVIT_LOG_WARN("setPrimToOver -> invalid path string: %s", prim_path);
            }
        }
        else
        {
            REVIT_LOG_WARN("setPrimToOver -> invalid stage with id: %s", stage_id);
        }
    }
}

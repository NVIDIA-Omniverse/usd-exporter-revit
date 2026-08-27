// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include "Types.h"

extern "C"
{
    /**
     * Define a UsdPrim of type Class as a child of the parent prim.
     * @param[in] stage_id            Stage Id.
     * @param[in] parent_path         Absolute path to the parent prim.
     * @param[in] name                Name of the class prim.
     * @return  If successful, the class's Prim path is returned.
     */
    REVIT_USD_EXPORT_API const char* pxr_usd_defineClass(const long stage_id, const char* parent_path, const char* name);

    /**
     * Sets a UsdPrim Instanceable flag to value.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] value               Boolean value.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setInstanceable(const long stage_id, const char* prim_path, bool value);

    /**
     * Sets a UsdPrim Visibility flag to value.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] value               Boolean value.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setVisibility(const long stage_id, const char* prim_path, bool value);

    /**
     * Sets a UsdGeomGprim primvar for doNotCastShadows to value.
     * @param[in] stage_id            Stage Id.
     * @param[in] gprim_path          Absolute path to the prim.
     * @param[in] value               Boolean value.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setDoNotCastShadows(const long stage_id, const char* gprim_path, bool value);

    /**
     * Sets a UsdPrim's Kind using the UsdModelAPI.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] kind                Kind value.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setKind(const long stage_id, const char* prim_path, const revit::usd_export::core::Kind kind);

    /**
     * Creates a UsdAttribute with a string value on a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] name                Attribute name.
     * @param[in] value               Value for the attribute.
     */
    REVIT_USD_EXPORT_API void pxr_usd_createStringAttribute(const long stage_id, const char* prim_path, const char* name, const char* value);

    /**
     * Sets the Display Name on a UsdAttribute of a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] attr_name           Attribute name.
     * @param[in] display_name        Display name for the attribute.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setAttributeDisplayName(const long stage_id, const char* prim_path, const char* attr_name, const char* display_name);

    /**
     * Adds a Payload to a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] payload_path        Path to the payload.
     */
    REVIT_USD_EXPORT_API void pxr_usd_addPayload(const long stage_id, const char* prim_path, const char* payload_path);

    /**
     * Adds a Reference to a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] reference_path      Path to the reference.
     */
    REVIT_USD_EXPORT_API void pxr_usd_addReference(const long stage_id, const char* prim_path, const char* reference_path);

    /**
     * Adds an internal Reference to a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     * @param[in] reference_path      Absolute path to the prototype prim.
     */
    REVIT_USD_EXPORT_API void pxr_usd_addInternalReference(const long stage_id, const char* prim_path, const char* reference_path);

    /**
     * Sets a UsdPrim to Over.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Absolute path to the prim.
     */
    REVIT_USD_EXPORT_API void pxr_usd_setPrimToOver(long stage_id, const char* prim_path);
}

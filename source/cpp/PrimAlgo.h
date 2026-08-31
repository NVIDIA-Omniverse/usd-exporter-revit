// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <pxr/usd/usd/prim.h>

#include <string>
#include <vector>

namespace usd::exporter::revit::core
{

//! @defgroup prim_displayname UsdPrim Display Name Functions
//!
//! Utility functions for interacting with the display name metadata of `UsdPrims`
//! @{

//! Return this prim's display name (metadata)
//!
//! @param prim The prim to get the display name from
//! @returns Authored value, or an empty string if no display name has been set
USD_EXPORTER_REVIT_API std::string getDisplayName(const pxr::UsdPrim& prim);

//! Sets this prim's display name (metadata).
//!
//! DisplayName is meant to be a descriptive label, not necessarily an alternate identifier; therefore there is no restriction on which characters can
//! appear in it
//!
//! @param prim The prim to set the display name for
//! @param name The value to set
//! @returns True on success, otherwise false
USD_EXPORTER_REVIT_API bool setDisplayName(pxr::UsdPrim prim, const std::string& name);

//! Utility functions to generate valid names for `UsdPrims`
//!
//! @{

//! Produce a valid prim name using the Bootstring algorithm.
//!
//! This is a lossless encoding algorithm that supports all UTF-8 code set (even control characters).
//! Transcoding can be disabled via the `USD_EXPORTER_REVIT_ENABLE_TRANSCODING` environment variable, in which case invalid
//! characters are replaced with "_". See
//! https://github.com/PixarAnimationStudios/OpenUSD-proposals/tree/main/proposals/transcoding_invalid_identifiers for details.
//!
//! @param name The input name
//! @returns A string that is considered valid for use as a prim name.
USD_EXPORTER_REVIT_API std::string getValidPrimName(const std::string& name);

//! Take a vector of preferred names and return a matching vector of valid and unique names.
//!
//! Each name is made valid (see `getValidPrimName`) and then made unique against the other supplied names and any reserved names by appending a
//! numeric suffix (`_1`, `_2`, ...). The returned vector is ordered to match the input `names`.
//!
//! @param names A vector of preferred prim names.
//! @param reservedNames A vector of reserved prim names. Names in this vector will not be returned.
//! @returns A vector of valid and unique names.
USD_EXPORTER_REVIT_API std::vector<std::string> getValidPrimNames(const std::vector<std::string>& names, const std::vector<std::string>& reservedNames = {});


} // namespace usd::exporter::revit::core

extern "C"
{
    /**
     * Return this prim's display name (metadata).
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path.
     * @return this prim's display name (metadata)
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_getDisplayName(const long int stage_id, const char* prim_path);

    /**
     * Sets this prim's display name (metadata).
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path.
     * @param[in] name          The value to set.
     * @return True on success, otherwise false
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_setDisplayName(const long int stage_id, const char* prim_path, const char* name);

    /**
     * Produce a valid prim name by replacing invalid characters with "_".
     * To be able to call each stage in a thread, a stage_id is given.
     * @param[in] stage_id    Stage Id.
     * @param[in] name        The input name.
     * @return A string that is considered valid for use as a prim name.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_getValidPrimName(const long int stage_id, const char* name);

    /**
     * Produce a matching array of valid and unique prim names from an array of preferred names.
     * To be able to call each stage in a thread, a stage_id is given.
     * The returned array (and its strings) is owned by the stage's temporary buffer and remains valid until the next
     * call that reuses the same stage's temporary buffer. The caller must not free it.
     * @param[in]  stage_id            Stage Id.
     * @param[in]  names               Array of input names (array of UTF-8 encoded C strings).
     * @param[in]  namesCount          Number of entries in `names`.
     * @param[in]  reservedNames       Array of reserved names (array of C strings). May be null.
     * @param[in]  reservedNamesCount  Number of entries in `reservedNames`.
     * @param[out] returnCount         Number of entries in the returned array.
     * @return An array of valid and unique names, ordered to match `names`, or null on failure.
     */
    USD_EXPORTER_REVIT_API const char** usd_exporter_revit_core_getValidPrimNames(const long int stage_id, const char** names, int namesCount, const char** reservedNames, int reservedNamesCount, int* returnCount);
}

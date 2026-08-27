// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <filesystem>
#include <string>

namespace revit::usd_export::core::detail
{
// URI helpers used to decide whether a path can be written locally. Exposed here so they can be unit tested.

//! Returns true if the URI begins with a Windows drive specifier (e.g. "C:").
bool isWindowsDrivePath(const std::string& uri);

//! Returns the lowercased URI scheme (e.g. "file"), or an empty string for a plain local path.
std::string getScheme(const std::string& uri);

//! Converts a local URI (plain path or "file:" URI) to a filesystem path.
std::filesystem::path getLocalPath(const std::string& uri);

//! Returns true if the URI refers to a local file (no scheme, or the "file" scheme).
bool isLocalUri(const std::string& uri);

} // namespace revit::usd_export::core::detail

extern "C"
{
    /**
     * Determine if the local URI exists.
     *
     * @param uri
     * @return A bool indicating if the URI exists.
     */
    REVIT_USD_EXPORT_API bool revit_file_client_uri_exists(const std::string& uri);

    /**
     * Determine if the URI refers to a local file.
     * @param uri
     * @return A bool indicating if the URI refers to a local file.
     */
    REVIT_USD_EXPORT_API bool revit_file_client_is_local_uri(const char* uri);
}

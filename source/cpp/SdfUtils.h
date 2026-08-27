// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include <pxr/usd/sdf/layer.h>
#include <pxr/usd/sdf/path.h>

#include <string>

namespace revit::usd_export::core::detail
{

std::string computeAbsolutePath(const pxr::SdfLayerRefPtr& rootLayer, const std::string& path);

void resolvePathsInternal(const pxr::SdfLayerRefPtr& srcLayer, pxr::SdfLayerRefPtr dstLayer, bool storeRelativePath, bool relativeToSrcLayer = false, bool copyLayerOffsets = false);

void resolvePaths(const std::string& srcLayerIdentifier, const std::string& targetLayerIdentifier, bool storeRelativePath, bool relativeToSrcLayer = false, bool copySublayerLayerOffsets = false);

bool mergePrimSpecInternal(pxr::SdfLayerRefPtr dstLayer, const pxr::SdfLayerRefPtr& srcLayer, const pxr::SdfPath& primSpecPath, bool isDstStrongerThanSrc, const pxr::SdfPath& targetPrimPath);

bool mergePrimSpec(const std::string& dstLayerIdentifier, const std::string& srcLayerIdentifier, const std::string& primSpecPath, bool isDstStrongerThanSrc, const std::string& targetPrimPath = "");

//! Return the string representation of this path as a std::string.
//!
//! This function is recommended only for human-readable or diagnostic output. Use the SdfPath API to manipulate paths.
//! It is less error-prone and has better performance.
//!
//! This function exists as a back port of SdfPath::GetAsString() which is not available in OpenUsd versions 20.08 and earlier
//!
//! @param path The SdfPath to consider.
//! @returns A string representation of this path.
std::string getPathAsString(const pxr::SdfPath& path);

} // namespace revit::usd_export::core::detail

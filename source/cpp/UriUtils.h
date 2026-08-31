// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include <string>

namespace usd::exporter::revit::core::detail
{

// These utilities were removed from the AR 2.0 API, so provide them here
// Ultimately we require isSearchPath() because of MDL asset paths like "OmniPBR.mdl"
// See https://graphics.pixar.com/usd/release/wp_ar2.html#add-identifier-concept
bool isFileRelative(const std::string& path);

bool isAbsolute(const std::string& path);

bool isSearchPath(const std::string& path);

std::string makeRelativeUrl(const char* baseUrl, const char* otherUrl);

std::string normalizePath(const std::string& path);

} // namespace usd::exporter::revit::core::detail

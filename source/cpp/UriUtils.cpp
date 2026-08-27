// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "UriUtils.h"

#include <filesystem>
#include <string>
#include <system_error>

namespace revit::usd_export::core
{

bool hasScheme(const std::string& path)
{
    auto colon = path.find(':');
    return colon != std::string::npos && colon < path.find('/');
}

} // namespace revit::usd_export::core

bool revit::usd_export::core::detail::isFileRelative(const std::string& path)
{
    return path[0] == '.';
}

bool revit::usd_export::core::detail::isAbsolute(const std::string& path)
{
    return path[0] == '/' || hasScheme(path);
}

bool revit::usd_export::core::detail::isSearchPath(const std::string& path)
{
    return !revit::usd_export::core::detail::isAbsolute(path) && !revit::usd_export::core::detail::isFileRelative(path);
}

std::string revit::usd_export::core::detail::normalizePath(const std::string& path)
{
    static auto replaceAll = [](std::string str, const std::string& from, const std::string& to)
    {
        size_t start_pos = 0;
        while ((start_pos = str.find(from, start_pos)) != std::string::npos)
        {
            str.replace(start_pos, from.length(), to);
            start_pos += to.length(); // Handles case where 'to' is a substring of 'from'
        }
        return str;
    };

    std::string finalPath = path;
    finalPath = replaceAll(finalPath, "%3C", "<");
    finalPath = replaceAll(finalPath, "%3E", ">");
    finalPath = replaceAll(finalPath, "%20", " ");
    finalPath = replaceAll(finalPath, "%5C", "/");
    std::replace(finalPath.begin(), finalPath.end(), '\\', '/');

    return finalPath;
}

std::string revit::usd_export::core::detail::makeRelativeUrl(const char* baseUrl, const char* otherUrl)
{
    if (!baseUrl || !otherUrl)
    {
        return "";
    }

    std::filesystem::path basePath(baseUrl);
    std::filesystem::path otherPath(otherUrl);
    std::filesystem::path baseDirectory = basePath.parent_path();
    if (baseDirectory.empty())
    {
        baseDirectory = std::filesystem::current_path();
    }

    std::error_code ec;
    std::filesystem::path relativePath = std::filesystem::relative(otherPath, baseDirectory, ec);
    if (ec || relativePath.empty())
    {
        relativePath = otherPath.lexically_relative(baseDirectory);
    }
    if (relativePath.empty())
    {
        relativePath = otherPath;
    }

    return normalizePath(relativePath.string());
}

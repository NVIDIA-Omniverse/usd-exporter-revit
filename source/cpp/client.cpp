// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "client.h"

#include <algorithm>
#include <cctype>
#include <filesystem>
#include <system_error>

static const std::string g_fileScheme = "file";

namespace usd::exporter::revit::core::detail
{
bool isWindowsDrivePath(const std::string& uri)
{
    return uri.size() >= 2 && std::isalpha(static_cast<unsigned char>(uri[0])) && uri[1] == ':';
}

std::string getScheme(const std::string& uri)
{
    if (isWindowsDrivePath(uri))
    {
        return "";
    }

    const size_t colon = uri.find(':');
    const size_t slash = uri.find('/');
    const size_t backslash = uri.find('\\');
    const size_t firstSeparator = std::min(slash == std::string::npos ? uri.size() : slash, backslash == std::string::npos ? uri.size() : backslash);
    if (colon == std::string::npos || colon > firstSeparator)
    {
        return "";
    }

    std::string scheme = uri.substr(0, colon);
    std::transform(
        scheme.begin(),
        scheme.end(),
        scheme.begin(),
        [](unsigned char c)
        {
            return static_cast<char>(std::tolower(c));
        }
    );
    return scheme;
}

std::filesystem::path getLocalPath(const std::string& uri)
{
    std::string path = uri;
    std::string scheme = getScheme(path);
    if (scheme == g_fileScheme)
    {
        static const std::string filePrefix = "file:";
        path = path.substr(filePrefix.size());
        if (path.rfind("///", 0) == 0)
        {
            path = path.substr(3);
        }
        else if (path.rfind("//", 0) == 0)
        {
            path = path.substr(2);
        }
        if (path.size() >= 3 && path[0] == '/' && isWindowsDrivePath(path.substr(1)))
        {
            path = path.substr(1);
        }
    }

    return std::filesystem::path(path);
}

bool isLocalUri(const std::string& uri)
{
    const std::string scheme = getScheme(uri);
    return scheme.empty() || scheme == g_fileScheme;
}

} // namespace usd::exporter::revit::core::detail

extern "C"
{
    bool usd_exporter_revit_file_client_uri_exists(const std::string& uri)
    {
        namespace detail = usd::exporter::revit::core::detail;
        if (!detail::isLocalUri(uri))
        {
            return false;
        }

        std::error_code ec;
        return std::filesystem::exists(detail::getLocalPath(uri), ec) && !ec;
    }

    USD_EXPORTER_REVIT_API bool usd_exporter_revit_file_client_is_local_uri(const char* uri)
    {
        return uri && usd::exporter::revit::core::detail::isLocalUri(uri);
    }
}

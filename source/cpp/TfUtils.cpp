// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "TfUtils.h"

#include "Log.h"
#include "Transcoding.h"

#include <pxr/base/tf/envSetting.h>

PXR_NAMESPACE_USING_DIRECTIVE

// Set the `USD_EXPORTER_REVIT_ENABLE_TRANSCODING` environment variable to enable/disable reversible Bootstring transcoding within
// `makeValidIdentifier`. Defaults `true` (transcoding is enabled). When disabled, invalid characters are replaced with "_".
TF_DEFINE_ENV_SETTING(USD_EXPORTER_REVIT_ENABLE_TRANSCODING, true, "Use the Bootstring transcoding implementation when producing valid Prim names.");

namespace
{
// Alternate implementation of TfMakeValidIdentifier, used as a fallback when transcoding is disabled or fails.
// Deviates from TfMakeValidIdentifier by prefixing (rather than replacing) a leading numeric character with "_",
// which reduces avoidable name collisions.
std::string makeValidIdentifierExtended(const std::string& in)
{
    std::string result;

    if (in.empty())
    {
        result.push_back('_');
        return result;
    }

    char const* p = in.c_str();

    if (('0' <= *p && *p <= '9'))
    {
        result.reserve(in.size() + 1);
        result.push_back('_');
        result.push_back(*p);
    }
    else
    {
        result.reserve(in.size());
        if (!(('a' <= *p && *p <= 'z') || ('A' <= *p && *p <= 'Z') || *p == '_'))
        {
            result.push_back('_');
        }
        else
        {
            result.push_back(*p);
        }
    }

    for (++p; *p; ++p)
    {
        if (!(('a' <= *p && *p <= 'z') || ('A' <= *p && *p <= 'Z') || ('0' <= *p && *p <= '9') || *p == '_'))
        {
            result.push_back('_');
        }
        else
        {
            result.push_back(*p);
        }
    }

    return result;
}

} // namespace

std::string usd::exporter::revit::core::detail::makeValidIdentifier(const std::string& in)
{
    static bool s_enableTranscoding = TfGetEnvSetting(USD_EXPORTER_REVIT_ENABLE_TRANSCODING);
    if (s_enableTranscoding)
    {
        std::string out = encodeIdentifier(in, TranscodingFormat::ASCII);
        if (out.empty())
        {
            // Encoding can fail for invalid UTF-8; fall back to character substitution.
            USD_EXPORTER_REVIT_LOG_WARN(kUsdExporterRevitChannel, "Bootstring encoding of \"%s\" failed. Resorting to character substitution.", in.c_str());
            return makeValidIdentifierExtended(in);
        }
        return out;
    }
    return makeValidIdentifierExtended(in);
}

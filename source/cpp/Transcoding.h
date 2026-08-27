// SPDX-FileCopyrightText: Copyright (c) 2025 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include <string>

namespace revit::usd_export::core::detail
{

//! Encoding algorithm produces different output depending on format.
enum class TranscodingFormat
{
    //! The identifier is composed only of alphanumeric characters and underscore.
    ASCII,

    //! The identifier is composed of UTF-8 non-control characters.
    UTF8_XID
};

//! Encodes an identifier using the Bootstring algorithm.
//!
//! Ported from OpenUSD Exchange internal transcoding, which replaced the external `omni_transcoding` sidecar.
//! Produces the same `tn__` encoded names as `omni::transcoding::encodeBootstringIdentifier`.
//!
//! @param inputString The input string.
//! @param format The format to apply in transcoding.
std::string encodeIdentifier(const std::string& inputString, TranscodingFormat format);

//! Decodes an identifier using the Bootstring algorithm.
//!
//! @param inputString The input string.
std::string decodeIdentifier(const std::string& inputString);

} // namespace revit::usd_export::core::detail

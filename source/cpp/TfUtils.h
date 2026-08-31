// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include <string>

namespace usd::exporter::revit::core::detail
{

//! Produce a valid identifier from `in`.
//!
//! When transcoding is enabled (the default), invalid characters are losslessly encoded using the reversible Bootstring algorithm,
//! producing `tn__` prefixed names that round-trip the original value (matching the previous `omni_transcoding` sidecar output).
//! Transcoding can be disabled via the `USD_EXPORTER_REVIT_ENABLE_TRANSCODING` environment variable, in which case (and as a fallback
//! when encoding fails) invalid characters are replaced with "_".
//!
//! The character-substitution fallback differs from pxr::TfMakeValidIdentifier in how it handles numeric characters at the start of
//! the value. Rather than replacing the character with an "_" this function will add an "_" prefix.
//!
//! @param in The input value
//! @returns A string that is considered valid for use as an identifier.
std::string makeValidIdentifier(const std::string& in);

} // namespace usd::exporter::revit::core::detail

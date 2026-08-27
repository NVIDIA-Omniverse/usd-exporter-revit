// SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include <doctest/doctest.h>

#include "Transcoding.h"

namespace detail = revit::usd_export::core::detail;

TEST_CASE("encodeIdentifier round-trips ASCII identifiers")
{
    const std::string input = "Wall_01";
    const std::string encoded = detail::encodeIdentifier(input, detail::TranscodingFormat::ASCII);
    CHECK(detail::decodeIdentifier(encoded) == input);
}

TEST_CASE("encodeIdentifier round-trips identifiers with invalid characters")
{
    const std::string input = "Level 1/A";
    const std::string encoded = detail::encodeIdentifier(input, detail::TranscodingFormat::ASCII);
    CHECK_FALSE(encoded.empty());
    CHECK(detail::decodeIdentifier(encoded) == input);
}

TEST_CASE("encodeIdentifier round-trips leading numeric identifiers")
{
    const std::string input = "123Element";
    const std::string encoded = detail::encodeIdentifier(input, detail::TranscodingFormat::ASCII);
    CHECK_FALSE(encoded.empty());
    CHECK(detail::decodeIdentifier(encoded) == input);
}

TEST_CASE("decodeIdentifier returns input unchanged when not bootstring-encoded")
{
    const std::string input = "ValidPrim";
    CHECK(detail::decodeIdentifier(input) == input);
}

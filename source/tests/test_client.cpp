// SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include <doctest/doctest.h>

#include "client.h"

namespace detail = revit::usd_export::core::detail;

TEST_CASE("isLocalUri accepts plain local paths")
{
    CHECK(detail::isLocalUri("C:/export"));
    CHECK(detail::isLocalUri("C:\\export\\model.usd"));
    CHECK(detail::isLocalUri("D:/projects/scene.usda"));
    CHECK(detail::isLocalUri("relative/path/model.usd"));
    CHECK(detail::isLocalUri("model.usd"));
    CHECK(detail::isLocalUri(""));
}

TEST_CASE("isLocalUri accepts the file scheme in any casing")
{
    CHECK(detail::isLocalUri("file:///C:/export/model.usd"));
    CHECK(detail::isLocalUri("file://C:/export/model.usd"));
    CHECK(detail::isLocalUri("FILE:///C:/export/model.usd"));
}

TEST_CASE("isLocalUri rejects remote schemes")
{
    CHECK_FALSE(detail::isLocalUri("omniverse://server/Projects/model.usd"));
    CHECK_FALSE(detail::isLocalUri("omni://server/Projects/model.usd"));
    CHECK_FALSE(detail::isLocalUri("http://example.com/model.usd"));
    CHECK_FALSE(detail::isLocalUri("https://example.com/model.usd"));
    CHECK_FALSE(detail::isLocalUri("s3://bucket/model.usd"));
}

TEST_CASE("getScheme lowercases and ignores Windows drive letters")
{
    CHECK(detail::getScheme("C:/export") == "");
    CHECK(detail::getScheme("c:\\export") == "");
    CHECK(detail::getScheme("relative/path") == "");
    CHECK(detail::getScheme("Omniverse://server/x") == "omniverse");
    CHECK(detail::getScheme("HTTPS://example.com") == "https");
    CHECK(detail::getScheme("file:///C:/x") == "file");
}

TEST_CASE("getLocalPath strips the file scheme prefix")
{
    CHECK(detail::getLocalPath("file:///C:/export/model.usd") == std::filesystem::path("C:/export/model.usd"));
    CHECK(detail::getLocalPath("file://C:/export/model.usd") == std::filesystem::path("C:/export/model.usd"));
    CHECK(detail::getLocalPath("C:/export/model.usd") == std::filesystem::path("C:/export/model.usd"));
}

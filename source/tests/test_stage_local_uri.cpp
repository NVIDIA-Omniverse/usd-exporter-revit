// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include <doctest/doctest.h>

#include "ExportApi.h"

// Call the C ABI exported by revit_usd_export.dll. Avoid StageAlgo.h — it pulls
// OpenUSD headers and would require linking the full USD set into this exe.
extern "C"
{
    REVIT_USD_EXPORT_API long int revit_usd_export_core_createStage(
        const char* identifier, const char* defaultPrimName, char* upAxis, const double linearUnits);
    REVIT_USD_EXPORT_API double revit_usd_export_core_getMetersPerUnitFromFile(const char* filePath);
}

TEST_CASE("getMetersPerUnitFromFile rejects remote URIs without opening")
{
    CHECK(revit_usd_export_core_getMetersPerUnitFromFile("omniverse://server/Projects/model.usd") == -1.0);
    CHECK(revit_usd_export_core_getMetersPerUnitFromFile("omni://server/Projects/model.usd") == -1.0);
    CHECK(revit_usd_export_core_getMetersPerUnitFromFile("http://example.com/model.usd") == -1.0);
    CHECK(revit_usd_export_core_getMetersPerUnitFromFile(
              "https://omniverse-content-production.s3-us-west-2.amazonaws.com/Assets/x.usd") == -1.0);
    CHECK(revit_usd_export_core_getMetersPerUnitFromFile("s3://bucket/model.usd") == -1.0);
}

TEST_CASE("createStage rejects remote identifiers")
{
    char upAxis[] = "Z";
    const long int stageId = revit_usd_export_core_createStage(
        "omniverse://server/Projects/model.usd", "Root", upAxis, 0.01);
    CHECK(stageId == 0);
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include <doctest/doctest.h>

#include "ExportApi.h"

// Call the C ABI exported by usd_exporter_revit.dll. Avoid StageAlgo.h — it pulls
// OpenUSD headers and would require linking the full USD set into this exe.
extern "C"
{
    USD_EXPORTER_REVIT_API long int usd_exporter_revit_core_createStage(const char* identifier, const char* defaultPrimName, char* upAxis, const double linearUnits);
    USD_EXPORTER_REVIT_API double usd_exporter_revit_core_getMetersPerUnitFromFile(const char* filePath);
}

TEST_CASE("getMetersPerUnitFromFile rejects remote URIs without opening")
{
    CHECK(usd_exporter_revit_core_getMetersPerUnitFromFile("omniverse://server/Projects/model.usd") == -1.0);
    CHECK(usd_exporter_revit_core_getMetersPerUnitFromFile("omni://server/Projects/model.usd") == -1.0);
    CHECK(usd_exporter_revit_core_getMetersPerUnitFromFile("http://example.com/model.usd") == -1.0);
    CHECK(usd_exporter_revit_core_getMetersPerUnitFromFile("https://omniverse-content-production.s3-us-west-2.amazonaws.com/Assets/x.usd") == -1.0);
    CHECK(usd_exporter_revit_core_getMetersPerUnitFromFile("s3://bucket/model.usd") == -1.0);
}

TEST_CASE("createStage rejects remote identifiers")
{
    char upAxis[] = "Z";
    const long int stageId = usd_exporter_revit_core_createStage("omniverse://server/Projects/model.usd", "Root", upAxis, 0.01);
    CHECK(stageId == 0);
}

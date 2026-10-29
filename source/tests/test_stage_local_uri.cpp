// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include <doctest/doctest.h>

#include "ExportApi.h"

#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>

// Call the C ABI exported by usd_exporter_revit.dll. Avoid StageAlgo.h — it pulls
// OpenUSD headers and would require linking the full USD set into this exe.
extern "C"
{
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_startup();
    USD_EXPORTER_REVIT_API long int usd_exporter_revit_core_createStage(const char* identifier, const char* defaultPrimName, char* upAxis, const double linearUnits);
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createUsdzPackage(const char* assetPath, const char* usdzFilePath);
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

TEST_CASE("createUsdzPackage creates a readable package")
{
    REQUIRE(usd_exporter_revit_core_startup());
    const auto directory = std::filesystem::temp_directory_path() / "usd_exporter_revit_usdz_test";
    const auto sourcePath = directory / "model.usda";
    const auto packagePath = directory / "model.usdz";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(directory);

    std::ofstream(sourcePath) << "#usda 1.0\n(\n    defaultPrim = \"Root\"\n    metersPerUnit = 0.01\n)\ndef Xform \"Root\" {}\n";

    REQUIRE(usd_exporter_revit_core_createUsdzPackage(sourcePath.string().c_str(), packagePath.string().c_str()));
    CHECK(std::filesystem::is_regular_file(packagePath));
    CHECK(usd_exporter_revit_core_getMetersPerUnitFromFile(packagePath.string().c_str()) == doctest::Approx(0.01));

    std::filesystem::remove_all(directory);
}

TEST_CASE("createUsdzPackage does not modify the source layer's MDL data")
{
    // USDZ packaging strips MDL shaders (unsupported by the format) before zipping. The source
    // layer may be a user-owned asset referenced via a family mapping rather than a throwaway
    // export layer, so packaging must operate on a private copy and leave the source and any
    // externally referenced layers untouched.
    REQUIRE(usd_exporter_revit_core_startup());
    const auto directory = std::filesystem::temp_directory_path() / "usd_exporter_revit_usdz_mdl_test";
    const auto sourcePath = directory / "model.usda";
    const auto referencedPath = directory / "material.usda";
    const auto packagePath = directory / "model.usdz";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(directory);

    const std::string referencedContent =
        "#usda 1.0\n"
        "def Xform \"Material\"\n"
        "{\n"
        "    def Shader \"MDLShader\"\n"
        "    {\n"
        "        uniform asset info:mdl:sourceAsset = @./shader.mdl@\n"
        "        token outputs:mdl:surface\n"
        "    }\n"
        "    def Shader \"PreviewSurface\"\n"
        "    {\n"
        "        uniform token info:id = \"UsdPreviewSurface\"\n"
        "        token outputs:surface\n"
        "    }\n"
        "}\n";
    std::ofstream(referencedPath) << referencedContent;

    const std::string sourceContent =
        "#usda 1.0\n"
        "(\n"
        "    defaultPrim = \"Root\"\n"
        "    metersPerUnit = 0.01\n"
        ")\n"
        "def Xform \"Root\"\n"
        "{\n"
        "    def Xform \"Material\" (\n"
        "        references = @./material.usda@</Material>\n"
        "    )\n"
        "    {\n"
        "    }\n"
        "    def Shader \"MdlShader\"\n"
        "    {\n"
        "        uniform asset info:mdl:sourceAsset = @./shader.mdl@\n"
        "        token outputs:mdl:surface\n"
        "    }\n"
        "}\n";
    std::ofstream(sourcePath) << sourceContent;

    REQUIRE(usd_exporter_revit_core_createUsdzPackage(sourcePath.string().c_str(), packagePath.string().c_str()));
    CHECK(std::filesystem::is_regular_file(packagePath));

    std::string contentAfter;
    {
        std::ifstream sourceAfter(sourcePath);
        contentAfter.assign(std::istreambuf_iterator<char>(sourceAfter), std::istreambuf_iterator<char>());
    }
    CHECK(contentAfter == sourceContent);

    std::string referencedContentAfter;
    {
        std::ifstream referencedAfter(referencedPath);
        referencedContentAfter.assign(std::istreambuf_iterator<char>(referencedAfter), std::istreambuf_iterator<char>());
    }
    CHECK(referencedContentAfter == referencedContent);

    std::filesystem::remove_all(directory);
}

TEST_CASE("createUsdzPackage packages a texture dependency under a deep export path")
{
    // Regression test: staging used to mirror each dependency's *full* absolute path under a new
    // temp directory, doubling the already-deep per-export staging path (%TEMP%/usd-exporter-revit/
    // usdz_<guid>/...) and pushing real-world material paths (e.g. "Looks/Brick, Common/albedo.png")
    // past Windows' 260-character MAX_PATH, which failed with "the system cannot find the path
    // specified" when copying the texture.
    REQUIRE(usd_exporter_revit_core_startup());
    const auto directory = std::filesystem::temp_directory_path() / "usd-exporter-revit" / "usdz_00000000000000000000000000000000";
    const auto texDir = directory / "Looks" / "Brick, Common";
    const auto sourcePath = directory / "model.usda";
    const auto texturePath = texDir / "albedo.png";
    const auto packagePath = directory / "model.usdz";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(texDir);

    std::ofstream(texturePath) << "fake png bytes";

    std::ofstream(sourcePath) << "#usda 1.0\n"
                                 "(\n"
                                 "    defaultPrim = \"Root\"\n"
                                 "    metersPerUnit = 0.01\n"
                                 ")\n"
                                 "def Xform \"Root\"\n"
                                 "{\n"
                                 "    def Shader \"DiffuseTexture\"\n"
                                 "    {\n"
                                 "        uniform token info:id = \"UsdUVTexture\"\n"
                                 "        uniform asset inputs:file = @./Looks/Brick, Common/albedo.png@\n"
                                 "        float3 outputs:rgb\n"
                                 "    }\n"
                                 "}\n";

    REQUIRE(usd_exporter_revit_core_createUsdzPackage(sourcePath.string().c_str(), packagePath.string().c_str()));
    CHECK(std::filesystem::is_regular_file(packagePath));

    std::filesystem::remove_all(directory);
}

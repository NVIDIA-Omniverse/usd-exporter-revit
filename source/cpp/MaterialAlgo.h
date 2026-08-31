// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include "Types.h"

#include <optional>

#include <pxr/base/gf/vec2f.h>
#include <pxr/base/gf/vec3f.h>
#include <pxr/usd/usd/prim.h>
#include <pxr/usd/usdShade/material.h>
#include <pxr/usd/usdShade/shader.h>

namespace usd::exporter::revit::core
{
//! Translate an sRGB color value to linear color space
//!
//! - Many 3D modeling applications define colors in RGB (0-255) or sRGB (0-1) color space
//! - MDL uses a linear color space that aligns with how light and color behave in the natural world
//! - Color is a complex topic in 3D rendering and providing utilities covering the full breadth of color science is out of the scope of this module
//!
//! @param color sRGB representation of a color to be translated to linear color space
//! @returns The translated color in linear color space
USD_EXPORTER_REVIT_API pxr::GfVec3f sRgbToLinear(const pxr::GfVec3f& color);

//! Translate a linear color value to sRGB color space
//!
//! - Many 3D modeling applications define colors in RGB (0-255) or sRGB (0-1) color space
//! - MDL uses a linear color space that aligns with how light and color behave in the natural world
//! - Color is a complex topic in 3D rendering and providing utilities covering the full breadth of color science is out of the scope of this module
//!
//! @param color linear representation of a color to be translated to sRGB color space
//! @returns The color in sRGB color space
USD_EXPORTER_REVIT_API pxr::GfVec3f linearToSrgb(const pxr::GfVec3f& color);

//! Get the effective surface Shader of a Material for the MDL render context.
//!
//! @param material The Material to consider
//! @returns The connected Shader. Returns an invalid object on error.
USD_EXPORTER_REVIT_API pxr::UsdShadeShader computeEffectiveMdlSurfaceShader(const pxr::UsdShadeMaterial& material);

//! Create a UsdShadeMaterial as a child of the UsdPrim argument
//!
//! @param parent Parent UsdPrim for the material to be created
//! @param name Name of the material to be created
//! @returns The newly created UsdShadeMaterial.  Returns an Invalid prim on error.
USD_EXPORTER_REVIT_API pxr::UsdShadeMaterial createMaterial(pxr::UsdPrim parent, const std::string& name);

//! Create a UsdShadeShader as a child of the UsdShadeMaterial argument with the specified MDL
//!
//! @param material Parent UsdShadeMaterial for the shader to be created
//! @param name Name of the shader to be created
//! @param mdlPath Absolute or relative path to the MDL asset
//! @param module Name of the MDL module to set as source asset sub-identifier for the shader
//! @param connectMaterialOutputs If true, creates surface, displacement and volume outputs on the material and connects them to the shader output
//! @returns The newly created UsdShadeShader.  Returns an Invalid prim on error.
USD_EXPORTER_REVIT_API pxr::UsdShadeShader createMdlShader(pxr::UsdShadeMaterial& material, const std::string& name, const pxr::SdfAssetPath& mdlPath, const pxr::TfToken& module, bool connectMaterialOutputs = true);

//! Create an MDL shader input
//!
//! If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
//!
//! If the shader input already exists and has a connected source -> the source will be disconnected before being set.
//!
//! @note When creating texture asset inputs (diffuse, normal, roughness, etc.) it is important to set the colorSpace parameter so that
//!       the textures are sampled correctly.  Typically, diffuse is "auto", which resolves to "sRGB".  Normal, roughness, and other textures
//!       should be "raw".
//!
//! @param material The UsdShadeMaterial prim that contains the MDL shader
//! @param name Name of the input to be created
//! @param value The value assigned to the input
//! @param typeName The value type of the input
//! @param colorSpace If set, the newly created input's colorSpace attribute
//! @returns The newly created UsdShadeInput input.  Returns an Invalid UsdShadeInput on error.
USD_EXPORTER_REVIT_API pxr::UsdShadeInput createMdlShaderInput(
    pxr::UsdShadeMaterial& material,
    const pxr::TfToken& name,
    const pxr::VtValue& value,
    const pxr::SdfValueTypeName& typeName,
    std::optional<usd::exporter::revit::core::ColorSpace> colorSpace = std::nullopt
);

//! Binds a UsdShadeMaterial to a UsdPrim
//!
//! @param prim UsdPrim to bind the material to
//! @param material UsdShadeMaterial to bind to the prim
USD_EXPORTER_REVIT_API void bindMaterial(pxr::UsdPrim prim, const pxr::UsdShadeMaterial& material);

//! Defines a UsdShadeMaterial and connected UsdShadeShaders using OmniPBR.mdl and UsdPreviewSurface with the specified input attributes
//!
//! MDL and UsdPreviewSurface use a linear color space, please convert RGB and sRGB values to linear
//!
//! @param stage The stage on which to define the Material
//! @param path The absolute prim path at which to define the Material
//! @param color The diffuse color of the Material
//! @param opacity The Opacity Amount to set. When less than 1.0, Enable Opacity is set to true and Fractional Opacity is enabled in the RT renderer
//! @param roughness The Roughness Amount to set, 0.0-1.0 range where 1.0 = flat and 0.0 = glossy
//! @param metallic The Metallic Amount to set, 0.0-1.0 range where 1.0 = max metallic and 0.0 = no metallic
//! @returns The newly defined UsdShadeMaterial. Returns an Invalid prim on error
USD_EXPORTER_REVIT_API pxr::UsdShadeMaterial defineOmniPbrMaterial(pxr::UsdStagePtr stage, const pxr::SdfPath& path, const pxr::GfVec3f& color, const float opacity = 1.0f, const float roughness = 0.5f, const float metallic = 0.0f);

//! Adds an emissive color to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! The color and intensity are authored on the OmniPBR MDL shader (`emissive_color`, `enable_emission`, `emissive_intensity`) to preserve the
//! existing RTX appearance, and the color is also wired to the UsdPreviewSurface `emissiveColor` input so non-RTX/MDL viewers show self-illumination.
//!
//! @note The color is expected to already be in linear color space, matching the other OmniPBR color inputs.
//!
//! @param material The UsdShadeMaterial prim to add the emissive color
//! @param color The emissive color (linear color space)
//! @param intensity The intensity of the emissive color
//! @returns Whether or not the emissive color was added to the material
USD_EXPORTER_REVIT_API bool addEmissiveColorToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::GfVec3f& color, const float intensity = 1000.0f);

//! Adds a diffuse texture to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! @note The material prim's "Color" input will be removed and replaced with "DiffuseTexture".
//!       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @returns Whether or not the texture was added to the material
USD_EXPORTER_REVIT_API bool addDiffuseTextureToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::SdfAssetPath& texturePath);

//! Get the effective surface Shader of a Material for the universal render context.
//!
//! @param material The Material to consider
//! @returns The connected Shader. Returns an invalid object on error.
USD_EXPORTER_REVIT_API pxr::UsdShadeShader computeEffectivePreviewSurfaceShader(const pxr::UsdShadeMaterial& material);

//! Adds a normal texture to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @returns Whether or not the texture was added to the material
USD_EXPORTER_REVIT_API bool addNormalTextureToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::SdfAssetPath& texturePath);

//! Adds an ORM (occlusion, roughness, metallic) texture to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! @note The material prim's "Roughness" and "Metallic" inputs will be removed and replaced with "ORMTexture".
//!       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @returns Whether or not the texture was added to the material
USD_EXPORTER_REVIT_API bool addOrmTextureToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::SdfAssetPath& texturePath);

//! Adds a roughness texture to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! @note The material prim's "Roughness" input will be removed and replaced with "RoughnessTexture".
//!       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @returns Whether or not the texture was added to the material
USD_EXPORTER_REVIT_API bool addRoughnessTextureToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::SdfAssetPath& texturePath);

//! Adds a metallic texture to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! @note The material prim's "Metallic" input will be removed and replaced with "MetallicTexture".
//!       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @returns Whether or not the texture was added to the material
USD_EXPORTER_REVIT_API bool addMetallicTextureToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::SdfAssetPath& texturePath);

//! Adds an opacity texture to the PBR material
//!
//! It is expected that the material was created by the defineOmniPbrMaterial() function.
//!
//! @note The material prim's "Opacity" input will be removed and replaced with "OpacityTexture".
//!       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
//!
//! These shader parameters will be set to produce better masked geometry:
//! - MDL OmniPBR: `opacity_threshold = float_epsilon` (just greater than zero)
//! - UsdPreviewSurface: `ior = 1.0`
//! - UsdPreviewSurface: `opacityThreshold = float_epsilon` (just greater than zero)
//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @returns Whether or not the texture was added to the material
USD_EXPORTER_REVIT_API bool addOpacityTextureToPbrMaterial(pxr::UsdShadeMaterial& material, const pxr::SdfAssetPath& texturePath);

//! Defines a UsdShadeMaterial and connected UsdShadeShaders using OmniGlass.mdl and UsdPreviewSurface with the specified input attributes
//!
//! MDL and UsdPreviewSurface use a linear color space, please convert RGB and sRGB values to linear
//!
//! @param stage The stage on which to define the Material
//! @param path The absolute prim path at which to define the Material
//! @param color The color of the Material
//! @param indexOfRefraction The Index of Refraction to set, 1.0-4.0 range
//! @param roughness The roughness of the frosted glass surface, 0.0-1.0 range where 1.0 = frosted and 0.0 = clear
//! @returns The newly defined UsdShadeMaterial. Returns an Invalid prim on error
USD_EXPORTER_REVIT_API pxr::UsdShadeMaterial defineOmniGlassMaterial(pxr::UsdStagePtr stage, const pxr::SdfPath& path, const pxr::GfVec3f& color, const float indexOfRefraction = 1.491f, const float roughness = 0.02f);
} // namespace usd::exporter::revit::core

extern "C"
{
    /**
     * Translate an sRGB color value to linear color space.
     * TODO : If called from multiple threads, it is not thread-safe.
     * @param[in] color     sRGB representation of a color to be translated to linear color space.
     * @return The translated color in linear color space.
     */
    USD_EXPORTER_REVIT_API float* usd_exporter_revit_core_sRgbToLinear(const pxr::GfVec3f color);

    /**
     * Translate a linear color value to sRGB color space.
     * TODO : If called from multiple threads, it is not thread-safe.
     * @param[in] color     linear representation of a color to be translated to sRGB color space.
     * @return The color in sRGB color space.
     */
    USD_EXPORTER_REVIT_API float* usd_exporter_revit_core_linearToSrgb(const pxr::GfVec3f color);

    /**
     * Create a UsdShadeMaterial as a child of the UsdPrim argument.
     * @param[in] stage_id      Stage Id.
     * @param[in] parent        Parent UsdPrim for the material to be created.
     * @param[in] name          Name of the material to be created.
     * return If successful, the material's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_createMaterial(const long int stage_id, const char* parent, const char* name);

    /**
     * Create a UsdShadeShader as a child of the UsdShadeMaterial argument with the specified MDL.
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path at material.
     * @param[in] name          Name of the shader to be created.
     * @param[in] mdlPath       Absolute or relative path to the MDL asset.
     * @param[in] module        Name of the MDL module to set as source asset sub-identifier for the shader.
     * @param[in] connectMaterialOutputs   If true, creates surface, displacement and volume outputs on the material and connects them to the shader output.
     * return If successful, the shader's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_createMdlShader(const long int stage_id, const char* prim_path, const char* name, const char* mdlPath, const char* module, bool connectMaterialOutputs);

    /**
     * Create an MDL shader input
     *
     * If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
     *
     * If the shader input already exists and has a connected source -> the source will be disconnected before being set.
     *
     * @note When creating texture asset inputs (diffuse, normal, roughness, etc.) it is important to set the colorSpace parameter so that
     *       the textures are sampled correctly.  Typically, diffuse is "auto", which resolves to "sRGB".  Normal, roughness, and other textures
     *       should be "raw".
     *
     * @param[in] stage_id            Stage Id.
     * @param material_path           The UsdShadeMaterial prim that contains the MDL shader
     * @param input_name              Name of the input to be created
     * @param value                   The value assigned to the input
     * @param color_space             Color space for the asset.
     * @returns If the input was created and set successfully.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createMdlShaderInputAsset(const long int stage_id, const char* material_path, const char* input_name, const char* value, usd::exporter::revit::core::ColorSpace color_space);

    /**
     * Create an MDL shader input
     *
     * If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
     *
     * If the shader input already exists and has a connected source -> the source will be disconnected before being set.
     *
     * @param[in] stage_id            Stage Id.
     * @param material_path           The UsdShadeMaterial prim that contains the MDL shader
     * @param input_name              Name of the input to be created
     * @param value                   The value assigned to the input
     * @returns If the input was created and set successfully.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createMdlShaderInputBool(const long int stage_id, const char* material_path, const char* input_name, bool value);

    /**
     * Create an MDL shader input
     *
     * If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
     *
     * If the shader input already exists and has a connected source -> the source will be disconnected before being set.
     *
     * @param[in] stage_id            Stage Id.
     * @param material_path           The UsdShadeMaterial prim that contains the MDL shader
     * @param input_name              Name of the input to be created
     * @param value                   The value assigned to the input
     * @returns If the input was created and set successfully.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createMdlShaderInputInt(const long int stage_id, const char* material_path, const char* input_name, const int value);

    /**
     * Create an MDL shader input
     *
     * If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
     *
     * If the shader input already exists and has a connected source -> the source will be disconnected before being set.
     *
     * @param[in] stage_id            Stage Id.
     * @param material_path           The UsdShadeMaterial prim that contains the MDL shader
     * @param input_name              Name of the input to be created
     * @param value                   The value assigned to the input
     * @returns If the input was created and set successfully.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createMdlShaderInputFloat(const long int stage_id, const char* material_path, const char* input_name, const float value);

    /**
     * Create an MDL shader input
     *
     * If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
     *
     * If the shader input already exists and has a connected source -> the source will be disconnected before being set.
     *
     * @param[in] stage_id            Stage Id.
     * @param material_path           The UsdShadeMaterial prim that contains the MDL shader
     * @param input_name              Name of the input to be created
     * @param value                   The value assigned to the input
     * @returns If the input was created and set successfully.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createMdlShaderInputFloat2(const long int stage_id, const char* material_path, const char* input_name, const pxr::GfVec2f value);

    /**
     * Create an MDL shader input
     *
     * If the shader input already exists and is a different type, defined in the current edit target layer -> it will be removed and recreated.
     *
     * If the shader input already exists and has a connected source -> the source will be disconnected before being set.
     *
     * @param[in] stage_id            Stage Id.
     * @param material_path           The UsdShadeMaterial prim that contains the MDL shader
     * @param input_name              Name of the input to be created
     * @param value                   The value assigned to the input
     * @returns If the input was created and set successfully.
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_createMdlShaderInputColor3f(const long int stage_id, const char* material_path, const char* input_name, const pxr::GfVec3f value);

    /**
     * Binds a UsdShadeMaterial to a UsdPrim.
     * @param[in] stage_id            Stage Id.
     * @param[in] prim_path           Usd prim path to bind the material to.
     * @param[in] material_prim_path  UsdShadeMaterial to bind to the prim.
     */
    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_bindMaterial(const long int stage_id, const char* prim_path, const char* material_prim_path);

    /**
     * Defines a UsdShadeMaterial and connected UsdShadeShaders using OmniPBR.mdl and UsdPreviewSurface with the specified input attributes.
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path at which to define the material.
     * @param color     The diffuse color of the Material
     * @param opacity   The Opacity Amount to set. When less than 1.0, Enable Opacity is set to true and Fractional Opacity is enabled in the RT renderer
     * @param roughness The Roughness Amount to set, 0.0-1.0 range where 1.0 = flat and 0.0 = glossy
     * @param metallic  The Metallic Amount to set, 0.0-1.0 range where 1.0 = max metallic and 0.0 = no metallic
     * return If successful, the material's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_defineOmniPbrMaterial(const long int stage_id, const char* prim_path, const pxr::GfVec3f color, const float opacity, const float roughness, const float metallic);

    /**
     * Adds an emissive color to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * The color and intensity are authored on the OmniPBR MDL shader (emissive_color, enable_emission, emissive_intensity) to preserve the
     * existing RTX appearance, and the color is also wired to the UsdPreviewSurface emissiveColor input so non-RTX/MDL viewers show self-illumination.
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the emissive color
     * @param color             The emissive color (linear color space)
     * @param intensity         The intensity of the emissive color
     * @returns Whether or not the emissive color was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addEmissiveColorToPbrMaterial(const long int stage_id, const char* material_path, const pxr::GfVec3f color, const float intensity);

    /**
     * Adds a diffuse texture to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * @note The material prim's "Color" input will be removed and replaced with "DiffuseTexture".
     *       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the texture
     * @param texture_path      The SdfAssetPath to the texture file
     * @returns Whether or not the texture was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addDiffuseTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path);

    /**
     * Adds a normal texture to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the texture
     * @param texture_path      The SdfAssetPath to the texture file
     * @returns Whether or not the texture was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addNormalTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path);

    /**
     * Adds an ORM (occlusion, roughness, metallic) texture to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * @note The material prim's "Roughness" and "Metallic" inputs will be removed and replaced with "ORMTexture".
     *       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the texture
     * @param texture_path      The SdfAssetPath to the texture file
     * @returns Whether or not the texture was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addOrmTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path);

    /**
     * Adds a roughness texture to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * @note The material prim's "Roughness" input will be removed and replaced with "RoughnessTexture".
     *       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the texture
     * @param texture_path      The SdfAssetPath to the texture file
     * @returns Whether or not the texture was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addRoughnessTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path);

    /**
     * Adds a metallic texture to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * @note The material prim's "Metallic" input will be removed and replaced with "MetallicTexture".
     *       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the texture
     * @param texture_path      The SdfAssetPath to the texture file
     * @returns Whether or not the texture was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addMetallicTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path);

    /**
     * Adds an opacity texture to the PBR material
     *
     * It is expected that the material was created by the defineOmniPbrMaterial() function.
     *
     * @note The material prim's "Opacity" input will be removed and replaced with "OpacityTexture".
     *       Due to the input removal this function should be used at initial authoring time rather than in a stronger layer.
     *
     * These shader parameters will be set to produce better masked geometry:
     * - MDL OmniPBR: `opacity_threshold = float_epsilon` (just greater than zero)
     * - UsdPreviewSurface: `ior = 1.0`
     * - UsdPreviewSurface: `opacityThreshold = float_epsilon` (just greater than zero)
     *
     * @param stage_id          Stage Id.
     * @param material_path     The UsdShadeMaterial prim to add the texture
     * @param texture_path      The SdfAssetPath to the texture file
     * @returns Whether or not the texture was added to the material
     */
    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_addOpacityTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path);

    /**
     * Defines a UsdShadeMaterial and connected UsdShadeShaders using OmniGlass.mdl and UsdPreviewSurface with the specified input attributes.
     * @param[in] stage_id      Stage Id.
     * @param[in] prim_path     The absolute prim path at which to define the material.
     * @param[in] color             The diffuse color of the Material
     * @param[in] indexOfRefraction The Index of Refraction to set, 1.0-4.0 range
     * @param[in] roughness         The roughness of the frosted glass surface, 0.0-1.0 range where 1.0 = frosted and 0.0 = clear
     * return If successful, the material's Prim path is returned.
     */
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_defineOmniGlassMaterial(const long int stage_id, const char* prim_path, const pxr::GfVec3f color, const float indexOfRefraction, const float roughness);
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "MaterialAlgo.h"

#include "Log.h"
#include "SdfUtils.h"
#include "StageCache.h"
#include "UsdUtils.h"

#include <pxr/usd/ar/resolver.h>
#include <pxr/usd/usdShade/materialBindingAPI.h>
#include <pxr/usd/usdShade/tokens.h>
#include <pxr/usd/usdUtils/pipeline.h>

#include <algorithm>
#include <vector>

using namespace pxr;

namespace revit::usd_export::core
{
static constexpr const char* g_omniPbrAssetPath("OmniPBR.mdl");

// clang-format off
    TF_DEFINE_PRIVATE_TOKENS(
        _tokens,
        ((defaultValue, "default"))
        ((rangeMin, "range:min"))
        ((rangeMax, "range:max"))
        ((softRangeMin, "soft_range:min"))
        ((softRangeMax, "soft_range:max"))
        ((mdl, "mdl"))
        ((out, "out"))
        ((colorSpaceAuto, "auto"))
        ((colorSpaceRaw, "raw"))
        ((colorSpacesRBG, "sRGB"))
        ((omniPbr, "OmniPBR"))
        ((omniPbrAlbedoColor, "diffuse_color_constant"))
        ((omniPbrRoughness, "reflection_roughness_constant"))
        ((omniPbrRoughnessTexture, "reflectionroughness_texture"))
        ((omniPbrRoughnessTextureInfluence, "reflection_roughness_texture_influence"))
        ((omniPbrMetallic, "metallic_constant"))
        ((omniPbrMetallicTexture, "metallic_texture"))
        ((omniPbrMetallicTextureInfluence, "metallic_texture_influence"))
        ((omniPbrOrmTextureEnabled, "enable_ORM_texture"))
        ((omniPbrOpacity, "opacity_constant"))
        ((omniPbrOpacityEnabled, "enable_opacity"))
        ((omniPbrOpacityTexture, "opacity_texture"))
        ((omniPbrOpacityTextureEnabled, "enable_opacity_texture"))
        ((omniPbrOpacityThreshold, "opacity_threshold"))
        ((omniPbrDiffuseTexture, "diffuse_texture"))
        ((omniPbrNormalTexture, "normalmap_texture"))
        ((omniPbrOrmTexture, "ORM_texture"))
        ((omniPbrEmissiveColor, "emissive_color"))
        ((omniPbrEmissiveEnable, "enable_emission"))
        ((omniPbrEmissiveIntensity, "emissive_intensity"))
        ((omniGlass, "OmniGlass"))
        ((omniGlassColor, "glass_color"))
        ((omniGlassIor, "glass_ior"))
        ((omniGlassFrostingRoughness, "frosting_roughness"))
        ((usdPreviewSurface, "UsdPreviewSurface"))
        ((usdPreviewSurfaceUvTexture, "UsdUVTexture"))
        ((usdPreviewSurfacePrimvarReader, "UsdPrimvarReader_float2"))
        ((usdPreviewSurfaceBias, "bias"))
        ((usdPreviewSurfaceColor, "diffuseColor"))
        ((usdPreviewSurfaceEmissiveColor, "emissiveColor"))
        ((usdPreviewSurfaceSourceColorSpace, "sourceColorSpace"))
        ((usdPreviewSurfaceFallback, "fallback"))
        ((usdPreviewSurfaceFile, "file"))
        ((usdPreviewSurfaceIor, "ior"))
        ((usdPreviewSurfaceMetallic, "metallic"))
        ((usdPreviewSurfaceNormal, "normal"))
        ((usdPreviewSurfaceOcclusion, "occlusion"))
        ((usdPreviewSurfaceOpacity, "opacity"))
        ((usdPreviewSurfaceOpacityThreshold, "opacityThreshold"))
        ((usdPreviewSurfaceResult, "result"))
        ((usdPreviewSurfaceRoughness, "roughness"))
        ((usdPreviewSurfaceScale, "scale"))
        ((usdPreviewSurfaceVarname, "varname"))
        ((usdPreviewSurfaceRedChannel, "r"))
        ((usdPreviewSurfaceGreenChannel, "g"))
        ((usdPreviewSurfaceBlueChannel, "b"))
        ((usdPreviewSurfaceAlphaChannel, "a"))
        ((usdPreviewSurfaceRgb, "rgb"))
        ((usdPreviewSurfaceWrapS, "wrapS"))
        ((usdPreviewSurfaceWrapT, "wrapT"))
        ((usdPreviewSurfaceRepeat, "repeat"))
        ((usdPreviewSurfaceDiffuseColorTex, "DiffuseColorTex"))
        ((usdPreviewSurfaceNormalTex, "NormalTex"))
        ((usdPreviewSurfaceOpacityTex, "OpacityTex"))
        ((usdPreviewSurfaceOrmTex, "ORMTex"))
        ((usdPreviewSurfaceRoughnessTex, "RoughnessTex"))
        ((usdPreviewSurfaceMetallicTex, "MetallicTex"))
        ((usdPreviewSurfacePrimST, "PrimST"))
        ((materialColor, "Color"))
        ((materialColorInputs, "inputs:Color"))
        ((materialOpacity, "Opacity"))
        ((materialOpacityInputs, "inputs:Opacity"))
        ((materialRoughness, "Roughness"))
        ((materialRoughnessInputs, "inputs:Roughness"))
        ((materialMetallic, "Metallic"))
        ((materialMetallicInputs, "inputs:Metallic"))
        ((materialIor, "IOR"))
        ((materialDiffuseTexture, "DiffuseTexture"))
        ((materialNormalTexture, "NormalTexture"))
        ((materialOpacityTexture, "OpacityTexture"))
        ((materialOrmTexture, "ORMTexture"))
        ((materialRoughnessTexture, "RoughnessTexture"))
        ((materialMetallicTexture, "MetallicTexture"))
    );
// clang-format on

//! A utility struct to pass shader input names and values to a function
struct TfTokenValuePair
{
    TfToken inputName;
    VtValue value;
    // Prefer to get this from the VtValue itself but not sure where to get that
    SdfValueTypeName valueTypeName;
};


// reference for the conversion math https://ww2.mathworks.cn/help/images/understanding-color-spaces-and-color-space-conversion.html
// process is known as 'gamma correction' and can be unique per implementation
// MDL's linear color space uses most of the same constants, the only exception is the lower threshold of 0.04045 for MDL
float toLinear(float value)
{
    if (value <= 0.04045)
    {
        return value / 12.92;
    }
    else
    {
        float adjusted = (value + 0.055) / 1.055;
        return std::pow(adjusted, 2.4f);
    }
}

float fromLinear(float value)
{
    float test = value * 12.92;
    if (test <= 0.04045)
    {
        return test;
    }
    else
    {
        float scaled = std::pow(value, 1.0f / 2.4f);
        return (scaled * 1.055) - 0.055;
    }
}

GfVec3f sRgbToLinear(const GfVec3f& color)
{
    return GfVec3f(toLinear(color[0]), toLinear(color[1]), toLinear(color[2]));
}

GfVec3f linearToSrgb(const GfVec3f& color)
{
    return GfVec3f(fromLinear(color[0]), fromLinear(color[1]), fromLinear(color[2]));
}

// Remove a property from a prim within the current edit target
// This is used for removing input properties from shaders and materials
bool removeProperty(UsdStageRefPtr stage, const SdfPath& primPath, const TfToken& propName)
{
    SdfLayerHandle layer = stage->GetEditTarget().GetLayer();
    if (layer)
    {
        SdfPrimSpecHandle primSpec = layer->GetPrimAtPath(primPath);
        if (primSpec)
        {
            SdfPropertySpecHandle propSpec = layer->GetPropertyAtPath(primPath.AppendProperty(propName));
            if (propSpec)
            {
                primSpec->RemoveProperty(propSpec);
                return true;
            }
        }

        REVIT_LOG_WARN("Cannot remove property <%s> from prim <%s>, it doesn't exist in the current edit target layer <%s>", propName.GetText(), revit::usd_export::core::detail::getPathAsString(primPath).c_str(), layer->GetIdentifier().c_str());
        return false;
    }
    else
    {
        REVIT_LOG_WARN("Failed to get the current edit target layer from stage <%s> while removing property <%s>", stage->GetRootLayer()->GetRealPath().c_str(), propName.GetText());
        return false;
    }
}

TfToken colorSpaceEnumToToken(const revit::usd_export::core::ColorSpace& colorSpace)
{
    switch (colorSpace)
    {
        case revit::usd_export::core::ColorSpace::ColorSpace_eAuto:
        {
            return _tokens->colorSpaceAuto;
        }
        case revit::usd_export::core::ColorSpace::ColorSpace_eRaw:
        {
            return _tokens->colorSpaceRaw;
        }
        case revit::usd_export::core::ColorSpace::ColorSpace_eSrgb:
        {
            return _tokens->colorSpacesRBG;
        }
        default:
        {
            return TfToken();
        }
    }
}

UsdShadeMaterial createMaterial(UsdPrim parent, const std::string& name)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(parent, name, &reason))
    {
        REVIT_LOG_WARN("Unable to create UsdShadeMaterial due to an invalid location: %s", reason.c_str());
        return UsdShadeMaterial();
    }

    SdfPath materialPath = parent.GetPath().AppendChild(TfToken(name));
    UsdStagePtr stage = parent.GetStage();

    UsdShadeMaterial material = UsdShadeMaterial::Define(stage, materialPath);
    return material;
}

UsdShadeShader computeEffectiveMdlSurfaceShader(const UsdShadeMaterial& material)
{
    if (!material)
    {
        return UsdShadeShader();
    }

    return material.ComputeSurfaceSource({ _tokens->mdl });
}

UsdShadeShader createMdlShader(UsdShadeMaterial& material, const std::string& name, const SdfAssetPath& mdlPath, const TfToken& module, bool connectMaterialOutputs)
{
    UsdPrim materialPrim = material.GetPrim();

    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(materialPrim, name, &reason))
    {
        REVIT_LOG_WARN("Unable to create UsdShadeShader due to an invalid location: %s", reason.c_str());
        return UsdShadeShader();
    }

    SdfPath shaderPath = materialPrim.GetPath().AppendChild(TfToken(name));
    UsdStagePtr stage = materialPrim.GetStage();

    UsdShadeShader shader = UsdShadeShader::Define(stage, shaderPath);
    shader.SetSourceAsset(mdlPath, _tokens->mdl);
    shader.SetSourceAssetSubIdentifier(module, _tokens->mdl);
    if (connectMaterialOutputs)
    {
        UsdShadeOutput shaderOutput = shader.CreateOutput(_tokens->out, SdfValueTypeNames->Token);
        material.CreateSurfaceOutput(_tokens->mdl).ConnectToSource(shaderOutput);
        material.CreateVolumeOutput(_tokens->mdl).ConnectToSource(shaderOutput);
        material.CreateDisplacementOutput(_tokens->mdl).ConnectToSource(shaderOutput);
    }
    return shader;
}

UsdShadeInput createMdlShaderInput(UsdShadeMaterial& material, const TfToken& name, const VtValue& value, const SdfValueTypeName& typeName, std::optional<revit::usd_export::core::ColorSpace> colorSpace)
{
    if (!material)
    {
        REVIT_LOG_WARN("Invalid UsdShadeMaterial, cannot create MDL shader input <%s>", name.GetText());
        return UsdShadeInput();
    }

    UsdShadeShader shaderPrim = computeEffectiveMdlSurfaceShader(material);
    if (!shaderPrim)
    {
        REVIT_LOG_WARN("Cannot create MDL shader input, no MDL shader found in UsdShadeMaterial <%s>", revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());
        return UsdShadeInput();
    }

    UsdShadeInput existingInput = shaderPrim.GetInput(name);
    if (existingInput && existingInput.GetTypeName() != typeName)
    {
        if (!removeProperty(shaderPrim.GetPrim().GetStage(), shaderPrim.GetPrim().GetPath(), existingInput.GetFullName()))
        {
            REVIT_LOG_ERROR(
                "Unable to create UsdShadeInput <%s> in material <%s> because input already exists as type <%s> in another layer",
                name.GetText(),
                revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str(),
                existingInput.GetTypeName().GetAsToken().GetText()
            );
            return UsdShadeInput();
        }
    }
    else if (existingInput && existingInput.HasConnectedSource())
    {
        if (!existingInput.DisconnectSource())
        {
            REVIT_LOG_WARN("Failure disconnecting the existing source in UsdShadeInput <%s> in material <%s>", name.GetText(), revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());
        }
    }

    UsdShadeInput surfaceInput = shaderPrim.CreateInput(name, typeName);
    if (!surfaceInput)
    {
        REVIT_LOG_ERROR("Unable to create UsdShadeInput <%s> in material <%s>", name.GetText(), revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());
        return UsdShadeInput();
    }

    surfaceInput.Set(value);
    const UsdAttribute& attr = surfaceInput.GetAttr();
    if (colorSpace.has_value())
    {
        attr.SetColorSpace(colorSpaceEnumToToken(colorSpace.value()));
    }
    return surfaceInput;
}

void bindMaterial(UsdPrim prim, const UsdShadeMaterial& material)
{
    UsdPrim matPrim = material.GetPrim();
    if (!matPrim && !prim)
    {
        REVIT_LOG_WARN("UsdPrim <%s> and UsdShadeMaterial <%s> are not valid, cannot bind material to prim", revit::usd_export::core::detail::getPathAsString(prim.GetPath()).c_str(), revit::usd_export::core::detail::getPathAsString(matPrim.GetPath()).c_str());
        return;
    }
    if (!matPrim)
    {
        REVIT_LOG_WARN("UsdShadeMaterial <%s> is not valid, cannot bind material to prim", revit::usd_export::core::detail::getPathAsString(matPrim.GetPath()).c_str());
        return;
    }
    if (!prim)
    {
        REVIT_LOG_WARN("UsdPrim <%s> is not valid, cannot bind material to prim", revit::usd_export::core::detail::getPathAsString(prim.GetPath()).c_str());
        return;
    }
    UsdShadeMaterialBindingAPI materialBinding = UsdShadeMaterialBindingAPI::Apply(prim);
    materialBinding.Bind(material);
}

UsdShadeShader createUsdPreviewSurfaceShader(UsdShadeMaterial& material, const std::string& name)
{
    UsdPrim prim = material.GetPrim();

    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(prim, name, &reason))
    {
        REVIT_LOG_WARN("Unable to create UsdShadeShader due to an invalid location: %s", reason.c_str());
        return UsdShadeShader();
    }

    SdfPath path = prim.GetPath().AppendChild(TfToken(name));
    UsdStagePtr stage = prim.GetStage();

    UsdShadeShader shader = UsdShadeShader::Define(stage, path);
    shader.SetShaderId(_tokens->usdPreviewSurface);

    material.CreateSurfaceOutput().ConnectToSource(shader.CreateOutput(UsdShadeTokens->surface, SdfValueTypeNames->Token));
    material.CreateDisplacementOutput().ConnectToSource(shader.CreateOutput(UsdShadeTokens->displacement, SdfValueTypeNames->Token));

    return shader;
}

void setFractionalOpacity(UsdStagePtr stage, bool isOn = true)
{
    VtDictionary cld = stage->GetRootLayer()->GetCustomLayerData();
    VtDictionary renderSettings;
    if (auto entry = cld.find("renderSettings"); entry != cld.end())
    {
        renderSettings = *&(entry->second.Get<VtDictionary>());
    }
    renderSettings["rtx:raytracing:fractionalCutoutOpacity"] = isOn;
    cld.SetValueAtPath("renderSettings", VtValue(renderSettings));
    stage->GetRootLayer()->SetCustomLayerData(cld);
}

UsdShadeMaterial defineOmniPbrMaterial(UsdStagePtr stage, const SdfPath& path, const GfVec3f& color, const float opacity, const float roughness, const float metallic)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(stage, path, &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial due to an invalid location: %s", reason.c_str());
        return UsdShadeMaterial();
    }

    // The opacity value must be within the defined min/max range
    if (opacity < 0.0 || opacity > 1.0)
    {
        reason = TfStringPrintf("Opacity value %g is outside range [0.0 - 1.0].", opacity);
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\" due to an invalid shader parameter value: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdShadeMaterial();
    }

    // The roughness value must be within the defined min/max range
    if (roughness < 0.0 || roughness > 1.0)
    {
        reason = TfStringPrintf("Roughness value %g is outside range [0.0 - 1.0].", roughness);
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\" due to an invalid shader parameter value: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdShadeMaterial();
    }

    // The metallic value must be within the defined min/max range
    if (metallic < 0.0 || metallic > 1.0)
    {
        reason = TfStringPrintf("Metallic value %g is outside range [0.0 - 1.0].", metallic);
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\" due to an invalid shader parameter value: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdShadeMaterial();
    }

    // Define the material
    // We do not use createMaterial here to avoid double validations
    UsdShadeMaterial material = UsdShadeMaterial::Define(stage, path);
    if (!material)
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdShadeMaterial();
    }

    // Explicitly author the specifier and type name
    UsdPrim prim = material.GetPrim();
    prim.SetSpecifier(SdfSpecifierDef);
    prim.SetTypeName(prim.GetTypeName());

    // Define the surface shader to be used in the "mdl" rendering context
    static const std::string mdlShaderName = "MDLShader";
    static const SdfAssetPath mdlAssetPath = SdfAssetPath(g_omniPbrAssetPath);
    UsdShadeShader mdlShader = createMdlShader(material, mdlShaderName, mdlAssetPath, _tokens->omniPbr);
    if (!mdlShader)
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeShader named \"%s\" as a child of \"%s\"", mdlShaderName.c_str(), revit::usd_export::core::detail::getPathAsString(path).c_str());
        // TODO: Cleanup any authored prims before returning a failure
        return UsdShadeMaterial();
    }

    // Define the surface shader to be used in the "default" rendering context
    // The shader parameters will produce a low fidelity approximation of the "mdl" rendering context for use with non-RTX renderers
    static const std::string previewShaderName = "PreviewSurface";
    UsdShadeShader previewShader = createUsdPreviewSurfaceShader(material, previewShaderName);
    if (!previewShader)
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeShader named \"%s\" as a child of \"%s\"", previewShaderName.c_str(), revit::usd_export::core::detail::getPathAsString(path).c_str());
        // TODO: Cleanup any authored prims before returning a failure
        return UsdShadeMaterial();
    }

    // Expose inputs on the material that will be connected to the corresponding inputs on the surface shaders
    // This acts as a Material interface from which value changes will be reflected across multiple renderers
    UsdShadeInput materialColorInput = material.CreateInput(_tokens->materialColor, SdfValueTypeNames->Color3f);
    UsdShadeInput materialOpacityInput = material.CreateInput(_tokens->materialOpacity, SdfValueTypeNames->Float);
    UsdShadeInput materialRoughnessInput = material.CreateInput(_tokens->materialRoughness, SdfValueTypeNames->Float);
    UsdShadeInput materialMetallicInput = material.CreateInput(_tokens->materialMetallic, SdfValueTypeNames->Float);

    // Set the min, max and default metadata on the material interface
    // We would copy this metadata from the connected MDL shader inputs, however the Sdr registry for MDL shaders may not be available.
    // Instead we author the same values that are enforced within this function.
    materialColorInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(GfVec3f(0.2, 0.2, 0.2)));

    materialOpacityInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(1.0f));
    materialOpacityInput.GetAttr().SetCustomDataByKey(_tokens->rangeMin, VtValue(0.0f));
    materialOpacityInput.GetAttr().SetCustomDataByKey(_tokens->rangeMax, VtValue(1.0f));

    materialRoughnessInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(0.5f));
    materialRoughnessInput.GetAttr().SetCustomDataByKey(_tokens->rangeMin, VtValue(0.0f));
    materialRoughnessInput.GetAttr().SetCustomDataByKey(_tokens->rangeMax, VtValue(1.0f));

    materialMetallicInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(0.0f));
    materialMetallicInput.GetAttr().SetCustomDataByKey(_tokens->rangeMin, VtValue(0.0f));
    materialMetallicInput.GetAttr().SetCustomDataByKey(_tokens->rangeMax, VtValue(1.0f));

    // Set the supplied values on the material interface
    materialColorInput.Set(color);
    materialOpacityInput.Set(opacity);
    materialRoughnessInput.Set(roughness);
    materialMetallicInput.Set(metallic);

    // Create MDL shader inputs to produce a physically based rendering result with the supplied values
    // Inputs are either set or connected to the material interface
    mdlShader.CreateInput(_tokens->omniPbrAlbedoColor, SdfValueTypeNames->Color3f).ConnectToSource(materialColorInput);
    mdlShader.CreateInput(_tokens->omniPbrOpacity, SdfValueTypeNames->Float).ConnectToSource(materialOpacityInput);
    mdlShader.CreateInput(_tokens->omniPbrRoughness, SdfValueTypeNames->Float).ConnectToSource(materialRoughnessInput);
    mdlShader.CreateInput(_tokens->omniPbrMetallic, SdfValueTypeNames->Float).ConnectToSource(materialMetallicInput);

    // Enable opacity and set the required render settings if the material is not fully opaque
    if (opacity < 1.0f)
    {
        mdlShader.CreateInput(_tokens->omniPbrOpacityEnabled, SdfValueTypeNames->Bool).Set(true);
        setFractionalOpacity(stage);
    }

    // Create default shader inputs to produce a physically based rendering result with the supplied values
    // Inputs are either set or connected to the material interface
    previewShader.CreateInput(_tokens->usdPreviewSurfaceColor, SdfValueTypeNames->Color3f).ConnectToSource(materialColorInput);
    previewShader.CreateInput(_tokens->usdPreviewSurfaceOpacity, SdfValueTypeNames->Float).ConnectToSource(materialOpacityInput);
    previewShader.CreateInput(_tokens->usdPreviewSurfaceRoughness, SdfValueTypeNames->Float).ConnectToSource(materialRoughnessInput);
    previewShader.CreateInput(_tokens->usdPreviewSurfaceMetallic, SdfValueTypeNames->Float).ConnectToSource(materialMetallicInput);

    return material;
}

bool addEmissiveColorToPbrMaterial(UsdShadeMaterial& material, const GfVec3f& color, const float intensity)
{
    if (!material)
    {
        REVIT_LOG_WARN("Cannot add emissive color, UsdShadeMaterial is not a valid material");
        return false;
    }

    // The material is expected to have both an MDL (OmniPBR) surface shader and a USD Preview Surface shader, as authored by defineOmniPbrMaterial
    UsdShadeShader mdlShader = computeEffectiveMdlSurfaceShader(material);
    UsdShadeShader previewShader = computeEffectivePreviewSurfaceShader(material);
    if (!mdlShader || !previewShader || (mdlShader.GetPrim() == previewShader.GetPrim()))
    {
        REVIT_LOG_WARN(
            "Cannot add emissive color, UsdShadeMaterial <%s> must be created by defineOmniPbrMaterial()",
            revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str()
        );
        return false;
    }

    // MDL (OmniPBR) render context: author the same raw inputs Revit has always emitted so the RTX appearance is preserved
    mdlShader.CreateInput(_tokens->omniPbrEmissiveColor, SdfValueTypeNames->Color3f).Set(color);
    mdlShader.CreateInput(_tokens->omniPbrEmissiveEnable, SdfValueTypeNames->Bool).Set(true);
    mdlShader.CreateInput(_tokens->omniPbrEmissiveIntensity, SdfValueTypeNames->Float).Set(intensity);

    // Universal render context: wire the UsdPreviewSurface emissiveColor so non-RTX/MDL viewers show the self-illumination
    previewShader.CreateInput(_tokens->usdPreviewSurfaceEmissiveColor, SdfValueTypeNames->Color3f).Set(color);

    return true;
}

// Common function to check that a material has an OmniPBR-based MDL & USD Preview Surface shaders
bool verifyValidOmniPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    if (!material)
    {
        REVIT_LOG_WARN("Cannot add texture <%s>, UsdShadeMaterial <%s> is not a valid material", texturePath.GetAssetPath().c_str(), revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());
        return false;
    }
    UsdShadeShader psShader = computeEffectivePreviewSurfaceShader(material);
    if (!psShader)
    {
        REVIT_LOG_WARN("Cannot add texture <%s>, UsdShadeMaterial <%s> does not have a valid USD Preview Surface Shader", texturePath.GetAssetPath().c_str(), revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());
        return false;
    }
    UsdShadeShader mdlShader = computeEffectiveMdlSurfaceShader(material);
    if (!mdlShader || (mdlShader.GetPrim() == psShader.GetPrim()))
    {
        REVIT_LOG_WARN("Cannot add texture <%s>, UsdShadeMaterial <%s> does not have a valid MDL Shader", texturePath.GetAssetPath().c_str(), revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());
        return false;
    }
    SdfAssetPath sourceAsset;
    bool sourceAssetSet = mdlShader.GetSourceAsset(&sourceAsset, _tokens->mdl);
    if (!sourceAssetSet || (sourceAsset.GetAssetPath() != std::string(g_omniPbrAssetPath)))
    {
        REVIT_LOG_WARN(
            "Cannot add texture <%s>, the UsdShadeShader <%s> does not have the correct source asset <%s>. It is using <%s>",
            texturePath.GetAssetPath().c_str(),
            revit::usd_export::core::detail::getPathAsString(mdlShader.GetPath()).c_str(),
            g_omniPbrAssetPath,
            sourceAssetSet ? sourceAsset.GetAssetPath().c_str() : ""
        );
        return false;
    }
    return true;
}

UsdShadeShader computeEffectivePreviewSurfaceShader(const UsdShadeMaterial& material)
{
    if (!material)
    {
        return UsdShadeShader();
    }

    return material.ComputeSurfaceSource({ UsdShadeTokens->universalRenderContext });
}

UsdShadeShader findOrCreateStPrimvarReader(UsdShadeMaterial& material)
{
    SdfPath primvarReaderShaderPath = material.GetPath().AppendChild(_tokens->usdPreviewSurfacePrimST);
    UsdShadeShader stShader = UsdShadeShader::Get(material.GetPrim().GetStage(), primvarReaderShaderPath);
    if (!stShader)
    {
        // Create the "USD Primvar reader for float2" shader
        stShader = UsdShadeShader::Define(material.GetPrim().GetStage(), primvarReaderShaderPath);
        if (!stShader)
        {
            REVIT_LOG_ERROR("Cannot add USD Preview Surface Primvar Reader shader <%s> to <%s>", revit::usd_export::core::detail::getPathAsString(primvarReaderShaderPath).c_str(), revit::usd_export::core::detail::getPathAsString(material.GetPath()).c_str());

            return stShader;
        }
    }

    // Whether the shader already existed or not, make sure that the attributes work for the primvar reader
    stShader.CreateIdAttr(VtValue(_tokens->usdPreviewSurfacePrimvarReader));
    stShader.CreateOutput(_tokens->usdPreviewSurfaceResult, SdfValueTypeNames->Float2);
    stShader.CreateInput(_tokens->usdPreviewSurfaceVarname, SdfValueTypeNames->Token).Set(UsdUtilsGetPrimaryUVSetName());

    return stShader;
}

UsdShadeInput createMaterialLinkedMdlFileInput(UsdShadeMaterial& materialPrim, const TfToken& materialInputName, const TfToken& shaderInputName, const SdfAssetPath& filePath, const TfToken& colorSpace)
{
    UsdShadeShader shaderPrim = computeEffectiveMdlSurfaceShader(materialPrim);
    UsdShadeInput matTextureInput = materialPrim.CreateInput(materialInputName, SdfValueTypeNames->Asset);
    matTextureInput.Set(filePath);
    // MDL render context requires that the color space (sampling mode) be an attribute on the file attribute
    UsdAttribute attr = matTextureInput.GetAttr();
    attr.SetColorSpace(colorSpace);
    UsdShadeInput surfaceInput = shaderPrim.CreateInput(shaderInputName, SdfValueTypeNames->Asset);
    surfaceInput.ConnectToSource(matTextureInput);
    return matTextureInput;
}

// Check if the file extension for the texture asset matches a set of known 8 bit texture formats
// Note, the UsdShadInput provided is expected to be for an SdfAssetPath for the shader's texture file input
bool isEightBitTextureFormat(const UsdShadeInput& textureAssetPathInput)
{
    SdfAssetPath resolvedTexturePath;
    textureAssetPathInput.Get(&resolvedTexturePath);

    static const std::vector<std::string> s_eightBitFormats = { "bmp", "tga", "jpg", "jpeg", "png", "tif" };
    std::string ext = ArGetResolver().GetExtension(resolvedTexturePath.GetResolvedPath());
    return std::find(s_eightBitFormats.begin(), s_eightBitFormats.end(), ext) != s_eightBitFormats.end();
}

bool addDiffuseTextureToPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    if (!verifyValidOmniPbrMaterial(material, texturePath))
    {
        return false;
    }

    // Because we have a texture, remove this "Color" material input created
    // Copy the value and set it to the MDL color input
    GfVec3f color(1.0f);
    UsdShadeInput matColorInput = material.GetInput(_tokens->materialColor);
    if (matColorInput)
    {
        matColorInput.Get<GfVec3f>(&color);
        createMdlShaderInput(material, _tokens->omniPbrAlbedoColor, VtValue(color), SdfValueTypeNames->Color3f);
        removeProperty(material.GetPrim().GetStage(), material.GetPrim().GetPath(), _tokens->materialColorInputs);
    }
    UsdShadeInput matTextureInput = revit::usd_export::core::createMaterialLinkedMdlFileInput(material, _tokens->materialDiffuseTexture, _tokens->omniPbrDiffuseTexture, texturePath, _tokens->colorSpaceAuto);

    // USD Preview Surface
    // Make sure there is a primvar reader for the UV data ("st")
    UsdShadeShader stShader = findOrCreateStPrimvarReader(material);
    if (!stShader)
    {
        return false;
    }

    // Create the "Diffuse Color Tex" shader
    SdfPath shaderPath = material.GetPath().AppendChild(_tokens->usdPreviewSurfaceDiffuseColorTex);
    UsdShadeShader texShader = UsdShadeShader::Define(material.GetPrim().GetStage(), shaderPath);
    texShader.CreateIdAttr(VtValue(_tokens->usdPreviewSurfaceUvTexture));
    if (!texShader.GetInput(_tokens->usdPreviewSurfaceFallback))
    {
        texShader.CreateInput(_tokens->usdPreviewSurfaceFallback, SdfValueTypeNames->Float4).Set(GfVec4f(color[0], color[1], color[2], 1.0f));
    }
    texShader.CreateInput(_tokens->usdPreviewSurfaceFile, SdfValueTypeNames->Asset).ConnectToSource(matTextureInput);
    texShader.CreateInput(_tokens->usdPreviewSurfaceSourceColorSpace, SdfValueTypeNames->Token).Set(_tokens->colorSpaceAuto);
    texShader.CreateInput(_tokens->usdPreviewSurfaceWrapS, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    texShader.CreateInput(_tokens->usdPreviewSurfaceWrapT, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    texShader.CreateInput(UsdUtilsGetPrimaryUVSetName(), SdfValueTypeNames->Float2).ConnectToSource(stShader.GetOutput(_tokens->usdPreviewSurfaceResult));

    UsdShadeOutput texShaderOutput = texShader.CreateOutput(_tokens->usdPreviewSurfaceRgb, SdfValueTypeNames->Float3);

    // Connect the PreviewSurface shader "diffuseColor" to the diffuse tex shader output
    UsdShadeShader psShader = computeEffectivePreviewSurfaceShader(material);
    UsdShadeInput diffuseColorInput = psShader.CreateInput(_tokens->usdPreviewSurfaceColor, SdfValueTypeNames->Color3f);
    diffuseColorInput.ConnectToSource(texShaderOutput);
    return true;
}

bool addNormalTextureToPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    if (!verifyValidOmniPbrMaterial(material, texturePath))
    {
        return false;
    }

    UsdShadeInput matTextureInput = revit::usd_export::core::createMaterialLinkedMdlFileInput(material, _tokens->materialNormalTexture, _tokens->omniPbrNormalTexture, texturePath, _tokens->colorSpaceRaw);

    // USD Preview Surface
    // Make sure there is a primvar reader for the UV data ("st")
    UsdShadeShader stShader = findOrCreateStPrimvarReader(material);
    if (!stShader)
    {
        return false;
    }

    // Create the "Normal Tex" shader
    SdfPath shaderPath = material.GetPath().AppendChild(_tokens->usdPreviewSurfaceNormalTex);
    UsdShadeShader normalShader = UsdShadeShader::Define(material.GetPrim().GetStage(), shaderPath);
    normalShader.CreateIdAttr(VtValue(_tokens->usdPreviewSurfaceUvTexture));
    normalShader.CreateInput(_tokens->usdPreviewSurfaceFallback, SdfValueTypeNames->Float4).Set(GfVec4f(0.0f, 0.0f, 1.0f, 1.0f));
    normalShader.CreateInput(_tokens->usdPreviewSurfaceFile, SdfValueTypeNames->Asset).ConnectToSource(matTextureInput);
    normalShader.CreateInput(_tokens->usdPreviewSurfaceSourceColorSpace, SdfValueTypeNames->Token).Set(_tokens->colorSpaceRaw);
    normalShader.CreateInput(_tokens->usdPreviewSurfaceWrapS, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    normalShader.CreateInput(_tokens->usdPreviewSurfaceWrapT, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    if (isEightBitTextureFormat(normalShader.GetInput(_tokens->usdPreviewSurfaceFile)))
    {
        // set the scale and bias to adjust normals into tangent space
        normalShader.CreateInput(_tokens->usdPreviewSurfaceScale, SdfValueTypeNames->Float4).Set(GfVec4f(2, 2, 2, 1));
        normalShader.CreateInput(_tokens->usdPreviewSurfaceBias, SdfValueTypeNames->Float4).Set(GfVec4f(-1, -1, -1, 0));
    }
    normalShader.CreateInput(UsdUtilsGetPrimaryUVSetName(), SdfValueTypeNames->Float2).ConnectToSource(stShader.GetOutput(_tokens->usdPreviewSurfaceResult));

    UsdShadeOutput normalShaderOutput = normalShader.CreateOutput(_tokens->usdPreviewSurfaceRgb, SdfValueTypeNames->Float3);

    // Connect the PreviewSurface shader "normal" to the normal tex shader output
    UsdShadeShader psShader = computeEffectivePreviewSurfaceShader(material);
    UsdShadeInput normalInput = psShader.CreateInput(_tokens->usdPreviewSurfaceNormal, SdfValueTypeNames->Normal3f);
    normalInput.ConnectToSource(normalShaderOutput);
    return true;
}

bool addOrmTextureToPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    if (!verifyValidOmniPbrMaterial(material, texturePath))
    {
        return false;
    }

    // Because we have a texture, remove the "Metallic" & "Roughness" material inputs created
    // Copy the values first and set it to the MDL shader inputs
    float metallic = 0.0f;
    UsdShadeInput input = material.GetInput(_tokens->materialMetallic);
    if (input)
    {
        input.Get<float>(&metallic);
        createMdlShaderInput(material, _tokens->omniPbrMetallic, VtValue(metallic), SdfValueTypeNames->Float);
        removeProperty(material.GetPrim().GetStage(), material.GetPrim().GetPath(), _tokens->materialMetallicInputs);
    }

    float roughness = 0.5f;
    input = material.GetInput(_tokens->materialRoughness);
    if (input)
    {
        input.Get<float>(&roughness);
        createMdlShaderInput(material, _tokens->omniPbrRoughness, VtValue(roughness), SdfValueTypeNames->Float);
        removeProperty(material.GetPrim().GetStage(), material.GetPrim().GetPath(), _tokens->materialRoughnessInputs);
    }

    // These need to be set for MDL to use an ORM map
    createMdlShaderInput(material, _tokens->omniPbrRoughnessTextureInfluence, VtValue(1.0f), SdfValueTypeNames->Float);
    createMdlShaderInput(material, _tokens->omniPbrMetallicTextureInfluence, VtValue(1.0f), SdfValueTypeNames->Float);
    createMdlShaderInput(material, _tokens->omniPbrOrmTextureEnabled, VtValue(true), SdfValueTypeNames->Bool);
    UsdShadeInput matTextureInput = revit::usd_export::core::createMaterialLinkedMdlFileInput(material, _tokens->materialOrmTexture, _tokens->omniPbrOrmTexture, texturePath, _tokens->colorSpaceRaw);

    // USD Preview Surface
    // Make sure there is a primvar reader for the UV data ("st")
    UsdShadeShader stShader = findOrCreateStPrimvarReader(material);
    if (!stShader)
    {
        return false;
    }

    // Create the "ORM Color Tex" shader
    SdfPath shaderPath = material.GetPath().AppendChild(_tokens->usdPreviewSurfaceOrmTex);
    UsdShadeShader ormShader = UsdShadeShader::Define(material.GetPrim().GetStage(), shaderPath);
    ormShader.CreateIdAttr(VtValue(_tokens->usdPreviewSurfaceUvTexture));
    if (!ormShader.GetInput(_tokens->usdPreviewSurfaceFallback))
    {
        ormShader.CreateInput(_tokens->usdPreviewSurfaceFallback, SdfValueTypeNames->Float4).Set(GfVec4f(1.0f, roughness, metallic, 0.0f));
    }
    ormShader.CreateInput(_tokens->usdPreviewSurfaceFile, SdfValueTypeNames->Asset).ConnectToSource(matTextureInput);
    ormShader.CreateInput(_tokens->usdPreviewSurfaceSourceColorSpace, SdfValueTypeNames->Token).Set(_tokens->colorSpaceRaw);
    ormShader.CreateInput(_tokens->usdPreviewSurfaceWrapS, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    ormShader.CreateInput(_tokens->usdPreviewSurfaceWrapT, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    ormShader.CreateInput(UsdUtilsGetPrimaryUVSetName(), SdfValueTypeNames->Float2).ConnectToSource(stShader.GetOutput(_tokens->usdPreviewSurfaceResult));

    UsdShadeOutput oOutput = ormShader.CreateOutput(_tokens->usdPreviewSurfaceRedChannel, SdfValueTypeNames->Float);
    UsdShadeOutput rOutput = ormShader.CreateOutput(_tokens->usdPreviewSurfaceGreenChannel, SdfValueTypeNames->Float);
    UsdShadeOutput mOutput = ormShader.CreateOutput(_tokens->usdPreviewSurfaceBlueChannel, SdfValueTypeNames->Float);

    // Connect the PreviewSurface shader "occlusion", "roughness", "metallic" to the ORM tex shader outputs
    UsdShadeShader psShader = computeEffectivePreviewSurfaceShader(material);
    psShader.CreateInput(_tokens->usdPreviewSurfaceOcclusion, SdfValueTypeNames->Float).ConnectToSource(oOutput);
    psShader.CreateInput(_tokens->usdPreviewSurfaceRoughness, SdfValueTypeNames->Float).ConnectToSource(rOutput);
    psShader.CreateInput(_tokens->usdPreviewSurfaceMetallic, SdfValueTypeNames->Float).ConnectToSource(mOutput);
    return true;
}

//! Add a single channel texture to an OmniPBR material (roughness, metallic, opacity)
//!
//! The color space (sampling mode) will be set to raw.
//!
//! ---------------------------------------------------------------------------------------------------------------
//! | Material prim
//! |-- input <matValueToken> (float, connected to MDL and USD PS shader inputs) - removed
//! |-- input <matTextureInputToken> (asset connected to MDL and USD PS shader texture inputs, set to texturePath)
//!   | MDL Shader prim ("MDLShader")
//!   |-- input <omniPBShaderValueToken> (float, set to old matValueToken value as fallback)
//!   |-- input <omniPbrTextureToken> (asset, connected to mat input)
//!   |-- input[s] <omniPbrInputValues> (MDL "enable", "influence" inputs are inconsistent so allow a list)
//!   | USD Preview Surface Shader prim ("UsdPreviewSurface")
//!   |-- input <usdShaderInputToken> (float, connected to texture shader output)
//!   | USD Preview Surface Shader prim (<usdTextureShaderToken>)
//!   |-- input fallback (float, set to old matValueToken value as fallback)
//!   |-- input file (asset, connected to mat input)
//! ---------------------------------------------------------------------------------------------------------------//!
//! @param material The UsdShadeMaterial prim to add the texture
//! @param texturePath The SdfAssetPath to the texture file
//! @param matValueToken The Material input name to remove, will be read to grab the fallback value
//! @param matValueInputsToken The Material input name to remove (with "inputs:" prepended)
//! @param matTextureInputToken The Material input name for the added texture input
//! @param omniPbrFallbackValueToken The MDL shader input that was formally connected to the matValueToken input
//! @param omniPbrInputValues A list of inputs that will be set on the MDL shader (enable, influence, etc.)
//! @param omniPbrTextureToken The MDL Shader input name for the texture
//! @param usdTextureShaderToken The USD Preview Surface texture shader name
//! @param usdShaderInputToken The USD Preview Surface input to connect to the texture shader
//!
//! @returns Whether or not the texture was added to the material
bool addSingleChannelTextureToPbrMaterial(
    UsdShadeMaterial& material,
    const SdfAssetPath& texturePath,
    const TfToken& matValueToken,
    const TfToken& matValueInputsToken,
    const TfToken& matTextureInputToken,
    const TfToken& omniPbrFallbackValueToken,
    std::vector<TfTokenValuePair> omniPbrInputValues,
    const TfToken& omniPbrTextureToken,
    const TfToken& usdTextureShaderToken,
    const TfToken& usdShaderInputToken
)
{
    if (!verifyValidOmniPbrMaterial(material, texturePath))
    {
        return false;
    }

    // Because we have a texture, remove the material input created
    // Copy the value first and set it to the MDL shader inputs
    float channelValue = 1.0f;
    UsdShadeInput input = material.GetInput(matValueToken);
    if (input)
    {
        input.Get<float>(&channelValue);
        createMdlShaderInput(material, omniPbrFallbackValueToken, VtValue(channelValue), SdfValueTypeNames->Float);
        removeProperty(material.GetPrim().GetStage(), material.GetPrim().GetPath(), matValueInputsToken);
    }

    // These need to be set for MDL to use this type texture file
    for (const TfTokenValuePair& pair : omniPbrInputValues)
    {
        createMdlShaderInput(material, pair.inputName, pair.value, pair.valueTypeName);
    }

    UsdShadeInput matTextureInput = revit::usd_export::core::createMaterialLinkedMdlFileInput(material, matTextureInputToken, omniPbrTextureToken, texturePath, _tokens->colorSpaceRaw);

    // USD Preview Surface
    // Make sure there is a primvar reader for the UV data ("st")
    UsdShadeShader stShader = findOrCreateStPrimvarReader(material);
    if (!stShader)
    {
        return false;
    }

    // Create the single channel texture shader
    SdfPath shaderPath = material.GetPath().AppendChild(usdTextureShaderToken);
    UsdShadeShader texShader = UsdShadeShader::Define(material.GetPrim().GetStage(), shaderPath);
    texShader.CreateIdAttr(VtValue(_tokens->usdPreviewSurfaceUvTexture));
    if (!texShader.GetInput(_tokens->usdPreviewSurfaceFallback))
    {
        texShader.CreateInput(_tokens->usdPreviewSurfaceFallback, SdfValueTypeNames->Float4).Set(GfVec4f(channelValue, 0.0f, 0.0f, 1.0f));
    }
    texShader.CreateInput(_tokens->usdPreviewSurfaceFile, SdfValueTypeNames->Asset).ConnectToSource(matTextureInput);
    texShader.CreateInput(_tokens->usdPreviewSurfaceSourceColorSpace, SdfValueTypeNames->Token).Set(_tokens->colorSpaceRaw);
    texShader.CreateInput(_tokens->usdPreviewSurfaceWrapS, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    texShader.CreateInput(_tokens->usdPreviewSurfaceWrapT, SdfValueTypeNames->Token).Set(_tokens->usdPreviewSurfaceRepeat);
    texShader.CreateInput(UsdUtilsGetPrimaryUVSetName(), SdfValueTypeNames->Float2).ConnectToSource(stShader.GetOutput(_tokens->usdPreviewSurfaceResult));

    UsdShadeOutput output = texShader.CreateOutput(_tokens->usdPreviewSurfaceRedChannel, SdfValueTypeNames->Float);

    // Connect the PreviewSurface shader "opacity" to the opacity tex shader output
    UsdShadeShader psShader = computeEffectivePreviewSurfaceShader(material);
    psShader.CreateInput(usdShaderInputToken, SdfValueTypeNames->Float).ConnectToSource(output);
    return true;
}

bool addRoughnessTextureToPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    std::vector<TfTokenValuePair> tokenValuePairs = { { _tokens->omniPbrRoughnessTextureInfluence, VtValue(1.0f), SdfValueTypeNames->Float } };

    return addSingleChannelTextureToPbrMaterial(
        material,
        texturePath,
        _tokens->materialRoughness,
        _tokens->materialRoughnessInputs,
        _tokens->materialRoughnessTexture,
        _tokens->omniPbrRoughness,
        tokenValuePairs,
        _tokens->omniPbrRoughnessTexture,
        _tokens->usdPreviewSurfaceRoughnessTex,
        _tokens->usdPreviewSurfaceRoughness
    );
}

bool addMetallicTextureToPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    std::vector<TfTokenValuePair> tokenValuePairs = { { _tokens->omniPbrMetallicTextureInfluence, VtValue(1.0f), SdfValueTypeNames->Float } };

    return addSingleChannelTextureToPbrMaterial(
        material,
        texturePath,
        _tokens->materialMetallic,
        _tokens->materialMetallicInputs,
        _tokens->materialMetallicTexture,
        _tokens->omniPbrMetallic,
        tokenValuePairs,
        _tokens->omniPbrMetallicTexture,
        _tokens->usdPreviewSurfaceMetallicTex,
        _tokens->usdPreviewSurfaceMetallic
    );
}

bool addOpacityTextureToPbrMaterial(UsdShadeMaterial& material, const SdfAssetPath& texturePath)
{
    std::vector<TfTokenValuePair> tokenValuePairs = { { _tokens->omniPbrOpacityEnabled, VtValue(true), SdfValueTypeNames->Bool },
                                                      { _tokens->omniPbrOpacityTextureEnabled, VtValue(true), SdfValueTypeNames->Bool },
                                                      { _tokens->omniPbrOpacityThreshold, VtValue(std::numeric_limits<float>::epsilon()), SdfValueTypeNames->Float } };

    bool success = addSingleChannelTextureToPbrMaterial(
        material,
        texturePath,
        _tokens->materialOpacity,
        _tokens->materialOpacityInputs,
        _tokens->materialOpacityTexture,
        _tokens->omniPbrOpacity,
        tokenValuePairs,
        _tokens->omniPbrOpacityTexture,
        _tokens->usdPreviewSurfaceOpacityTex,
        _tokens->usdPreviewSurfaceOpacity
    );

    if (success)
    {
        UsdShadeShader psShader = computeEffectivePreviewSurfaceShader(material);
        // IOR should be 1.0 for a PBR style material, it causes mask/opacity issues if not
        psShader.CreateInput(_tokens->usdPreviewSurfaceIor, SdfValueTypeNames->Float).Set(1.0f);
        // Geometric cutouts work better with opacity threshold set to above 0
        psShader.CreateInput(_tokens->usdPreviewSurfaceOpacityThreshold, SdfValueTypeNames->Float).Set(std::numeric_limits<float>::epsilon());
    }

    return success;
}

UsdShadeMaterial defineOmniGlassMaterial(UsdStagePtr stage, const SdfPath& path, const GfVec3f& color, const float indexOfRefraction, const float roughness)
{
    // Early out if the proposed prim location is invalid
    std::string reason;
    if (!revit::usd_export::core::detail::isEditablePrimLocation(stage, path, &reason))
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial due to an invalid location: %s", reason.c_str());
        return UsdShadeMaterial();
    }

    // The color value must be within the defined min, max range
    if (color[0] < 0.0 || color[1] < 0.0 || color[2] < 0.0 || color[0] > 1.0 || color[1] > 1.0 || color[2] > 1.0)
    {
        reason = TfStringPrintf("Color value (%g, %g, %g)  is outside range [(0, 0, 0) - (1, 1, 1)].", color[0], color[1], color[2]);
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\" due to an invalid shader parameter value: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdShadeMaterial();
    }

    // The index of refraction value must be within the defined soft min, soft max range
    if (indexOfRefraction < 1.0 || indexOfRefraction > 4.0)
    {
        reason = TfStringPrintf("IOR value %g is outside range [1.0 - 4.0].", indexOfRefraction);
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\" due to an invalid shader parameter value: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdShadeMaterial();
    }

    // The roughness value must be within the defined min, max range
    if (roughness < 0.0 || roughness > 1.0)
    {
        reason = TfStringPrintf("Roughness value %g is outside range [0.0 - 1.0].", roughness);
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\" due to an invalid shader parameter value: %s", revit::usd_export::core::detail::getPathAsString(path).c_str(), reason.c_str());
        return UsdShadeMaterial();
    }

    // Define the material
    // We do not use omni::connect::core::createMaterial here to avoid double validations
    UsdShadeMaterial material = UsdShadeMaterial::Define(stage, path);
    if (!material)
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeMaterial at \"%s\"", revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdShadeMaterial();
    }

    // Explicitly author the specifier and type name
    UsdPrim prim = material.GetPrim();
    prim.SetSpecifier(SdfSpecifierDef);
    prim.SetTypeName(prim.GetTypeName());

    // Define the surface shader to be used in the "mdl" rendering context
    static const std::string mdlShaderName = "MDLShader";
    const SdfAssetPath mdlAssetPath = SdfAssetPath("OmniGlass.mdl");
    UsdShadeShader mdlShader = createMdlShader(material, mdlShaderName, mdlAssetPath, _tokens->omniGlass);
    if (!mdlShader)
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeShader named \"%s\" as a child of \"%s\"", mdlShaderName.c_str(), revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdShadeMaterial();
    }

    // Define the surface shader to be used in the "default" rendering context
    // The shader parameters will produce a low fidelity approximation of the "mdl" rendering context for use with non-RTX renderers
    static const std::string previewShaderName = "PreviewSurface";
    UsdShadeShader previewShader = createUsdPreviewSurfaceShader(material, previewShaderName);
    if (!previewShader)
    {
        REVIT_LOG_ERROR("Unable to define UsdShadeShader named \"%s\" as a child of \"%s\"", previewShaderName.c_str(), revit::usd_export::core::detail::getPathAsString(path).c_str());
        return UsdShadeMaterial();
    }

    // Expose inputs on the material that will be connected to the corresponding inputs on the surface shaders
    // This acts as a Material interface from which value changes will be reflected across multiple renderers
    UsdShadeInput materialColorInput = material.CreateInput(_tokens->materialColor, SdfValueTypeNames->Color3f);
    UsdShadeInput materialIorInput = material.CreateInput(_tokens->materialIor, SdfValueTypeNames->Float);
    UsdShadeInput materialRoughnessInput = material.CreateInput(_tokens->materialRoughness, SdfValueTypeNames->Float);
    UsdShadeInput materialOpacityInput = material.CreateInput(_tokens->materialOpacity, SdfValueTypeNames->Float);

    // Set the min, max and default metadata on the material interface
    // We would copy this metadata from the connected MDL shader inputs, however the Sdr registry for MDL shaders may not be available.
    // Instead we author the same values that are enforced within this function.
    materialColorInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(GfVec3f(1.0, 1.0, 1.0)));
    materialColorInput.GetAttr().SetCustomDataByKey(_tokens->rangeMin, VtValue(GfVec3f(0.0, 0.0, 0.0)));
    materialColorInput.GetAttr().SetCustomDataByKey(_tokens->rangeMax, VtValue(GfVec3f(1.0, 1.0, 1.0)));

    materialIorInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(1.491f));
    materialIorInput.GetAttr().SetCustomDataByKey(_tokens->softRangeMin, VtValue(1.0f));
    materialIorInput.GetAttr().SetCustomDataByKey(_tokens->softRangeMax, VtValue(4.0f));

    materialRoughnessInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(0.02f));
    materialRoughnessInput.GetAttr().SetCustomDataByKey(_tokens->rangeMin, VtValue(0.0f));
    materialRoughnessInput.GetAttr().SetCustomDataByKey(_tokens->rangeMax, VtValue(1.0f));

    materialOpacityInput.GetAttr().SetCustomDataByKey(_tokens->defaultValue, VtValue(0.2f));
    materialOpacityInput.GetAttr().SetCustomDataByKey(_tokens->rangeMin, VtValue(0.0f));
    materialOpacityInput.GetAttr().SetCustomDataByKey(_tokens->rangeMax, VtValue(1.0f));

    // Set the supplied values on the material interface
    materialColorInput.Set(color);
    materialIorInput.Set(indexOfRefraction);
    materialRoughnessInput.Set(roughness);
    materialOpacityInput.Set(0.2f);

    // Create MDL shader inputs to produce a glass result with the supplied values
    // Inputs are either set or connected to the material interface
    mdlShader.CreateInput(_tokens->omniGlassColor, SdfValueTypeNames->Color3f).ConnectToSource(materialColorInput);
    mdlShader.CreateInput(_tokens->omniGlassIor, SdfValueTypeNames->Float).ConnectToSource(materialIorInput);
    mdlShader.CreateInput(_tokens->omniGlassFrostingRoughness, SdfValueTypeNames->Float).ConnectToSource(materialRoughnessInput);

    // Create default shader inputs to produce a glass result with the supplied values
    // Inputs are either set or connected to the material interface
    // "opacity" is connected to the material interface (default 0.2) so frosted/transparent glass renders on non-RTX renderers
    previewShader.CreateInput(_tokens->usdPreviewSurfaceColor, SdfValueTypeNames->Color3f).ConnectToSource(materialColorInput);
    previewShader.CreateInput(_tokens->usdPreviewSurfaceIor, SdfValueTypeNames->Float).ConnectToSource(materialIorInput);
    previewShader.CreateInput(_tokens->usdPreviewSurfaceRoughness, SdfValueTypeNames->Float).ConnectToSource(materialRoughnessInput);
    previewShader.CreateInput(_tokens->usdPreviewSurfaceOpacity, SdfValueTypeNames->Float).ConnectToSource(materialOpacityInput);

    return material;
}

} // namespace revit::usd_export::core

extern "C"
{
    REVIT_USD_EXPORT_API float* revit_usd_export_core_sRgbToLinear(const pxr::GfVec3f color)
    {
        static pxr::GfVec3f retColor;
        retColor = revit::usd_export::core::sRgbToLinear(color);
        return &(retColor[0]);
    }

    REVIT_USD_EXPORT_API float* revit_usd_export_core_linearToSrgb(const pxr::GfVec3f color)
    {
        static pxr::GfVec3f retColor;
        retColor = revit::usd_export::core::linearToSrgb(color);
        return &(retColor[0]);
    }

    REVIT_USD_EXPORT_API const char* revit_usd_export_core_createMaterial(const long int stage_id, const char* parent, const char* name)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim parentPrim = stage->GetPrimAtPath(pxr::SdfPath(parent));
        if (!parentPrim.IsValid())
        {
            return nullptr;
        }

        pxr::UsdShadeMaterial material = revit::usd_export::core::createMaterial(parentPrim, std::string(name));
        if (!material.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = material.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    REVIT_USD_EXPORT_API const char* revit_usd_export_core_createMdlShader(const long int stage_id, const char* prim_path, const char* name, const char* mdlPath, const char* module, bool connectMaterialOutputs)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return nullptr;
        }
        if (prim.GetTypeName().GetString() != "Material")
        {
            return nullptr;
        }

        pxr::UsdShadeMaterial material(prim);

        pxr::UsdShadeShader shader = revit::usd_export::core::createMdlShader(material, std::string(name), pxr::SdfAssetPath(mdlPath), pxr::TfToken(module), connectMaterialOutputs);
        if (!shader.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = shader.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_createMdlShaderInputAsset(const long int stage_id, const char* material_path, const char* input_name, const char* value, revit::usd_export::core::ColorSpace color_space)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);

        pxr::UsdShadeInput input = revit::usd_export::core::createMdlShaderInput(material, pxr::TfToken(input_name), pxr::VtValue(value), pxr::SdfValueTypeNames->Asset, color_space);

        return input.GetAttr().IsValid();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_createMdlShaderInputBool(const long int stage_id, const char* material_path, const char* input_name, bool value)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);

        pxr::UsdShadeInput input = revit::usd_export::core::createMdlShaderInput(material, pxr::TfToken(input_name), pxr::VtValue(value), pxr::SdfValueTypeNames->Bool);

        return input.GetAttr().IsValid();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_createMdlShaderInputInt(const long int stage_id, const char* material_path, const char* input_name, const int value)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);

        pxr::UsdShadeInput input = revit::usd_export::core::createMdlShaderInput(material, pxr::TfToken(input_name), pxr::VtValue(value), pxr::SdfValueTypeNames->Int);

        return input.GetAttr().IsValid();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_createMdlShaderInputFloat(const long int stage_id, const char* material_path, const char* input_name, const float value)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);

        pxr::UsdShadeInput input = revit::usd_export::core::createMdlShaderInput(material, pxr::TfToken(input_name), pxr::VtValue(value), pxr::SdfValueTypeNames->Float);

        return input.GetAttr().IsValid();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_createMdlShaderInputFloat2(const long int stage_id, const char* material_path, const char* input_name, const pxr::GfVec2f value)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);

        pxr::UsdShadeInput input = revit::usd_export::core::createMdlShaderInput(material, pxr::TfToken(input_name), pxr::VtValue(value), pxr::SdfValueTypeNames->Float2);

        return input.GetAttr().IsValid();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_createMdlShaderInputColor3f(const long int stage_id, const char* material_path, const char* input_name, const pxr::GfVec3f value)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);

        pxr::UsdShadeInput input = revit::usd_export::core::createMdlShaderInput(material, pxr::TfToken(input_name), pxr::VtValue(value), pxr::SdfValueTypeNames->Color3f);

        return input.GetAttr().IsValid();
    }

    REVIT_USD_EXPORT_API void revit_usd_export_core_bindMaterial(const long int stage_id, const char* prim_path, const char* material_prim_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_prim_path));
        if (materialPrim.GetTypeName().GetString() != "Material")
        {
            return;
        }
        pxr::UsdShadeMaterial material(materialPrim);

        revit::usd_export::core::bindMaterial(prim, material);
    }


    REVIT_USD_EXPORT_API const char* revit_usd_export_core_defineOmniPbrMaterial(const long int stage_id, const char* prim_path, const pxr::GfVec3f color, const float opacity, const float roughness, const float metallic)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdShadeMaterial material = revit::usd_export::core::defineOmniPbrMaterial(stage, pxr::SdfPath(prim_path), color, opacity, roughness, metallic);
        if (!material.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = material.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addEmissiveColorToPbrMaterial(const long int stage_id, const char* material_path, const pxr::GfVec3f color, const float intensity)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addEmissiveColorToPbrMaterial(material, color, intensity);
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addDiffuseTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addDiffuseTextureToPbrMaterial(material, pxr::SdfAssetPath(texture_path));
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addNormalTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addNormalTextureToPbrMaterial(material, pxr::SdfAssetPath(texture_path));
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addOrmTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addOrmTextureToPbrMaterial(material, pxr::SdfAssetPath(texture_path));
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addRoughnessTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addRoughnessTextureToPbrMaterial(material, pxr::SdfAssetPath(texture_path));
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addMetallicTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addMetallicTextureToPbrMaterial(material, pxr::SdfAssetPath(texture_path));
    }

    REVIT_USD_EXPORT_API bool revit_usd_export_core_addOpacityTextureToPbrMaterial(const long int stage_id, const char* material_path, const char* texture_path)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim materialPrim = stage->GetPrimAtPath(pxr::SdfPath(material_path));
        if (!materialPrim.IsValid())
        {
            return false;
        }

        if (!materialPrim.IsA<pxr::UsdShadeMaterial>())
        {
            return false;
        }

        pxr::UsdShadeMaterial material(materialPrim);
        return revit::usd_export::core::addOpacityTextureToPbrMaterial(material, pxr::SdfAssetPath(texture_path));
    }


    REVIT_USD_EXPORT_API const char* revit_usd_export_core_defineOmniGlassMaterial(const long int stage_id, const char* prim_path, const pxr::GfVec3f color, const float indexOfRefraction, const float roughness)
    {
        pxr::UsdStagePtr stage = revit::usd_export::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdShadeMaterial material = revit::usd_export::core::defineOmniGlassMaterial(stage, pxr::SdfPath(prim_path), color, indexOfRefraction, roughness);
        if (!material.GetPrim().IsValid())
        {
            return nullptr;
        }
        const std::string newPath = material.GetPath().GetAsString();

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = revit::usd_export::core::stageCache.getTempData(stage_id, newPath);
        return buff.c_str();
    }
}

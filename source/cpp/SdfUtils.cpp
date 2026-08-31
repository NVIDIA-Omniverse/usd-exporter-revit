// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "SdfUtils.h"

#include "UriUtils.h"
#include "client.h"

#include <pxr/usd/sdf/copyUtils.h>
#include <pxr/usd/sdf/layer.h>
#include <pxr/usd/sdf/path.h>
#include <pxr/usd/sdf/primSpec.h>
#include <pxr/usd/usdUtils/dependencies.h>
#include <pxr/usd/usdUtils/stitch.h>

#include <unordered_set>
#include <vector>

PXR_NAMESPACE_USING_DIRECTIVE

namespace usd::exporter::revit::core
{

static const std::string g_mutenessCustomKey = "omni_layer:muteness";
static const std::string g_lockedCustomKey = "omni_layer:locked";

} // namespace usd::exporter::revit::core

std::string usd::exporter::revit::core::detail::computeAbsolutePath(const SdfLayerRefPtr& rootLayer, const std::string& path)
{
    if (SdfLayer::IsAnonymousLayerIdentifier(path) || rootLayer->IsAnonymous())
    {
        return path;
    }
    else
    {
        // Compute the path through the resolver
        const std::string& absolutePath = rootLayer->ComputeAbsolutePath(path);
        return usd::exporter::revit::core::detail::normalizePath(absolutePath);
    }
}

void usd::exporter::revit::core::detail::resolvePathsInternal(const SdfLayerRefPtr& srcLayer, SdfLayerRefPtr dstLayer, bool storeRelativePath, bool relativeToSrcLayer, bool copyLayerOffsets)
{
    using PathConvertFn = std::function<std::string(const std::string& path)>;
    PathConvertFn makePathAbsolute = [&srcLayer, &dstLayer](const std::string& path)
    {
        if (path.empty())
        {
            return path;
        }

        std::string externRefPathFull;
        if (!srcLayer->IsAnonymous())
        {
            externRefPathFull = usd::exporter::revit::core::detail::computeAbsolutePath(srcLayer, path);
        }
        else
        {
            externRefPathFull = usd::exporter::revit::core::detail::computeAbsolutePath(dstLayer, path);
        }

        if (usd::exporter::revit::core::detail::isSearchPath(path) && !usd_exporter_revit_file_client_uri_exists(externRefPathFull))
        {
            return path;
        }

        if (externRefPathFull.empty())
        {
            // If it failed to compute the absolute path, just returning the original one.
            return path;
        }
        else
        {
            return externRefPathFull;
        }
    };

    PathConvertFn makePathRelative = [&srcLayer, &dstLayer, relativeToSrcLayer](const std::string& path)
    {
        if (path.empty())
        {
            return path;
        }

        std::string relativePath = usd::exporter::revit::core::detail::computeAbsolutePath(srcLayer, path);
        // FIXME: Resolver will firstly find MDL in the same dir as USD
        // for material reference, then Core Library. Currently, this is
        // used to check the existence of the path and see if it's necessary
        // to resolve path of mdl references.
        if (usd::exporter::revit::core::detail::isSearchPath(path) && !usd_exporter_revit_file_client_uri_exists(relativePath))
        {
            return path;
        }

        if (relativePath.empty() || SdfLayer::IsAnonymousLayerIdentifier(relativePath))
        {
            return path;
        }
        else
        {
            // Remove old omni: prefix
            if (relativePath.size() >= 5 && relativePath.substr(0, 5) == "omni:")
            {
                relativePath = relativePath.substr(5);
            }
            if (relativeToSrcLayer)
            {
                relativePath = usd::exporter::revit::core::detail::makeRelativeUrl(srcLayer->GetIdentifier().c_str(), relativePath.c_str());
            }
            else
            {
                relativePath = usd::exporter::revit::core::detail::makeRelativeUrl(dstLayer->GetIdentifier().c_str(), relativePath.c_str());
            }
            relativePath = usd::exporter::revit::core::detail::normalizePath(relativePath);

            // If relative path cannot be computed, it returns absolute path to avoid
            // reference issue. For example, if src and dst are not in the same domain.
            return relativePath;
        }
    };

    // Save offsets and scales.
    const auto& layerOffsets = srcLayer->GetSubLayerOffsets();

    PathConvertFn convertPath = storeRelativePath ? makePathRelative : makePathAbsolute;
    UsdUtilsModifyAssetPaths(
        dstLayer,
        [&convertPath](const std::string& assetPath)
        {
            return convertPath(assetPath);
        }
    );

    // Copy sublayer offsets
    if (copyLayerOffsets)
    {
        for (size_t i = 0; i < layerOffsets.size(); i++)
        {
            const auto& layerOffset = layerOffsets[i];
            dstLayer->SetSubLayerOffset(layerOffset, static_cast<int>(i));
        }
    }

    // Resolve paths saved in customdata.
    VtDictionary valueMap;
    VtDictionary rootLayerCustomData = dstLayer->GetCustomLayerData();
    const auto& customDataValue = rootLayerCustomData.GetValueAtPath(g_mutenessCustomKey);
    if (customDataValue && !customDataValue->IsEmpty())
    {
        valueMap = customDataValue->Get<VtDictionary>();
    }

    VtDictionary newValueMap;
    for (const auto& valuePair : valueMap)
    {
        const std::string& absolutePath = srcLayer->ComputeAbsolutePath(valuePair.first);
        const std::string& relativePath = convertPath(absolutePath);
        newValueMap[relativePath] = valuePair.second;
    }

    rootLayerCustomData.SetValueAtPath(g_mutenessCustomKey, VtValue(newValueMap));
    dstLayer->SetCustomLayerData(rootLayerCustomData);
}

void usd::exporter::revit::core::detail::resolvePaths(const std::string& srcLayerIdentifier, const std::string& targetLayerIdentifier, bool storeRelativePath, bool relativeToSrcLayer, bool copySublayerLayerOffsets)
{
    auto srcLayer = SdfLayer::Find(srcLayerIdentifier);
    auto dstLayer = SdfLayer::Find(targetLayerIdentifier);
    if (!srcLayer || !dstLayer)
    {
        return;
    }

    usd::exporter::revit::core::detail::resolvePathsInternal(srcLayer, dstLayer, storeRelativePath, relativeToSrcLayer, copySublayerLayerOffsets);
}

bool usd::exporter::revit::core::detail::mergePrimSpecInternal(SdfLayerRefPtr dstLayer, const SdfLayerRefPtr& srcLayer, const SdfPath& primSpecPath, bool isDstStrongerThanSrc, const SdfPath& targetPrimPath)
{
    if (dstLayer == srcLayer)
    {
        // If target path is not the same as original path, it means a duplicate.
        if (!targetPrimPath.IsEmpty() && primSpecPath != targetPrimPath)
        {
            SdfCopySpec(srcLayer, primSpecPath, dstLayer, targetPrimPath);
            return true;
        }

        return false;
    }

    if (!srcLayer->HasSpec(primSpecPath) && !dstLayer->HasSpec(primSpecPath))
    {
        return false;
    }

    auto originalStrongLayer = isDstStrongerThanSrc ? dstLayer : srcLayer;
    auto originalWeakLayer = isDstStrongerThanSrc ? srcLayer : dstLayer;
    auto targetLayer = dstLayer;

    // srcLayer is weak and dst is strong
    auto shouldCopyValueFn = [targetLayer](const TfToken& field, const SdfPath& path, const SdfLayerHandle& strongLayer, bool fieldInStrong, const SdfLayerHandle& weakLayer, bool fieldInWeak, VtValue* valueToCopy)
    {
        UsdUtilsStitchValueStatus status = UsdUtilsStitchValueStatus::UseDefaultValue;
        bool handleSublayers = false;
        if (field == SdfFieldKeys->SubLayers)
        {
            handleSublayers = true;
            status = UsdUtilsStitchValueStatus::UseSuppliedValue;
        }
        else if (fieldInWeak && fieldInStrong && field == SdfFieldKeys->Specifier)
        {
            const auto& sObj = weakLayer->GetObjectAtPath(path);
            const auto& dObj = strongLayer->GetObjectAtPath(path);
            const auto& sSpec = sObj->GetField(field).Get<SdfSpecifier>();
            const auto& dSpec = dObj->GetField(field).Get<SdfSpecifier>();

            // if either is not an over, we want the new specifier to be whatever that is.
            if (sSpec != SdfSpecifier::SdfSpecifierOver && dSpec != SdfSpecifier::SdfSpecifierOver)
            {
            }
            if (sSpec != SdfSpecifier::SdfSpecifierOver)
            {
                *valueToCopy = VtValue(sSpec);
                status = UsdUtilsStitchValueStatus::UseSuppliedValue;
            }
            else if (dSpec != SdfSpecifier::SdfSpecifierOver)
            {
                *valueToCopy = VtValue(dSpec);
                status = UsdUtilsStitchValueStatus::UseSuppliedValue;
            }
        }

        if (handleSublayers)
        {
            // Merge sublayers list between src and dst.
            SdfSubLayerProxy weakSublayerProxy = weakLayer->GetSubLayerPaths();
            SdfSubLayerProxy strongSublayerProxy = strongLayer->GetSubLayerPaths();
            std::vector<std::string> mergedSublayerList;
            std::unordered_set<std::string> uniqueSublayers;

            for (size_t i = 0; i < strongSublayerProxy.size(); i++)
            {
                std::string sublayer = strongSublayerProxy[i];
                if (uniqueSublayers.find(sublayer) == uniqueSublayers.end())
                {
                    mergedSublayerList.push_back(sublayer);
                    uniqueSublayers.insert(sublayer);
                }
            }

            for (size_t i = 0; i < weakSublayerProxy.size(); i++)
            {
                std::string sublayer = weakSublayerProxy[i];
                if (uniqueSublayers.find(sublayer) == uniqueSublayers.end())
                {
                    mergedSublayerList.push_back(sublayer);
                    uniqueSublayers.insert(sublayer);
                }
            }

            *valueToCopy = VtValue::Take(mergedSublayerList);
        }

        return status;
    };

    if (!srcLayer->HasSpec(primSpecPath))
    {
        return true;
    }

    auto tempStrongLayer = SdfLayer::CreateAnonymous();
    auto tempWeakLayer = SdfLayer::CreateAnonymous();
    auto tempPath = primSpecPath.IsAbsoluteRootPath() ? SdfPath::AbsoluteRootPath() : SdfPath::AbsoluteRootPath().AppendElementToken(primSpecPath.GetNameToken());
    SdfCreatePrimInLayer(tempStrongLayer, tempPath);
    SdfCreatePrimInLayer(tempWeakLayer, tempPath);
    if (originalStrongLayer->GetPrimAtPath(primSpecPath))
    {
        SdfCopySpec(originalStrongLayer, primSpecPath, tempStrongLayer, tempPath);
        usd::exporter::revit::core::detail::resolvePathsInternal(originalStrongLayer, tempStrongLayer, false);
    }
    if (originalWeakLayer->GetPrimAtPath(primSpecPath))
    {
        SdfCopySpec(originalWeakLayer, primSpecPath, tempWeakLayer, tempPath);
        usd::exporter::revit::core::detail::resolvePathsInternal(originalWeakLayer, tempWeakLayer, false);
    }
    UsdUtilsStitchLayers(tempStrongLayer, tempWeakLayer, shouldCopyValueFn);
    usd::exporter::revit::core::detail::resolvePathsInternal(targetLayer, tempStrongLayer, true, true);

    SdfCreatePrimInLayer(targetLayer, primSpecPath);
    SdfPath newPrimPath;
    if (targetPrimPath != SdfPath::EmptyPath())
    {
        newPrimPath = targetPrimPath;
    }
    else
    {
        newPrimPath = primSpecPath;
    }
    SdfCopySpec(tempStrongLayer, tempPath, targetLayer, newPrimPath);

    return true;
}

bool usd::exporter::revit::core::detail::mergePrimSpec(const std::string& dstLayerIdentifier, const std::string& srcLayerIdentifier, const std::string& primSpecPath, bool isDstStrongerThanSrc, const std::string& targetPrimPath)
{
    auto dstLayer = SdfLayer::Find(dstLayerIdentifier);
    auto srcLayer = SdfLayer::Find(srcLayerIdentifier);
    if (!dstLayer || !srcLayer)
    {
        return false;
    }

    SdfPath sdfPrimPath;
    SdfPath sdfTargetPrimPath;
    if (primSpecPath.empty())
    {
        sdfPrimPath = SdfPath::EmptyPath();
    }
    else
    {
        sdfPrimPath = SdfPath(primSpecPath);
    }

    if (targetPrimPath.empty())
    {
        sdfTargetPrimPath = SdfPath::EmptyPath();
    }
    else
    {
        sdfTargetPrimPath = SdfPath(targetPrimPath);
    }

    return usd::exporter::revit::core::detail::mergePrimSpecInternal(dstLayer, srcLayer, sdfPrimPath, isDstStrongerThanSrc, sdfTargetPrimPath);
}

std::string usd::exporter::revit::core::detail::getPathAsString(const SdfPath& path)
{
#if PXR_VERSION > 2008
    return path.GetAsString();
#else
    return path.GetString();
#endif // PXR_VERSION > 2008
}

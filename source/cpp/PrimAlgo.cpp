// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "PrimAlgo.h"

#include "StageCache.h"
#include "StringUtil.h"
#include "TfUtils.h"

#include <algorithm>
#include <string>
#include <unordered_map>

using namespace pxr;

namespace
{

//! Tracks the names that have already been allocated (and therefore may not be reused) while producing a batch of unique names.
struct ValidNameCache
{
    //! Names that can not be allocated
    std::vector<std::string> usedNames;

    // The start index to be used for making a given name unique
    std::unordered_map<std::string, size_t> startIndices;
};

void reserveNames(ValidNameCache& cache, const std::vector<std::string>& names)
{
    cache.usedNames.reserve(cache.usedNames.size() + names.size());
    for (const std::string& name : names)
    {
        cache.usedNames.push_back(name);
    }
}

//! Produce a matching vector of valid and unique names.
//!
//! Ported from `usdex::core` `getValidNames`: each preferred name is made valid, then made unique by incrementing a numeric suffix on the original
//! name until an available candidate is found. Suffixed candidates that clash with later supplied names are skipped so that the requested name is
//! returned unchanged more often.
std::vector<std::string> getValidNames(const std::vector<std::string>& names, ValidNameCache& cache)
{
    // Early exit if no names given.
    if (names.empty())
    {
        return std::vector<std::string>{};
    }

    // Construct an appropriately sized vector to hold resulting names
    std::vector<std::string> result;
    result.reserve(names.size());

    for (size_t nameIndex = 0; nameIndex < names.size(); ++nameIndex)
    {
        // Keep the original name
        const std::string& originalName = names[nameIndex];

        // Make the name valid before checking uniqueness
        const std::string validName = usd::exporter::revit::core::detail::makeValidIdentifier(originalName);

        // Check if the valid name is already used. Increment a numeric suffix on the original name until an available one is found
        std::string name = validName;
        while (true)
        {
            if (std::find(cache.usedNames.begin(), cache.usedNames.end(), name) == cache.usedNames.end())
            {
                // Avoid allocating suffixed names that exist in the list of supplied names
                // This increases the number of cases where the requested name is returned unchanged
                if (name == validName || std::find(names.begin() + nameIndex + 1, names.end(), name) == names.end())
                {
                    result.push_back(name);
                    cache.usedNames.push_back(name);
                    break;
                }
            }

            // Get the latest index for this name and build a new name.
            size_t& index = cache.startIndices[originalName];

            index++;
            name = usd::exporter::revit::core::detail::makeValidIdentifier(originalName + "_" + std::to_string(index));
        }
    }

    return result;
}

} // namespace

namespace usd::exporter::revit::core
{

std::string getDisplayName(const UsdPrim& prim)
{
    // This function acts as a shim as "UsdObject::GetDisplayName" is not available before OpenUsd version 23.02
    for (const auto& primSpec : prim.GetPrimStack())
    {
        const VtValue displayName = primSpec->GetField(SdfFieldKeys->DisplayName);
        if (!displayName.IsEmpty())
        {
            if (displayName.IsHolding<std::string>())
            {
                return displayName.UncheckedGet<std::string>();
            }
            return "";
        }
    }
    return "";
}

bool setDisplayName(UsdPrim prim, const std::string& name)
{
    // This function acts as a shim as "UsdObject::SetDisplayName" is not available before OpenUsd version 23.02
    if (SdfPrimSpecHandle primSpec = SdfCreatePrimInLayer(prim.GetStage()->GetEditTarget().GetLayer(), prim.GetPath()))
    {
        return primSpec->SetField(SdfFieldKeys->DisplayName, name);
    }
    return false;
}

std::string getValidPrimName(const std::string& name)
{
    return usd::exporter::revit::core::detail::makeValidIdentifier(name);
}

std::vector<std::string> getValidPrimNames(const std::vector<std::string>& names, const std::vector<std::string>& reservedNames)
{
    ValidNameCache cache;
    reserveNames(cache, reservedNames);
    return getValidNames(names, cache);
}

} // namespace usd::exporter::revit::core

extern "C"
{
    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_getDisplayName(const long int stage_id, const char* prim_path)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return nullptr;
        }

        const std::string name = usd::exporter::revit::core::getDisplayName(prim);

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, name);
        return buff.c_str();
    }

    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_setDisplayName(const long int stage_id, const char* prim_path, const char* name)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return false;
        }

        pxr::UsdPrim prim = stage->GetPrimAtPath(pxr::SdfPath(prim_path));
        if (!prim.IsValid())
        {
            return false;
        }

        return usd::exporter::revit::core::setDisplayName(prim, std::string(name));
    }

    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_core_getValidPrimName(const long int stage_id, const char* name)
    {
        const std::string new_name = usd::exporter::revit::core::getValidPrimName(std::string(name));

        // Returns a temporary buffer for each stage (thread-safe).
        std::string& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, new_name);
        return buff.c_str();
    }

    USD_EXPORTER_REVIT_API const char** usd_exporter_revit_core_getValidPrimNames(const long int stage_id, const char** names, int namesCount, const char** reservedNames, int reservedNamesCount, int* returnCount)
    {
        if (returnCount != nullptr)
        {
            *returnCount = 0;
        }

        if (names == nullptr || namesCount <= 0)
        {
            return nullptr;
        }

        std::vector<std::string> nameVec;
        nameVec.reserve(namesCount);
        for (int i = 0; i < namesCount; ++i)
        {
            nameVec.push_back((names[i] != nullptr) ? std::string(names[i]) : std::string());
        }

        std::vector<std::string> reservedVec;
        if (reservedNames != nullptr && reservedNamesCount > 0)
        {
            reservedVec.reserve(reservedNamesCount);
            for (int i = 0; i < reservedNamesCount; ++i)
            {
                reservedVec.push_back((reservedNames[i] != nullptr) ? std::string(reservedNames[i]) : std::string());
            }
        }

        const std::vector<std::string> validNames = usd::exporter::revit::core::getValidPrimNames(nameVec, reservedVec);

        // Returns a temporary buffer for each stage (thread-safe). The buffer owns the strings and the array of pointers.
        std::vector<const char*>& buff = usd::exporter::revit::core::stageCache.getTempData(stage_id, validNames);

        if (returnCount != nullptr)
        {
            *returnCount = static_cast<int>(buff.size());
        }
        return buff.data();
    }
}

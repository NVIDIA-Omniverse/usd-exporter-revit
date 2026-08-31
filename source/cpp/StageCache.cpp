// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "StageCache.h"

#include "StringUtil.h"

#include <pxr/usd/usd/editTarget.h>
#include <pxr/usd/usd/stageCache.h>
#include <pxr/usd/usd/stageCacheContext.h>
#include <pxr/usd/usdUtils/stageCache.h>

using namespace usd::exporter::revit::core;
usd::exporter::revit::core::StageCache usd::exporter::revit::core::stageCache;

// ---------------------------------------------------.
CacheTransformData::CacheTransformData() : translation(0), pivot(0), rotation(0), rotationOrder(usd::exporter::revit::core::RotationOrder_eXyz), scale(1)
{
}

void CacheTransformData::copyData(const CacheTransformData& d)
{
    translation = d.translation;
    pivot = d.pivot;
    rotation = d.rotation;
    rotationOrder = d.rotationOrder;
    scale = d.scale;
}

CacheTransformData::CacheTransformData(const CacheTransformData& d)
{
    copyData(d);
}
CacheTransformData& CacheTransformData::operator=(const CacheTransformData& d)
{
    copyData(d);
    return *this;
}

CacheTransformData::~CacheTransformData()
{
}

CacheStringArrayData::CacheStringArrayData()
{
}
CacheStringArrayData::CacheStringArrayData(const std::vector<std::string>& data)
{
    strings = data;
    storePointers();
}

void CacheStringArrayData::storePointers()
{
    stringPointers.clear();
    if (!strings.empty())
    {
        // Create an array of string pointers.
        stringPointers.resize(strings.size());
        for (size_t i = 0; i < strings.size(); ++i)
        {
            stringPointers[i] = strings[i].c_str();
        }
    }
}

CacheStringArrayData::CacheStringArrayData(const CacheStringArrayData& d)
{
    strings = d.strings;
    storePointers();
}
CacheStringArrayData& CacheStringArrayData::operator=(const CacheStringArrayData& d)
{
    strings = d.strings;
    storePointers();
    return *this;
}

CacheStringArrayData::~CacheStringArrayData()
{
}

// ---------------------------------------------------.
StageCache::StageCache()
{
}

void StageCache::clear()
{
    std::lock_guard<std::mutex> lock(_mutex);

    pxr::UsdStageCache& _stageCache = pxr::UsdUtilsStageCache::Get();
    pxr::UsdStageCacheContext context(_stageCache);
    _stageCache.Clear();
    _tempStageData.clear();
}

long int StageCache::add(pxr::UsdStagePtr stage)
{
    pxr::UsdStageCache& _stageCache = pxr::UsdUtilsStageCache::Get();
    pxr::UsdStageCacheContext context(_stageCache);

    // At this time, the stage is retrieved from the cache.
    // https://openusd.org/release/api/class_usd_stage_cache_context.html#details
    if (stage == nullptr)
    {
        return -1;
    }

    if (!_stageCache.Contains(stage))
    {
        _stageCache.Insert(stage);
    }

    return _stageCache.GetId(stage).ToLongInt();
}

bool StageCache::remove(const long int stage_id)
{
    pxr::UsdStageCache& _stageCache = pxr::UsdUtilsStageCache::Get();
    pxr::UsdStageCacheContext context(_stageCache);

    const pxr::UsdStageCache::Id ID = pxr::UsdStageCache::Id::FromLongInt(stage_id);
    if (!_stageCache.Contains(ID))
    {
        return false;
    }

    removeTempData(stage_id);
    _stageCache.Erase(ID);
    return true;
}

pxr::UsdStagePtr StageCache::findStageFromId(const long int stage_id)
{
    pxr::UsdStageCache& _stageCache = pxr::UsdUtilsStageCache::Get();
    pxr::UsdStageCacheContext context(_stageCache);
    const pxr::UsdStageCache::Id ID = pxr::UsdStageCache::Id::FromLongInt(stage_id);
    return _stageCache.Find(ID);
}

void StageCache::removeTempData(const long int stage_id)
{
    std::lock_guard<std::mutex> lock(_mutex);
    _tempStageData.erase(stage_id);
}

std::string& StageCache::getTempData(const long int stage_id, const std::string& data)
{
    std::lock_guard<std::mutex> lock(_mutex);
    _tempStageData[stage_id] = data;
    return std::any_cast<std::string&>(_tempStageData[stage_id]);
}

pxr::UsdAttribute& StageCache::getTempData(const long int stage_id, const pxr::UsdAttribute& data)
{
    std::lock_guard<std::mutex> lock(_mutex);
    _tempStageData[stage_id] = data;
    return std::any_cast<pxr::UsdAttribute&>(_tempStageData[stage_id]);
}

pxr::GfMatrix4d& StageCache::getTempData(const long int stage_id, const pxr::GfMatrix4d& data)
{
    std::lock_guard<std::mutex> lock(_mutex);
    _tempStageData[stage_id] = data;
    return std::any_cast<pxr::GfMatrix4d&>(_tempStageData[stage_id]);
}

CacheTransformData& StageCache::getTempData(const long int stage_id, const CacheTransformData& data)
{
    std::lock_guard<std::mutex> lock(_mutex);
    _tempStageData[stage_id] = data;
    return std::any_cast<CacheTransformData&>(_tempStageData[stage_id]);
}

std::vector<const char*>& StageCache::getTempData(const long int stage_id, const std::vector<std::string>& data)
{
    std::lock_guard<std::mutex> lock(_mutex);
    CacheStringArrayData strArrayData(data);
    _tempStageData[stage_id] = strArrayData;
    return std::any_cast<CacheStringArrayData&>(_tempStageData[stage_id]).stringPointers;
}

// ---------------------------------------------------------.

extern "C"
{
    USD_EXPORTER_REVIT_API long int usd_exporter_revit_stage_cache_find_stage(pxr::UsdStagePtr stage)
    {
        return usd::exporter::revit::core::stageCache.add(stage);
    }

    USD_EXPORTER_REVIT_API pxr::UsdStage* usd_exporter_revit_stage_cache_find_stage_from_id(const long int stage_id)
    {
        pxr::UsdStagePtr stage = usd::exporter::revit::core::stageCache.findStageFromId(stage_id);
        if (stage == nullptr)
        {
            return nullptr;
        }
        pxr::UsdStage* stage_ptr = &(*stage);
        return stage_ptr;
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_stage_cache_evict_stage(const long int stage_id)
    {
        usd::exporter::revit::core::stageCache.remove(stage_id);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_stage_cache_clear()
    {
        usd::exporter::revit::core::stageCache.clear();
    }
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#ifndef _CONNECT_STAGECACHE_H
#define _CONNECT_STAGECACHE_H

#include "ExportApi.h"
#include "Types.h"

#include <pxr/base/gf/matrix4d.h>
#include <pxr/usd/usd/attribute.h>
#include <pxr/usd/usd/stage.h>

#include <any>
#include <map>
#include <mutex>
#include <string>
#include <vector>

namespace revit::usd_export::core
{
/**
 * Buffer to be held in revit_usd_export_core_getLocalTransformComponents.
 * Used to keep the return value static for each stage when returning.
 */
class CacheTransformData
{
public:

    CacheTransformData();
    CacheTransformData(const CacheTransformData& d);
    CacheTransformData& operator=(const CacheTransformData& d);
    ~CacheTransformData();

private:

    void copyData(const CacheTransformData& d);

public:

    pxr::GfVec3d translation;
    pxr::GfVec3d pivot;
    pxr::GfVec3d rotation;
    revit::usd_export::core::RotationOrder rotationOrder;
    pxr::GfVec3d scale;
};

/**
 * Used in getValidPrimNames.
 * String Array Data.
 */
class CacheStringArrayData
{
public:

    CacheStringArrayData();
    CacheStringArrayData(const std::vector<std::string>& data);
    CacheStringArrayData(const CacheStringArrayData& d);
    CacheStringArrayData& operator=(const CacheStringArrayData& d);
    ~CacheStringArrayData();

private:

    void storePointers();

public:

    std::vector<std::string> strings;
    std::vector<const char*> stringPointers;
};

/**
 * Manage stage and uri maps.
 */
class StageCache
{
public:

    StageCache();

    /**
     * Clear cache.
     */
    void clear();

    /**
     * Add Stage.
     * @param[in] stage  Stage.
     * @ return  If successful, stage Id.
     */
    long int add(pxr::UsdStagePtr stage);

    /**
     * Remove stage and release its file handles.
     * @param[in] stage_id  Stage Id.
     */
    bool remove(const long int stage_id);

    /**
     * Returns a reference to Stage from stage id.
     * @param[in] stage_id  Stage Id.
     * @return Stage.
     */
    pxr::UsdStagePtr findStageFromId(const long int stage_id);

    /**
     * Temporary data for each stage.
     * This is used in the return value of an external function.
     * @param[in] stage_id     Stage Id.
     * @param[in] data         Target data.
     * @return Temporary data reference.
     */
    std::string& getTempData(const long int stage_id, const std::string& data);
    pxr::UsdAttribute& getTempData(const long int stage_id, const pxr::UsdAttribute& data);
    pxr::GfMatrix4d& getTempData(const long int stage_id, const pxr::GfMatrix4d& data);
    CacheTransformData& getTempData(const long int stage_id, const CacheTransformData& data);
    std::vector<const char*>& getTempData(const long int stage_id, const std::vector<std::string>& data);

private:

    /**
     * Remove temporary data.
     * @param[in] stage_id     Stage Id.
     */
    void removeTempData(const long int stage_id);

private:

    // Holds data of any type using stage_id as a key.
    std::map<long int, std::any> _tempStageData;

    std::mutex _mutex;
};

// This cache is shared throughout.
extern StageCache stageCache;
} // namespace revit::usd_export::core

extern "C"
{
    /**
     * Find stage to StageCache.
     * @param[in] stage  Stage.
     * @ return  If successful, stage Id.
     */
    REVIT_USD_EXPORT_API long int c_connect_findStage(pxr::UsdStagePtr stage);

    /**
     * If a stage with the specified URI exists, it is returned.
     * @param[in] stage_id     Stage Id.
     * @return Stage pointer(pxr::UsdStagePtr).
     */
    REVIT_USD_EXPORT_API pxr::UsdStage* c_connect_findStageFromId(const long int stage_id);

    /**
     * Remove stage from cache.
     * @param[in] stage_id     Stage Id.
     */
    REVIT_USD_EXPORT_API void c_connect_evict_stage(const long int stage_id);

    /**
     * Clear stage cache.
     */
    REVIT_USD_EXPORT_API void c_connect_clear_stage_cache();
}

#endif

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <pxr/usd/usd/stage.h>

namespace revit::usd_export::core
{
//! Utility functions to manipulate `UsdStage`
//!
//! @{

//! Create and configure a `UsdStage`
//!
//! See `configureStage` for more details.
//!
//! Important: The extension of the `identifier` must be associated with a file format that supports editing.
//!
//! @param identifier The identifier to be used for the root layer of this stage.
//! @param defaultPrimName Name of the default root prim.
//! @param upAxis The up axis for all the geometry contained in the stage.
//! @param linearUnits The meters per unit for all linear measurements in the stage.
//! @note Mass units are authored as kilogramsPerUnit = 1 (UsdPhysicsMassUnits::kilograms).
//! @param fileFormatArgs Additional file format-specific arguments to be supplied during Stage creation.
//! @returns The newly created stage or a null pointer.
REVIT_USD_EXPORT_API pxr::UsdStageRefPtr createStage(
    const std::string& identifier,
    const std::string& defaultPrimName,
    const pxr::TfToken& upAxis,
    const double linearUnits,
    const pxr::SdfLayer::FileFormatArguments& fileFormatArgs = pxr::SdfLayer::FileFormatArguments()
);

//! Save the given `UsdStage` with an optional checkpoint comment
//!
//! Save and add commented checkpoints on all dirty session layers and sublayers of session layers contributing
//! to this stage.
//!
//! All dirty layers will be annotated with authoring metadata, unless previously annotated. This is to preserve
//! authoring metadata on referenced layers that came from other applications. See @ref layers for more details
//! on `setLayerAuthoringMetadata`.
//!
//! @param stage The stage to be saved.
//! @param comment Optional save comment recorded in the log.
REVIT_USD_EXPORT_API void saveStage(pxr::UsdStagePtr stage, const char* comment = nullptr);

//! Convert meters per unit for the given `UsdStage`
//!
//! Sets the metersPerUnit attribute for the stage and scales vertices, translations, and pivots
//! for all prims in the stage accordingly.
//!
//! @param stage The stage to convert.
//! @param metersPerUnit The new meters per unit value.
REVIT_USD_EXPORT_API bool convertMetersPerUnit(pxr::UsdStagePtr stage, const double metersPerUnit);

//! Get the meters per unit from a USD file
//!
//! Opens the local USD file at the given path and reads its metersPerUnit metadata.
//!
//! @param filePath The local path to the USD file.
//! @return The metersPerUnit value, or -1.0 if the file cannot be opened, is not local, or doesn't have the metadata.
REVIT_USD_EXPORT_API double getMetersPerUnitFromFile(const std::string& filePath);


} // namespace revit::usd_export::core

extern "C"
{
    /**
     * Create and configure a `UsdStage`.
     * @param[in] identifier       The identifier to be used for the root layer of this stage.
     * @param[in] defaultPrimName  Name of the default root prim.
     * @param[in] upAxis           The up axis for all the geometry contained in the stage.
     * @param[in] linearUnits      The meters per unit for all linear measurements in the stage.
     * @return The newly created stage id.
     */
    REVIT_USD_EXPORT_API long int revit_usd_export_core_createStage(const char* identifier, const char* defaultPrimName, char* upAxis, const double linearUnits);

    /**
     * Configure a stage so that the defining metadata is explicitly authored.
     * @param[in] stage_id     Stage Id.
     * @param[in] comment      Optional save comment recorded in the log.
     */
    REVIT_USD_EXPORT_API void revit_usd_export_core_saveStage(const long int stage_id, const char* commit);

    /**
     * Convert meters per unit for the given stage.
     * @param[in] stage_id     Stage Id.
     * @param[in] metersPerUnit The new meters per unit value.
     * @return true if successful, false otherwise.
     */
    REVIT_USD_EXPORT_API bool revit_usd_export_core_convertMetersPerUnit(const long int stage_id, const double metersPerUnit);

    /**
     * Get the meters per unit from a USD file.
     * @param[in] filePath     The local path to the USD file.
     * @return The metersPerUnit value, or -1.0 if the it cannot be determined.
     */
    REVIT_USD_EXPORT_API double revit_usd_export_core_getMetersPerUnitFromFile(const char* filePath);
}

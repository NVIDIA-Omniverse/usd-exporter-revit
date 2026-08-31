// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include "ExportApi.h"

#include <pxr/usd/sdf/layer.h>

namespace usd::exporter::revit::core
{
//! Utility functions to manipulate `SdfLayers`
//!
//! @{

//! Check if the `SdfLayer` has metadata indicating the provenance of the data.
//!
//! Important: this metadata is strictly informational, it is not advisable to key runtime behavior off of this metadata.
//!
//! @param layer The layer to check
USD_EXPORTER_REVIT_API bool hasLayerAuthoringMetadata(pxr::SdfLayerHandle layer);


//! Set metadata on the `SdfLayer` indicating the provenance of the data.
//!
//! Important: this metadata is strictly informational, it is not advisable to key runtime behavior off of this metadata.
//!
//! This will add information to the layer that can be used to track it back to its product of origin.
//! The mandatory settings `app.name`, `app.version`, `usd.exporter.revit.core.client.name`, and
//! `usd.exporter.revit.core.client.version` are used to format the metadata.
//!
//! Note `startup()` must be called before calling this function.
//!
//! @param layer The layer to modify
USD_EXPORTER_REVIT_API void setLayerAuthoringMetadata(pxr::SdfLayerHandle layer);

} // namespace usd::exporter::revit::core

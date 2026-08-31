// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include <pxr/base/tf/token.h>
#include <pxr/usd/sdf/path.h>
#include <pxr/usd/usd/object.h>
#include <pxr/usd/usd/prim.h>
#include <pxr/usd/usd/stage.h>

#include <string>


namespace usd::exporter::revit::core::detail
{

//! Validate that prim opinions could be authored at this path on the stage
//!
//! This validates that the `stage` and `path` are valid, and that the path is absolute.
//! If a prim already exists at the given path it must not be an instance proxy.
//!
//! If the location is invalid and `reason` is non-null, an error message describing the validation error will be set.
//!
//! @param stage The Stage to consider.
//! @param path The Path to consider.
//! @param reason The output message for failed validation.
//! @returns True if the location is valid, or false otherwise.
bool isEditablePrimLocation(const pxr::UsdStagePtr stage, const pxr::SdfPath& path, std::string* reason);

//! Validate that prim opinions could be authored for a child prim with the given name
//!
//! This validates that the `prim` is valid, and that the name is a valid identifier.
//! If a prim already exists at the given path it must not be an instance proxy.
//!
//! If the location is invalid and `reason` is non-null, an error message describing the validation error will be set.
//!
//! @param prim The UsdPrim which would be the parent of the proposed location.
//! @param name The name which would be used for the UsdPrim at the proposed location.
//! @param reason The output message for failed validation.
//! @returns True if the location is valid, or false otherwise.
bool isEditablePrimLocation(const pxr::UsdPrim& prim, const std::string& name, std::string* reason);

} // namespace usd::exporter::revit::core::detail

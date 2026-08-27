// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

namespace revit::usd_export::core
{
enum RotationOrder
{
    RotationOrder_eXyz = 0,
    RotationOrder_eXzy = 1,
    RotationOrder_eYxz = 2,
    RotationOrder_eYzx = 3,
    RotationOrder_eZxy = 4,
    RotationOrder_eZyx = 5
};

enum Kind
{
    Kind_eAssembly = 0,
    Kind_eComponent = 1,
    Kind_eGroup = 2,
    Kind_eModel = 3,
    Kind_eSubcomponent = 4
};

enum ColorSpace
{
    ColorSpace_eAuto = 0,
    ColorSpace_eRaw = 1,
    ColorSpace_eSrgb = 2
};
} // namespace revit::usd_export::core

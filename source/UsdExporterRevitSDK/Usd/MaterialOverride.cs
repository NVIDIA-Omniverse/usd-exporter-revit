// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class MaterialOverride
{
    public string PrimPath;
    public string MaterialPath;

    public MaterialOverride(string primPath, string materialPath)
    {
        PrimPath = primPath;
        MaterialPath = materialPath;
    }
}
}

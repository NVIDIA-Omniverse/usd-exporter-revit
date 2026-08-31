// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace pxr.usd
{
public class stage
{
    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_stageSetVariantEditTarget", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool pxr_usd_stageSetVariantEditTarget(long stage_id, byte[] prim_path, byte[] set_name, byte[] option_name);

    public static bool setVariantEditTarget(long stage_id, string prim_path, string set_name, string option_name)
    {
        return pxr_usd_stageSetVariantEditTarget(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(set_name), Encoding.UTF8.GetBytes(option_name));
    }

    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_stageSetRootEditTarget", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_stageSetRootEditTarget(long stage_id);

    public static void setEditTargetToRoot(long stage_id)
    {
        pxr_usd_stageSetRootEditTarget(stage_id);
    }
}
}

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
public class variantSet
{
    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_addVariantSet", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_addVariantSet(long stage_id, byte[] prim_path, byte[] set_name);

    public static void addSetToPrim(long stage_id, string prim_path, string set_name)
    {
        pxr_usd_addVariantSet(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(set_name));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_addVariantOption", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_addVariantOption(long stage_id, byte[] prim_path, byte[] set_name, byte[] option_name);

    public static void addOptionToSet(long stage_id, string prim_path, string set_name, string option_name)
    {
        pxr_usd_addVariantOption(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(set_name), Encoding.UTF8.GetBytes(option_name));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setVariantSelection", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_setVariantSelection(long stage_id, byte[] prim_path, byte[] set_name, byte[] option_name);

    public static void setSelection(long stage_id, string prim_path, string set_name, string option_name)
    {
        pxr_usd_setVariantSelection(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(set_name), Encoding.UTF8.GetBytes(option_name));
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using revit.usd.export;

namespace pxr.usd.usdGeom
{
public class cylinder
{
    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_defineCylinder", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pxr_usd_defineCylinder(long stage_id, byte[] parent, byte[] name, GfVec3f start, GfVec3f end, double radius);

    public static string define(long stage_id, string parent_path, string name, GfVec3f start, GfVec3f end, double radius)
    {
        IntPtr intPtr = pxr_usd_defineCylinder(stage_id, Encoding.UTF8.GetBytes(parent_path), Encoding.UTF8.GetBytes(name), start, end, radius);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }
}
public class scope
{
    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_defineScope", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pxr_usd_defineScope(long stage_id, byte[] parent, byte[] name);

    public static string define(long stage_id, string parent_path, string name)
    {
        IntPtr intPtr = pxr_usd_defineScope(stage_id, Encoding.UTF8.GetBytes(parent_path), Encoding.UTF8.GetBytes(name));
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace pxr.usd.usdLux
{
public class cylinderLight
{
    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_defineCylinderLight", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pxr_usd_defineCylinderLight(long stage_id, byte[] parent, byte[] name, float length, float radius, float intensity);

    public static string define(long stage_id, string parent_path, string name, float length, float radius, float intensity)
    {
        IntPtr intPtr = pxr_usd_defineCylinderLight(stage_id, Encoding.UTF8.GetBytes(parent_path), Encoding.UTF8.GetBytes(name), length, radius, intensity);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }
}

public class diskLight
{
    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_defineDiskLight", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pxr_usd_defineDiskLight(long stage_id, byte[] parent, byte[] name, float radius, float intensity);

    public static string define(long stage_id, string parent_path, string name, float radius, float intensity)
    {
        IntPtr intPtr = pxr_usd_defineDiskLight(stage_id, Encoding.UTF8.GetBytes(parent_path), Encoding.UTF8.GetBytes(name), radius, intensity);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }
}

public class sphereLight
{
    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_defineSphereLight", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pxr_usd_defineSphereLight(long stage_id, byte[] parent, byte[] name, float radius, float intensity);

    public static string define(long stage_id, string parent_path, string name, float radius, float intensity)
    {
        IntPtr intPtr = pxr_usd_defineSphereLight(stage_id, Encoding.UTF8.GetBytes(parent_path), Encoding.UTF8.GetBytes(name), radius, intensity);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }
}

public class shapingApi
{
    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_createLuxShapingApiIesFileAttr", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_createLuxShapingApiIesFileAttr(long stage_id, byte[] light_path, byte[] file_path);

    public static void createIesFileAttr(long stage_id, string light_path, string file_path)
    {
        pxr_usd_createLuxShapingApiIesFileAttr(stage_id, Encoding.UTF8.GetBytes(light_path), Encoding.UTF8.GetBytes(file_path));
    }

    [DllImport("usd_exporter_revit", EntryPoint = "pxr_usd_createLuxShapingApiIesFileAttrAtTime", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_createLuxShapingApiIesFileAttrAtTime(long stage_id, byte[] light_path, byte[] file_path, double time);

    public static void createIesFileAttrAtTime(long stage_id, string light_path, string file_path, double time = 0.0)
    {
        pxr_usd_createLuxShapingApiIesFileAttrAtTime(stage_id, Encoding.UTF8.GetBytes(light_path), Encoding.UTF8.GetBytes(file_path), time);
    }
}
}

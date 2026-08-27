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
public enum Kind
{
    eAssembly = 0,
    eComponent = 1,
    eGroup = 2,
    eModel = 3,
    eSubcomponent = 4
}
;
public class classPrim
{
    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_defineClass", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pxr_usd_defineClass(long stage_id, byte[] parent, byte[] name);

    public static string define(long stage_id, string parent_path, string name)
    {
        IntPtr intPtr = pxr_usd_defineClass(stage_id, Encoding.UTF8.GetBytes(parent_path), Encoding.UTF8.GetBytes(name));
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }
}
public class prim
{
    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setInstanceable", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_setInstanceable(long stage_id, byte[] prim_path, bool value);

    public static void setInstanceable(long stage_id, string prim_path, bool value)
    {
        pxr_usd_setInstanceable(stage_id, Encoding.UTF8.GetBytes(prim_path), value);
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setVisibility", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_setVisibility(long stage_id, byte[] prim_path, bool value);

    public static void setVisibility(long stage_id, string prim_path, bool value)
    {
        pxr_usd_setVisibility(stage_id, Encoding.UTF8.GetBytes(prim_path), value);
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setDoNotCastShadows", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_setDoNotCastShadows(long stage_id, byte[] prim_path, bool value);

    public static void setDoNotCastShadows(long stage_id, string prim_path, bool value)
    {
        pxr_usd_setDoNotCastShadows(stage_id, Encoding.UTF8.GetBytes(prim_path), value);
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setKind", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_setKind(long stage_id, byte[] prim_path, Kind kind);

    public static void setKind(long stage_id, string prim_path, Kind kind)
    {
        pxr_usd_setKind(stage_id, Encoding.UTF8.GetBytes(prim_path), kind);
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_createStringAttribute", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_createStringAttribute(long stage_id, byte[] prim_path, byte[] name, byte[] value);

    public static void createStringAttribute(long stage_id, string prim_path, string name, string value)
    {
        pxr_usd_createStringAttribute(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(name), Encoding.UTF8.GetBytes(value));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setAttributeDisplayName", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_setAttributeDisplayName(long stage_id, byte[] prim_path, byte[] attr_name, byte[] display_name);

    public static void setAttributeDisplayName(long stage_id, string prim_path, string attr_name, string display_name)
    {
        pxr_usd_setAttributeDisplayName(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(attr_name), Encoding.UTF8.GetBytes(display_name));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_addPayload", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_addPayload(long stage_id, byte[] prim_path, byte[] payload_path);

    public static void addPayload(long stage_id, string prim_path, string payload_path)
    {
        pxr_usd_addPayload(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(payload_path));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_addReference", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_addReference(long stage_id, byte[] prim_path, byte[] reference_path);

    public static void addReference(long stage_id, string prim_path, string reference_path)
    {
        pxr_usd_addReference(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(reference_path));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_addInternalReference", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pxr_usd_addInternalReference(long stage_id, byte[] prim_path, byte[] reference_path);

    public static void addInternalReference(long stage_id, string prim_path, string reference_path)
    {
        pxr_usd_addInternalReference(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(reference_path));
    }

    [DllImport("revit_usd_export", EntryPoint = "pxr_usd_setPrimToOver", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)] // NOSONAR
    private static extern void pxr_usd_setPrimToOver(long stage_id, byte[] path);
    public static void setPrimToOver(long stage_id, string path)
    {
        pxr_usd_setPrimToOver(stage_id, Encoding.UTF8.GetBytes(path));
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace usd.exporter.revit
{
[StructLayout(LayoutKind.Sequential)]
public struct GfVec4f
{
    public float x, y, z, w;

    public GfVec4f(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
};

[StructLayout(LayoutKind.Sequential)]
public struct GfVec3f
{
    public float x, y, z;

    public GfVec3f(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
};

[StructLayout(LayoutKind.Sequential)]
public struct GfVec2f
{
    public float x, y;

    public GfVec2f(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
};

[StructLayout(LayoutKind.Sequential)]
public struct GfVec4d
{
    public double x, y, z, w;

    public GfVec4d(double x, double y, double z, double w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
};

[StructLayout(LayoutKind.Sequential)]
public struct GfVec3d
{
    public double x, y, z;

    public GfVec3d(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
};

[StructLayout(LayoutKind.Sequential)]
public struct GfVec2d
{
    public double x, y;

    public GfVec2d(double x, double y)
    {
        this.x = x;
        this.y = y;
    }
};

public enum RotationOrder
{
    eXyz = 0,
    eXzy = 1,
    eYxz = 2,
    eYzx = 3,
    eZxy = 4,
    eZyx = 5
}
;

public enum ColorSpace
{
    eAuto = 0,
    eRaw = 1,
    eSrgb = 2
}

public class core
{
    // -------------------------------------------------------
    // External Functions Mapping.

    // Stage cache.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_stage_cache_evict_stage", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_stage_cache_evict_stage(long stage_id);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_stage_cache_clear", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_stage_cache_clear();

    // Core.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_startup", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_startup();

    // Log.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_startupLog", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_startupLog();

    // CameraAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_defineCamera", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_defineCamera(long stage_id, byte[] prim_path, IntPtr cameraData);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_defineCameraEx", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_defineCameraEx(
        long stage_id,
        byte[] prim_path,
        double[,] transform,
        bool perspective,
        float horizontalAperture,
        float verticalAperture,
        float horizontalApertureOffset,
        float verticalApertureOffset,
        float focalLength,
        float clippingRangeNear,
        float clippingRangeFar,
        float fStop,
        float focusDistance
    );

    // LightAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_defineRectLight", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_defineRectLight(long stage_id, byte[] prim_path, float width, float height, float intensity, byte[] texturePath);

    // LightCompatibility.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createEnableColorTemperatureAttr", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_createEnableColorTemperatureAttr(long stage_id, byte[] prim_path, bool value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createColorTemperatureAttr", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_createColorTemperatureAttr(long stage_id, byte[] prim_path, float value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createLightExtentAttr", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_createLightExtentAttr(long stage_id, byte[] prim_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createIntensityAttr", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_createIntensityAttr(long stage_id, byte[] prim_path, float value);

    // MaterialAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_sRgbToLinear", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_sRgbToLinear(GfVec3f color);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_createMaterial(long stage_id, byte[] parent, byte[] name);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShader", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_createMdlShader(long stage_id, byte[] prim_path, byte[] name, byte[] mdlPath, byte[] module, bool connectMaterialOutputs);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShaderInputAsset", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_createMdlShaderInputAsset(long stage_id, byte[] material_path, byte[] input_name, byte[] value, ColorSpace color_space);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShaderInputBool", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_createMdlShaderInputBool(long stage_id, byte[] material_path, byte[] input_name, bool value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShaderInputInt", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_createMdlShaderInputInt(long stage_id, byte[] material_path, byte[] input_name, int value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShaderInputFloat", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_createMdlShaderInputFloat(long stage_id, byte[] material_path, byte[] input_name, float value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShaderInputFloat2", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_createMdlShaderInputFloat2(long stage_id, byte[] material_path, byte[] input_name, GfVec2f value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createMdlShaderInputColor3f", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_createMdlShaderInputColor3f(long stage_id, byte[] material_path, byte[] input_name, GfVec3f value);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_bindMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_bindMaterial(long stage_id, byte[] prim_path, byte[] material_prim_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_defineOmniPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_defineOmniPbrMaterial(long stage_id, byte[] prim_path, GfVec3f color, float opacity, float roughness, float metallic);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addEmissiveColorToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addEmissiveColorToPbrMaterial(long stage_id, byte[] material_path, GfVec3f color, float intensity);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addDiffuseTextureToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addDiffuseTextureToPbrMaterial(long stage_id, byte[] material_path, byte[] texture_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addNormalTextureToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addNormalTextureToPbrMaterial(long stage_id, byte[] material_path, byte[] texture_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addOrmTextureToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addOrmTextureToPbrMaterial(long stage_id, byte[] material_path, byte[] texture_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addRoughnessTextureToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addRoughnessTextureToPbrMaterial(long stage_id, byte[] material_path, byte[] texture_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addMetallicTextureToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addMetallicTextureToPbrMaterial(long stage_id, byte[] material_path, byte[] texture_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_addOpacityTextureToPbrMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_addOpacityTextureToPbrMaterial(long stage_id, byte[] material_path, byte[] texture_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_defineOmniGlassMaterial", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_defineOmniGlassMaterial(long stage_id, byte[] prim_path, GfVec3f color, float indexOfRefraction, float roughness);

    // MeshAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_definePolyMesh", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_definePolyMesh(
        long stage_id,
        byte[] prim_path,
        int[] faceVertexCounts,
        int faceVertexCountsCount,
        int[] faceVertexIndices,
        int faceVertexIndicesCount,
        GfVec3f[] points,
        int pointsCount,
        byte[] normalsInterporation,
        GfVec3f[] normals,
        int normalsCount,
        int[] normalsIndices,
        int normalsIndicesCount,
        byte[] uvsInterporation,
        GfVec2f[] uvs,
        int uvsCount,
        int[] uvsIndices,
        int uvsIndicesCount,
        byte[] displayColorInterporation,
        GfVec3f[] displayColor,
        int displayColorCount,
        int[] displayColorIndices,
        int displayColorIndicesCount,
        byte[] displayOpacityInterporation,
        float[] displayOpacity,
        int displayOpacityCount,
        int[] displayOpacityIndices,
        int displayOpacityIndicesCount
    );

    // PrimAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_getDisplayName", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_getDisplayName(long stage_id, byte[] prim_path);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_setDisplayName", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_setDisplayName(long stage_id, byte[] prim_path, byte[] name);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_getValidPrimName", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_getValidPrimName(long stage_id, byte[] name);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_getValidPrimNames", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_getValidPrimNames(long stage_id, IntPtr names, int namesCount, IntPtr reservedNames, int reservedNamesCount, out int returnCount);

    // StageAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_createStage", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern long usd_exporter_revit_core_createStage(byte[] identifier, byte[] defaultPrimName, byte[] upAxis, double linearUnits);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_saveStage", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_saveStage(long stage_id, byte[] commit);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_convertMetersPerUnit", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_convertMetersPerUnit(long stage_id, double metersPerUnit);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_getMetersPerUnitFromFile", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern double usd_exporter_revit_core_getMetersPerUnitFromFile(byte[] filePath);

    // XformAlgo.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_setLocalTransform", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_setLocalTransform(long stage_id, byte[] prim_path, double[,] transform);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_setLocalTransformPivot", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)] // NOSONAR
    public static extern bool usd_exporter_revit_core_setLocalTransformPivot(long stage_id, byte[] prim_path, double[,] transform, GfVec3d pivot);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_setLocalTransformMatrix", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_core_setLocalTransformMatrix(long stage_id, byte[] prim_path, double[,] transform);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_getLocalTransformComponents", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_core_getLocalTransformComponents(long stage_id, byte[] prim_path, out IntPtr translation, out IntPtr pivot, out IntPtr rotation, out RotationOrder rotationOrder, out IntPtr scale);

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_core_defineXform", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr usd_exporter_revit_core_defineXform(long stage_id, byte[] prim_path);

    // Linear units.
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_getGeomLinearUnits", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)] // NOSONAR
    private static extern double usd_exporter_revit_getGeomLinearUnits(byte[] name);

    // -------------------------------------------------------

    public core()
    {
    }

    // Stage cache.
    public static void evictStage(long stage_id)
    {
        usd_exporter_revit_stage_cache_evict_stage(stage_id);
    }

    public static void clearStageCache()
    {
        usd_exporter_revit_stage_cache_clear();
    }

    // Core.
    public static bool startup()
    {
        return usd_exporter_revit_core_startup();
    }

    // Log.

    public static void startupLog()
    {
        usd_exporter_revit_core_startupLog();
    }

    // CameraAlgo.
    public static string defineCamera(long stage_id, string prim_path, IntPtr cameraData)
    {
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(usd_exporter_revit_core_defineCamera(stage_id, Encoding.UTF8.GetBytes(prim_path), cameraData));
    }

    public static string defineCameraEx(
        long stage_id,
        string prim_path,
        double[,] transform,
        bool perspective = true,
        float horizontalAperture = 20.955f,
        float verticalAperture = 15.2908f,
        float horizontalApertureOffset = 0.0f,
        float verticalApertureOffset = 0.0f,
        float focalLength = 50.0f,
        float clippingRangeNear = 1.0f,
        float clippingRangeFar = 1000000.0f,
        float fStop = 0.0f,
        float focusDistance = 0.0f
    )
    {
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(usd_exporter_revit_core_defineCameraEx(
            stage_id,
            Encoding.UTF8.GetBytes(prim_path),
            transform,
            perspective,
            horizontalAperture,
            verticalAperture,
            horizontalApertureOffset,
            verticalApertureOffset,
            focalLength,
            clippingRangeNear,
            clippingRangeFar,
            fStop,
            focusDistance
        ));
    }

    // LightAlgo.
    public static string defineRectLight(long stage_id, string prim_path, float width, float height, float intensity, string texturePath = "")
    {
        IntPtr intPtr = usd_exporter_revit_core_defineRectLight(stage_id, Encoding.UTF8.GetBytes(prim_path), width, height, intensity, (texturePath == "") ? null : Encoding.UTF8.GetBytes(texturePath));
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    // LightCompatibility.
    public static void createEnableColorTemperatureAttr(long stage_id, string prim_path, bool value)
    {
        usd_exporter_revit_core_createEnableColorTemperatureAttr(stage_id, Encoding.UTF8.GetBytes(prim_path), value);
    }

    public static void createColorTemperatureAttr(long stage_id, string prim_path, float value)
    {
        usd_exporter_revit_core_createColorTemperatureAttr(stage_id, Encoding.UTF8.GetBytes(prim_path), value);
    }

    public static void createLightExtentAttr(long stage_id, string prim_path)
    {
        usd_exporter_revit_core_createLightExtentAttr(stage_id, Encoding.UTF8.GetBytes(prim_path));
    }

    public static void createIntensityAttr(long stage_id, string prim_path, float value)
    {
        usd_exporter_revit_core_createIntensityAttr(stage_id, Encoding.UTF8.GetBytes(prim_path), value);
    }

    // MaterialAlgo.
    public static GfVec3f sRgbToLinear(GfVec3f color)
    {
        IntPtr intPtr = usd_exporter_revit_core_sRgbToLinear(color);
        GfVec3f col = (GfVec3f)Marshal.PtrToStructure(intPtr, typeof(GfVec3f));
        return col;
    }

    public static string createMaterial(long stage_id, string parent, string name)
    {
        IntPtr intPtr = usd_exporter_revit_core_createMaterial(stage_id, Encoding.UTF8.GetBytes(parent), Encoding.UTF8.GetBytes(name));
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    public static string createMdlShader(long stage_id, string prim_path, string name, string mdlPath, string module, bool connectMaterialOutputs = true)
    {
        IntPtr intPtr = usd_exporter_revit_core_createMdlShader(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(name), Encoding.UTF8.GetBytes(mdlPath), Encoding.UTF8.GetBytes(module), connectMaterialOutputs);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    public static bool createMdlShaderInputAsset(long stage_id, string material_path, string input_name, string value, ColorSpace color_space)
    {
        return usd_exporter_revit_core_createMdlShaderInputAsset(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(input_name), Encoding.UTF8.GetBytes(value), color_space);
    }

    public static bool createMdlShaderInputBool(long stage_id, string material_path, string input_name, bool value)
    {
        return usd_exporter_revit_core_createMdlShaderInputBool(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(input_name), value);
    }

    public static bool createMdlShaderInputInt(long stage_id, string material_path, string input_name, int value)
    {
        return usd_exporter_revit_core_createMdlShaderInputInt(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(input_name), value);
    }

    public static bool createMdlShaderInputFloat(long stage_id, string material_path, string input_name, float value)
    {
        return usd_exporter_revit_core_createMdlShaderInputFloat(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(input_name), value);
    }

    public static bool createMdlShaderInputFloat2(long stage_id, string material_path, string input_name, GfVec2f value)
    {
        return usd_exporter_revit_core_createMdlShaderInputFloat2(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(input_name), value);
    }

    public static bool createMdlShaderInputColor3f(long stage_id, string material_path, string input_name, GfVec3f value)
    {
        return usd_exporter_revit_core_createMdlShaderInputColor3f(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(input_name), value);
    }

    public static void bindMaterial(long stage_id, string prim_path, string material_prim_path)
    {
        usd_exporter_revit_core_bindMaterial(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(material_prim_path));
    }

    public static string defineOmniPbrMaterial(long stage_id, string prim_path, GfVec3f color, float opacity = 1.0f, float roughness = 0.5f, float metallic = 0.0f)
    {
        IntPtr intPtr = usd_exporter_revit_core_defineOmniPbrMaterial(stage_id, Encoding.UTF8.GetBytes(prim_path), color, opacity, roughness, metallic);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    public static bool addEmissiveColorToPbrMaterial(long stage_id, string material_path, GfVec3f color, float intensity = 1000.0f)
    {
        return usd_exporter_revit_core_addEmissiveColorToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), color, intensity);
    }

    public static bool addDiffuseTextureToPbrMaterial(long stage_id, string material_path, string texture_path)
    {
        return usd_exporter_revit_core_addDiffuseTextureToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(texture_path));
    }

    public static bool addNormalTextureToPbrMaterial(long stage_id, string material_path, string texture_path)
    {
        return usd_exporter_revit_core_addNormalTextureToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(texture_path));
    }

    public static bool addOrmTextureToPbrMaterial(long stage_id, string material_path, string texture_path)
    {
        return usd_exporter_revit_core_addOrmTextureToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(texture_path));
    }

    public static bool addRoughnessTextureToPbrMaterial(long stage_id, string material_path, string texture_path)
    {
        return usd_exporter_revit_core_addRoughnessTextureToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(texture_path));
    }

    public static bool addMetallicTextureToPbrMaterial(long stage_id, string material_path, string texture_path)
    {
        return usd_exporter_revit_core_addMetallicTextureToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(texture_path));
    }

    public static bool addOpacityTextureToPbrMaterial(long stage_id, string material_path, string texture_path)
    {
        return usd_exporter_revit_core_addOpacityTextureToPbrMaterial(stage_id, Encoding.UTF8.GetBytes(material_path), Encoding.UTF8.GetBytes(texture_path));
    }

    public static string defineOmniGlassMaterial(long stage_id, string prim_path, GfVec3f color, float indexOfRefraction = 1.491f, float roughness = 0.02f)
    {
        IntPtr intPtr = usd_exporter_revit_core_defineOmniGlassMaterial(stage_id, Encoding.UTF8.GetBytes(prim_path), color, indexOfRefraction, roughness);
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    // MeshAlgo.
    public static string definePolyMesh(
        long stage_id,
        string prim_path,
        int[] faceVertexCounts,
        int[] faceVertexIndices,
        GfVec3f[] points,
        string normalsInterporation = "",
        GfVec3f[] normals = null,
        int[] normalsIndices = null,
        string uvsInterporation = "",
        GfVec2f[] uvs = null,
        int[] uvsIndices = null,
        string displayColorInterporation = "",
        GfVec3f[] displayColor = null,
        int[] displayColorIndices = null,
        string displayOpacityInterporation = "",
        float[] displayOpacity = null,
        int[] displayOpacityIndices = null
    )
    {
        IntPtr intPtr = usd_exporter_revit_core_definePolyMesh(
            stage_id,
            Encoding.UTF8.GetBytes(prim_path),
            faceVertexCounts,
            (faceVertexCounts != null) ? faceVertexCounts.Length : 0,
            faceVertexIndices,
            (faceVertexIndices != null) ? faceVertexIndices.Length : 0,
            points,
            (points != null) ? points.Length : 0,
            Encoding.UTF8.GetBytes(normalsInterporation),
            normals,
            (normals != null) ? normals.Length : 0,
            normalsIndices,
            (normalsIndices != null) ? normalsIndices.Length : 0,
            Encoding.UTF8.GetBytes(uvsInterporation),
            uvs,
            (uvs != null) ? uvs.Length : 0,
            uvsIndices,
            (uvsIndices != null) ? uvsIndices.Length : 0,
            Encoding.UTF8.GetBytes(displayColorInterporation),
            displayColor,
            (displayColor != null) ? displayColor.Length : 0,
            displayColorIndices,
            (displayColorIndices != null) ? displayColorIndices.Length : 0,
            Encoding.UTF8.GetBytes(displayOpacityInterporation),
            displayOpacity,
            (displayOpacity != null) ? displayOpacity.Length : 0,
            displayOpacityIndices,
            (displayOpacityIndices != null) ? displayOpacityIndices.Length : 0
        );
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    // PrimAlgo.
    public static string getDisplayName(long stage_id, string prim_path)
    {
        IntPtr intPtr = usd_exporter_revit_core_getDisplayName(stage_id, Encoding.UTF8.GetBytes(prim_path));
        return usd.exporter.revit.stringutil.convertUTF8String(intPtr);
    }

    public static bool setDisplayName(long stage_id, string prim_path, string name)
    {
        return usd_exporter_revit_core_setDisplayName(stage_id, Encoding.UTF8.GetBytes(prim_path), Encoding.UTF8.GetBytes(name));
    }

    public static string getValidPrimName(long stage_id, string name)
    {
        IntPtr intPtr = usd_exporter_revit_core_getValidPrimName(stage_id, Encoding.UTF8.GetBytes(name));
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    public static string[] getValidPrimNames(long stage_id, string[] names, string[] reservedNames = null)
    {
        if (names == null || names.Length == 0)
            return null;

        // Converts a string array to an IntPtr.
        // This conversion is necessary if the string contains UTF-8.
        usd.exporter.revit.StringArrayToIntPtr stringArrayToIntPtr = new usd.exporter.revit.StringArrayToIntPtr();
        IntPtr namesIntPtr = stringArrayToIntPtr.ConvertStringArrayToBytesArray(names);
        if (namesIntPtr == IntPtr.Zero)
            return null;

        // Marshal the reserved names the same way as names so UTF-8 identifiers are matched correctly by the native layer.
        usd.exporter.revit.StringArrayToIntPtr reservedArrayToIntPtr = new usd.exporter.revit.StringArrayToIntPtr();
        int reservedCount = (reservedNames == null) ? 0 : reservedNames.Length;
        IntPtr reservedIntPtr = (reservedCount > 0) ? reservedArrayToIntPtr.ConvertStringArrayToBytesArray(reservedNames) : IntPtr.Zero;

        // The return value is a string array.
        // The number of strings stored in returnCount.
        int returnCount = 0;
        IntPtr intPtr = usd_exporter_revit_core_getValidPrimNames(stage_id, namesIntPtr, names.Length, reservedIntPtr, reservedCount, out returnCount);
        if (intPtr == IntPtr.Zero || returnCount == 0)
            return null;

        IntPtr[] ptrBuff = new IntPtr[returnCount];
        Marshal.Copy(intPtr, ptrBuff, 0, returnCount);

        string[] stringBuffer = new string[returnCount];
        for (int i = 0; i < returnCount; ++i)
            stringBuffer[i] = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptrBuff[i]);
        return stringBuffer;
    }

    // StageAlgo.
    public static long createStage(string identifier, string defaultPrimName, string upAxis = "Y", double linearUnits = 0.01)
    {
        return usd_exporter_revit_core_createStage(Encoding.UTF8.GetBytes(identifier), Encoding.UTF8.GetBytes(defaultPrimName), Encoding.UTF8.GetBytes(upAxis), linearUnits);
    }

    public static void saveStage(long stage_id, string commit = "")
    {
        usd_exporter_revit_core_saveStage(stage_id, Encoding.UTF8.GetBytes(commit));
    }

    // XformAlgo.
    public static bool setLocalTransform(long stage_id, string prim_path, double[,] transform)
    {
        return usd_exporter_revit_core_setLocalTransform(stage_id, Encoding.UTF8.GetBytes(prim_path), transform);
    }

    public static bool setLocalTransformPivot(long stage_id, string path, double[,] transform, GfVec3d pivot)
    {
        return usd_exporter_revit_core_setLocalTransformPivot(stage_id, Encoding.UTF8.GetBytes(path), transform, pivot);
    }

    public static bool setLocalTransformMatrix(long stage_id, string prim_path, double[,] transform)
    {
        return usd_exporter_revit_core_setLocalTransformMatrix(stage_id, Encoding.UTF8.GetBytes(prim_path), transform);
    }

    public static void getLocalTransformComponents(long stage_id, string prim_path, out GfVec3d translation, out GfVec3d pivot, out GfVec3d rotation, out RotationOrder rotationOrder, out GfVec3d scale)
    {
        rotationOrder = RotationOrder.eXyz;

        IntPtr _translation = IntPtr.Zero;
        IntPtr _pivot = IntPtr.Zero;
        IntPtr _rotation = IntPtr.Zero;
        IntPtr _scale = IntPtr.Zero;
        usd_exporter_revit_core_getLocalTransformComponents(stage_id, Encoding.UTF8.GetBytes(prim_path), out _translation, out _pivot, out _rotation, out rotationOrder, out _scale);

        translation = (GfVec3d)Marshal.PtrToStructure(_translation, typeof(GfVec3d));
        pivot = (GfVec3d)Marshal.PtrToStructure(_pivot, typeof(GfVec3d));
        rotation = (GfVec3d)Marshal.PtrToStructure(_rotation, typeof(GfVec3d));
        scale = (GfVec3d)Marshal.PtrToStructure(_scale, typeof(GfVec3d));
    }

    public static string defineXform(long stage_id, string prim_path)
    {
        IntPtr intPtr = usd_exporter_revit_core_defineXform(stage_id, Encoding.UTF8.GetBytes(prim_path));
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr);
    }

    public static double getGeomLinearUnits(string name)
    {
        return usd_exporter_revit_getGeomLinearUnits(Encoding.UTF8.GetBytes(name));
    }

    public static double getMetersPerUnitFromFile(string filePath)
    {
        return usd_exporter_revit_core_getMetersPerUnitFromFile(Encoding.UTF8.GetBytes(filePath));
    }
}
}

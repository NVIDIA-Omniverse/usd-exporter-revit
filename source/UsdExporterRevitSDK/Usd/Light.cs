// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Lighting;
using Autodesk.Revit.DB.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class Light : Prim
{
    private double[,] matrix;

    public UsdLightShape Shape;
    public string IesFileName = string.Empty;
    public byte[] IesData;
    public float Intensity;
    public float Temperature; // kelvin
    public float ConeAngle = 180f; // degrees
    public double TiltAngle = -90; // degrees

    public Light(long stageId, string name, Prim parent, Asset asset, LightType revitLight) : base(stageId, name + $"_{parent.Children.Count}", parent)
    {
        process(asset, revitLight);
        ActivateBranch();
    }

    // private constructor for default light
    private Light(long stageId, string name, Prim parent) : base(stageId, name + $"_{parent.Children.Count}", parent)
    {
        double radius = 0.0254; // 1" in meters
        double scale = usd.exporter.revit.core.getGeomLinearUnits(Enum.GetName(typeof(UnitType), ExportManager.Settings.Options.UnitType));
        radius = radius / scale;
        Shape = new UsdSphereLight((float)radius);
        Intensity = 14000f;
        Temperature = 3200f;
        ActivateBranch();
    }

    public static Light DefaultLight(long stageId, string name, Prim parent)
    {
        return new Light(stageId, name, parent);
    }

    public override void Write(long stageId)
    {
        bool exists = false;
        // 1. define light
        switch (Shape.Type)
        {
            case UsdLightType.Sphere:
                UsdSphereLight sphereLight = (UsdSphereLight)Shape;
                pxr.usd.usdLux.sphereLight.define(stageId, Parent.Path, Name, sphereLight.Radius, Intensity);
                break;
            case UsdLightType.Cylinder:
                UsdCylinderLight cylinderLight = (UsdCylinderLight)Shape;
                pxr.usd.usdLux.cylinderLight.define(stageId, Parent.Path, Name, cylinderLight.Length, cylinderLight.Radius, Intensity);
                break;
            case UsdLightType.Rectangle:
                UsdRectangleLight rectangleLight = (UsdRectangleLight)Shape;
                usd.exporter.revit.core.defineRectLight(stageId, Path, rectangleLight.Width, rectangleLight.Height, Intensity);
                break;
            case UsdLightType.Disk:
                UsdDiskLight diskLight = (UsdDiskLight)Shape;
                pxr.usd.usdLux.diskLight.define(stageId, Parent.Path, Name, diskLight.Radius, Intensity);
                break;
            default:
                break;
        }
        if (exists)
        {
            pxr.usd.prim.setPrimToOver(stageId, Path);
        }
        usd.exporter.revit.core.createColorTemperatureAttr(stageId, Path, Temperature);
        usd.exporter.revit.core.createEnableColorTemperatureAttr(stageId, Path, true);

        // 2. copy ies file into the export's local IES folder and reference it via ShapingAPI
        if (!string.IsNullOrEmpty(IesFileName))
        {
            string tempPath = System.IO.Path.Combine(ExportManager.OvTempFolder, IesFileName);
            if (!System.IO.File.Exists(tempPath))
            {
                System.IO.File.WriteAllBytes(tempPath, IesData);
            }

            Stage stage = ExportManager.TryGetStage(stageId);
            string iesFolder = System.IO.Path.Combine(stage.FolderPath, "IES");
            if (!usd.exporter.revit.file.client.isLocalUri(iesFolder))
            {
                // Skip writing the IES attribute rather than leave a dangling reference to a file we cannot copy.
                usd.exporter.revit.log.warning($"Copying IES profiles to non-local paths is not supported; skipping IES for \"{Name}\": \"{iesFolder}\"");
            }
            else
            {
                System.IO.Directory.CreateDirectory(iesFolder);
                System.IO.File.Copy(tempPath, System.IO.Path.Combine(iesFolder, IesFileName), true);

                string relativePath = "./IES/" + IesFileName;
                pxr.usd.usdLux.shapingApi.createIesFileAttr(stageId, Path, relativePath);
            }
        }
        // 3. set transform on light
        // Flip Z-axis for disk and rectangle lights to correct orientation
        if (Shape.Type == UsdLightType.Disk || Shape.Type == UsdLightType.Rectangle)
        {
            matrix[2, 0] *= -1.0;
            matrix[2, 1] *= -1.0;
            matrix[2, 2] *= -1.0;
        }
        usd.exporter.revit.core.setLocalTransformMatrix(stageId, Path, matrix);
        // 4. compute light extents
        usd.exporter.revit.core.createLightExtentAttr(stageId, Path);
        base.Write(stageId);
    }
    public void SetTransform(Transform t)
    {
        matrix = new double[4, 4] { { t.BasisX.X, t.BasisX.Y, t.BasisX.Z, 0.0 }, { t.BasisY.X, t.BasisY.Y, t.BasisY.Z, 0.0 }, { t.BasisZ.X, t.BasisZ.Y, t.BasisZ.Z, 0.0 }, { t.Origin.X, t.Origin.Y, t.Origin.Z, 1.0 } };

        // Tilt Angle is already included in transform
    }

    private void process(Asset a, LightType light)
    {
        // distribution
        LightDistribution dist = light.GetLightDistribution();
        if (dist is PhotometricWebLightDistribution)
        {
            PhotometricWebLightDistribution photo = dist as PhotometricWebLightDistribution;
            IesFileName = photo.PhotometricWebFile;
            if (!string.IsNullOrEmpty(IesFileName))
            {
                IesData = Encoding.Unicode.GetBytes((a.FindByName(LightAsset.LightProfileCacheData) as AssetPropertyString).Value);
            }
            double tilt = photo.TiltAngle; // radians with +90 degrees pointing down
            TiltAngle = (tilt * 180.0 / Math.PI) - 180.0;
        }
        else if (dist is SpotLightDistribution)
        {
            SpotLightDistribution spot = dist as SpotLightDistribution;
            double tilt = spot.TiltAngle; // radians with pi/2 pointing down
            TiltAngle = (tilt * ExportManager.RadiansToDegrees) - 180.0;
            ConeAngle = (float)(spot.SpotBeamAngle * ExportManager.RadiansToDegrees);
        }

        // intensity
        InitialIntensity ii = light.GetInitialIntensity();
        double intensity = ii.InitialIntensityValue;
        if (ii is InitialWattageIntensity)
        {
            intensity *= 10;
        }
        else if (ii is InitialIlluminanceIntensity)
        {
            // intensity is the right value here
        }
        else if (ii is InitialLuminousIntensity)
        {
            intensity *= 100;
        }
        else if (ii is InitialFluxIntensity)
        {
            intensity *= 10;
        }
        Intensity = (float)intensity;

        // color temperature
        InitialColor ic = light.GetInitialColor();
        Temperature = (float)ic.TemperatureValue;

        // shape
        LightShape shape = light.GetLightShape();
        if (shape is PointLightShape)
        {
            if (dist is SphericalLightDistribution)
            {
                Shape = new UsdSphereLight(.25f);
            }
            else
            {
                Shape = new UsdDiskLight(.25f);
            }
        }
        else if (shape is LineLightShape)
        {
            LineLightShape line = (LineLightShape)shape;
            double length = line.EmitLength;
            Shape = new UsdCylinderLight((float)length, 0.125f);
        }
        else if (shape is CircleLightShape)
        {
            CircleLightShape circle = (CircleLightShape)shape;
            float radius = (float)(circle.EmitDiameter / 2.0); // feet
            Shape = new UsdDiskLight(radius);
        }
        else if (shape is RectangleLightShape)
        {
            RectangleLightShape rectangle = (RectangleLightShape)shape;
            double length = rectangle.EmitLength; // feet
            double width = rectangle.EmitWidth; // feet
            Shape = new UsdRectangleLight((float)length, (float)width);
        }
    }

    private static class LightAsset
    {
        public static string On = "on";
        public static string ShadowOn = "shadowOn";
        public static string LightColorUnits = "lightColorUnits";
        public static string LightTemperature = "lightTemperature";
        public static string LightTempPresets = "lightTemperaturePresets";
        public static string FilterColor = "filterColor";
        public static string ElectricalEfficiency = "electricalEfficiency";
        public static string IntensityUnits = "intensityUnitys";
        public static string IntensityValue = "intensityValue";
        public static string LightLossFactor = "lightLossFactor";
        public static string ColorShifting = "colorShifting";
        public static string LightObjectAreaType = "lightobjectareatype";
        public static string RectangleWidth = "rectangle_width";
        public static string RectangleLength = "rectangle_length";
        public static string IsLinear = "isLinearLight";
        public static string Distribution = "distribution";
        public static string LightProfileFileName = "lightProfileFileName";
        public static string LightProfileCacheData = "lightProfileCacheData";
        public static string HotSpot = "hotSpot";
        public static string FallOff = "fallOff";
    }
}

internal enum UsdLightType
{
    Sphere,
    Cylinder,
    Rectangle,
    Disk
}

internal class UsdLightShape
{
    public UsdLightType Type;
}

internal class UsdSphereLight : UsdLightShape
{
    public float Radius;
    public UsdSphereLight(float radius)
    {
        Radius = radius;
        Type = UsdLightType.Sphere;
    }
}
internal class UsdCylinderLight : UsdLightShape
{
    public float Length;
    public float Radius;

    public UsdCylinderLight(float length, float radius)
    {
        Length = length;
        Radius = radius;
        Type = UsdLightType.Cylinder;
    }
}
internal class UsdRectangleLight : UsdLightShape
{
    public float Height;
    public float Width;

    public UsdRectangleLight(float height, float width)
    {
        Height = height;
        Width = width;
        Type = UsdLightType.Rectangle;
    }
}
internal class UsdDiskLight : UsdLightShape
{
    public float Radius;

    public UsdDiskLight(float radius)
    {
        Radius = radius;
        Type = UsdLightType.Disk;
    }
}
}

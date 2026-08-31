// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class Stage
{
    public long Id;
    public string FolderPath;
    public string Name;
    public string Extension;
    public string FullPath;
    public Xform Default;
    public bool Written = false;

    // long value is stageId
    public Dictionary<Prim, long> ChildReferences = new Dictionary<Prim, long>();

    // long key is Revit ID
    public Dictionary<long, Link> Links = new Dictionary<long, Link>();
    // by family type/symbol id
    public List<ClassPrim> ClassPrims = new List<ClassPrim>();
    // by family id
    public Dictionary<long, Family> Families = new Dictionary<long, Family>();

    // set as materials overrides are encountered in revit export
    public List<MaterialOverride> MaterialOverrides = new List<MaterialOverride>();

    public Stage(string folderPath, string fileName, string fileExtension, string defaultPrim, bool isReference)
    {
        if (!usd.exporter.revit.file.client.isLocalUri(folderPath))
        {
            throw new InvalidOperationException($"Stage folder is not a local path and cannot be written: \"{folderPath}\"");
        }

        Directory.CreateDirectory(folderPath);

        FullPath = folderPath + '/' + fileName + fileExtension;
        string defaultPrimName = string.Empty;

        defaultPrimName = defaultPrim;

        string validDefault = usd.exporter.revit.core.getValidPrimName(-1, defaultPrimName);

        Id = usd.exporter.revit.core.createStage(FullPath, validDefault, "Z", ExportManager.REVIT_DEFAULT_MPU);
        if (Id == 0)
        {
            throw new InvalidOperationException($"Failed to create USD stage at \"{FullPath}\"");
        }

        FolderPath = folderPath;
        Name = fileName;
        Extension = fileExtension;
        Default = new Xform(Id, defaultPrimName, PrimKind.Assembly);
        if (isReference)
        {
            ExportManager.AddReference(Id, this);
        }
    }

    public List<string> ToReferencePaths(Prim prim)
    {
        return toReferencePaths(prim.Path, Default, Default.Path, true);
    }

    private static List<string> toReferencePaths(string pathStub, Prim prim, string defaultPrimPath, bool isDefaultPrim)
    {
        List<string> paths = new List<string>();
        if (!isDefaultPrim)
        {
            paths.Add(pathStub + prim.Path.Remove(0, defaultPrimPath.Length));
        }
        foreach (Prim p in prim.Children)
        {
            paths.AddRange(toReferencePaths(pathStub, p, defaultPrimPath, false));
        }
        foreach (string refPath in prim.ReferencedPrimPaths)
        {
            paths.Add(pathStub + refPath.Remove(0, defaultPrimPath.Length));
        }
        return paths;
    }

    public void Write()
    {

        // write default and scope/xform structures
        Default.Write(Id);

        // write references files before adding references to prims
        foreach (KeyValuePair<Prim, long> child in ChildReferences)
        {
            Stage refStage = ExportManager.TryGetStage(child.Value);
            if (refStage != null && !refStage.Written)
            {
                refStage.Write();
            }
            string relativePath = GetRelativePathToStage(refStage);
            child.Key.WriteReference();
        }

        // write meshes and cylinders + bind materials
        writeMeshes(Default);

        // write variant selections
        writeVariants(Default);

        // turn off things we dont want to see
        ExportManager.SetVisibilityForStage(this);

        // Convert stage units from feet
        Stage.ConvertMetersPerUnit(Id, ExportManager.Settings.Options.UnitType);

        // save it
        usd.exporter.revit.core.saveStage(Id, "Exported from Revit");
        usd.exporter.revit.core.evictStage(Id);

        Written = true;
    }

    private void writeVariants(Prim prim)
    {
        if (!string.IsNullOrEmpty(prim.VariantSet) && !string.IsNullOrEmpty(prim.VariantOption))
        {
            pxr.usd.variantSet.setSelection(Id, prim.Path, prim.VariantSet, prim.VariantOption);
        }
        foreach (Prim child in prim.Children)
        {
            writeVariants(child);
        }
    }
    private void writeMeshes(Prim prim)
    {
        foreach (Prim child in prim.Children)
        {
            if (child is Mesh)
            {
                Mesh mesh = (Mesh)child;
                mesh.Write(Id);
            }
            else if (child is Cylinder)
            {
                Cylinder cylinder = (Cylinder)child;
                cylinder.Write(Id);
            }
            else
            {
                writeMeshes(child);
            }
        }
    }

    // Get metersPerUnit value from UnitType.
    public static double GetMetersPerUnitValue(UnitType unitType)
    {
        string name = Enum.GetName(typeof(UnitType), unitType);
        return usd.exporter.revit.core.getGeomLinearUnits(name);
    }

    public static void ConvertMetersPerUnit(long Id, UnitType unitType)
    {
        double metersPerUnit = GetMetersPerUnitValue(unitType);

        // Call the C++ function to set MPU and scale extents, translation, and pivot
        bool success = usd.exporter.revit.core.usd_exporter_revit_core_convertMetersPerUnit(Id, metersPerUnit);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to convert meters per unit to {metersPerUnit} for stage {Id}");
        }
    }

    public string GetRelativePathToStage(Stage stage)
    {
        string relativePath = "./";
        if (this.Id != stage.Id)
        {
            relativePath += stage.FullPath.Replace(this.FolderPath + "/", "");
        }
        else
        {
            relativePath += stage.Name + stage.Extension; // should we even allow self referencing... is this bad??
        }
        return relativePath;
    }

    public Prim GetPrimAtPath(string path)
    {
        Prim prim = null;
        List<string> prims = path.Split('/').ToList();
        prims.RemoveAt(0);
        Prim parent = Default;
        foreach (string p in prims)
        {
            if (parent.HasChild(p))
            {
                parent = parent.GetChild(p);
            }
        }
        if (parent.Name == prims.Last())
        {
            prim = parent;
        }
        return prim;
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class Family
{
    public long FamilyId;
    public long StageId;
    public long MaterialStageId;
    public VariantSet VariantSet;
    public bool Written = false;
    public Scope Instance = null;

    public Family(long id, string folderPath, string fileName, string fileExtension, string defaultPrimName)
    {
        FamilyId = id;
        fileName = (fileName.Length > 25) ? fileName.Substring(0, 25) : fileName;
        fileName = fileName.Trim();
        Stage stage = new Stage(folderPath, fileName, fileExtension, defaultPrimName, true);
        Stage materialStage = new Stage(folderPath, ExportManager.Settings.Options.MaterialFolderName, fileExtension, ExportManager.Settings.Options.MaterialFolderName, true);
        Scope looks = new Scope(materialStage.Id, ExportManager.Settings.Options.MaterialFolderName, materialStage.Default);
        StageId = stage.Id;
        MaterialStageId = materialStage.Id;
        stage.Default.AddStageReference(materialStage.Id, false);

        Instance = new Scope(StageId, "Instance", stage.Default);
        VariantSet = new VariantSet(StageId, "FamilyType", stage.Default);
    }

    public VariantOption AddVariantOption(long typeId, string name)
    {
        name = (name.Length > 25) ? name.Substring(0, 25) : name;
        name = name.TrimEnd();
        string pathsafe = name.RemoveBadWindowsFilePathChars() + $"_{VariantSet.Options.Count}";
        VariantOption option = new VariantOption(StageId, typeId, name + $"_{VariantSet.Options.Count}", VariantSet);
        Stage stage = ExportManager.TryGetStage(StageId);
        Stage geometryStage = new Stage(stage.FolderPath + "/Geometry", pathsafe, stage.Extension, name, true);
        option.ReferencePath = stage.GetRelativePathToStage(geometryStage);
        option.GeometryStageId = geometryStage.Id;
        VariantSet.Options.Add(option);
        Instance.ActivateBranch();
        return option;
    }

    public void Write()
    {
        setMaterialOverrides();
        foreach (VariantOption option in VariantSet.Options)
        {
            Stage geo = ExportManager.TryGetStage(option.GeometryStageId);
            if (geo != null)
            {
                // we want to avoid some of the behavior of the default write sequence
                // particularly how meshes try to bind materials
                writeGeoStage(geo);
            }
        }
        if (MaterialStageId != StageId)
        {
            Stage mat = ExportManager.TryGetStage(MaterialStageId);
            if (mat != null)
            {
                mat.Write();
            }
        }
        Stage family = ExportManager.TryGetStage(StageId);
        family.Default.Write(StageId);
        family.Default.WriteReference();
        VariantSet.Write(StageId);

        usd.exporter.revit.core.saveStage(StageId);
        Written = true;
    }
    private static void writeGeoStage(Stage stage)
    {
        pxr.usd.prim.setKind(stage.Id, stage.Default.Path, pxr.usd.Kind.eComponent);
        if (!string.IsNullOrEmpty(stage.Default.DisplayName))
        {
            usd.exporter.revit.core.setDisplayName(stage.Id, stage.Default.Path, stage.Default.DisplayName);
        }
        foreach (Prim child in stage.Default.Children)
        {
            if (child is Mesh)
            {
                Mesh mesh = (Mesh)child;
                if (mesh.MeshData.Normals.Count != mesh.MeshData.Points.Count)
                {
                    mesh.MeshData.Normals.Clear();
                }
                // write it
                usd.exporter.revit.core.definePolyMesh(
                    stage.Id,
                    mesh.Path,
                    mesh.MeshData.FaceVertexCount.ToArray(),
                    mesh.MeshData.FaceVertexIndices.ToArray(),
                    mesh.MeshData.GetPointsArray(),
                    "vertex",
                    normals: mesh.MeshData.GetNormalsArray(),
                    uvsInterporation: "vertex",
                    uvs: mesh.MeshData.GetUVsArray()
                );
                if (!mesh.CastShadows)
                {
                    pxr.usd.prim.setDoNotCastShadows(stage.Id, mesh.Path, true);
                }
                if (!string.IsNullOrEmpty(mesh.DisplayName))
                {
                    usd.exporter.revit.core.setDisplayName(stage.Id, mesh.Path, mesh.DisplayName);
                }
            }
        }

        usd.exporter.revit.core.saveStage(stage.Id);
    }
    private void setMaterialOverrides()
    {
        Stage family = ExportManager.TryGetStage(StageId);
        Stage materials = ExportManager.TryGetStage(MaterialStageId);
        foreach (VariantOption option in VariantSet.Options)
        {
            Stage geo = ExportManager.TryGetStage(option.GeometryStageId);
            List<Mesh> meshes = geo.Default.Children.Where(p => p is Mesh).Cast<Mesh>().ToList();
            foreach (Mesh mesh in meshes)
            {
                Material mat = MaterialManager.GetMaterial(MaterialStageId, mesh.MeshData.MaterialId);
                if (mat != null)
                {
                    string root = family.Default.Path;
                    string materialStub = mat.Path.Remove(0, materials.Default.Path.Length);
                    string materialPath = root + materialStub;

                    string meshStub = mesh.Path.Remove(0, geo.Default.Path.Length);
                    string meshPath = Instance.Path + meshStub;

                    option.MaterialBindings.Add(new MaterialOverride(meshPath, materialPath));
                }
            }
        }
    }
}
}

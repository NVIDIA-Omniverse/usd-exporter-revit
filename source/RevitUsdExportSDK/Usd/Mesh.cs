// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace RevitUsdExportSdk
{
internal class Mesh : Prim
{
    public MeshData MeshData;
    public bool CastShadows = true;
    private long linkId = -1;

    public Mesh(long stageId, string name, Prim parent, MeshData meshData, long linkId = -1) : base(stageId, name, parent)
    {
        PrimType = PrimType.Mesh;
        MeshData = meshData;
        this.linkId = linkId;
        ActivateBranch();
    }
    public override void Write(long stageId)
    {
        bool exists = false;
        if (MeshData.Normals.Count != MeshData.Points.Count)
        {
            MeshData.Normals.Clear();
        }
        revit.usd.export.core
            .definePolyMesh(stageId, Path, MeshData.FaceVertexCount.ToArray(), MeshData.FaceVertexIndices.ToArray(), MeshData.GetPointsArray(), "vertex", normals: MeshData.GetNormalsArray(), uvsInterporation: "vertex", uvs: MeshData.GetUVsArray());
        if (exists)
        {
            pxr.usd.prim.setPrimToOver(stageId, Path);
        }
        bindMaterial();
        if (!CastShadows)
        {
            pxr.usd.prim.setDoNotCastShadows(stageId, Path, true);
        }
        base.Write(stageId);
    }

    private void bindMaterial()
    {
        if (ExportManager.Settings.Options.InstanceFamilies && ExportManager.Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses && Parent.Parent is ClassPrim)
        {

            ClassPrim _class = (ClassPrim)Parent.Parent;
            Material material = _class.MaterialScope.Children.Where(p => ((Material)p).Id == MeshData.MaterialId).Cast<Material>().First();
            revit.usd.export.core.bindMaterial(StageId, Path, material.Path);
        }
        else
        {
            if (StageId == ExportManager.MainStage.Id)
            {
                string materialPath = MaterialManager.GetMaterialPath(ExportManager.MaterialStage.Id, MeshData.MaterialId);
                if (!string.IsNullOrEmpty(materialPath) && materialPath.Length > ExportManager.MaterialStage.Default.Path.Length)
                {
                    string material = materialPath.Remove(0, ExportManager.MaterialStage.Default.Path.Length);
                    material = ExportManager.MainStage.Default.Path + material;
                    revit.usd.export.core.bindMaterial(StageId, Path, material);
                }
                return;
            }
            Link link = ExportManager.TryGetLink(linkId);
            if (link != null)
            {
                Stage stage = ExportManager.TryGetStage(StageId);
                Stage matStage = ExportManager.TryGetStage(link.MaterialStageId);
                string materialPath = MaterialManager.GetMaterialPath(link.MaterialStageId, MeshData.MaterialId);
                if (!string.IsNullOrEmpty(materialPath) && materialPath.Length > matStage.Default.Path.Length)
                {
                    string material = materialPath.Remove(0, matStage.Default.Path.Length);
                    material = stage.Default.Path + material;
                    revit.usd.export.core.bindMaterial(StageId, Path, material);
                }
            }
        }
    }
    public void AddMeshData(MeshData meshData)
    {
        bool addNormals = MeshData.Normals.Count == MeshData.Points.Count && meshData.Normals.Count == meshData.Points.Count;
        int offset = MeshData.Points.Count;
        MeshData.Points.AddRange(meshData.Points);
        if (addNormals)
        {
            MeshData.Normals.AddRange(meshData.Normals);
        }
        else
        {
            MeshData.Normals.Clear();
        }
        MeshData.UVs.AddRange(meshData.UVs);
        MeshData.FaceVertexIndices.AddRange(meshData.FaceVertexIndices.Select(vi => vi + offset));
        MeshData.FaceVertexCount.AddRange(meshData.FaceVertexCount);
    }
}
internal class MeshData
{
    public List<XYZ> Points;
    public List<XYZ> Normals;
    public List<UV> UVs;
    public List<int> FaceVertexIndices;
    public List<int> FaceVertexCount;
    public long MaterialId;
    public double MaterialArea;

    public MeshData(List<XYZ> points, List<XYZ> normals, List<UV> uvs, List<int> faceVertexIndices, List<int> faceVertexCount, long materialId, double materialArea)
    {
        Points = points;
        Normals = normals;
        UVs = uvs;
        FaceVertexIndices = faceVertexIndices;
        FaceVertexCount = faceVertexCount;
        MaterialId = materialId;
        MaterialArea = materialArea;
    }

    public void Add(MeshData data)
    {
        bool addNormals = this.Normals.Count == this.Points.Count && data.Normals.Count == data.Points.Count;
        int offset = this.Points.Count;
        this.Points.AddRange(data.Points);
        if (addNormals)
        {
            this.Normals.AddRange(data.Normals);
        }
        else
        {
            this.Normals.Clear();
        }
        this.UVs.AddRange(data.UVs);
        this.FaceVertexIndices.AddRange(data.FaceVertexIndices.Select(vi => vi + offset));
        this.FaceVertexCount.AddRange(data.FaceVertexCount);
    }

    public revit.usd.export.GfVec3f[] GetPointsArray()
    {
        List<revit.usd.export.GfVec3f> points = new List<revit.usd.export.GfVec3f>();
        foreach (XYZ p in this.Points)
        {
            points.Add(new revit.usd.export.GfVec3f((float)p.X, (float)p.Y, (float)p.Z));
        }
        return points.ToArray();
    }
    public revit.usd.export.GfVec3f[] GetNormalsArray()
    {
        List<revit.usd.export.GfVec3f> normals = new List<revit.usd.export.GfVec3f>();
        foreach (XYZ n in this.Normals)
        {
            normals.Add(new revit.usd.export.GfVec3f((float)n.X, (float)n.Y, (float)n.Z));
        }
        return normals.ToArray();
    }

    public revit.usd.export.GfVec2f[] GetUVsArray()
    {
        List<revit.usd.export.GfVec2f> uvs = new List<revit.usd.export.GfVec2f>();
        foreach (UV uv in this.UVs)
        {
            uvs.Add(new revit.usd.export.GfVec2f((float)uv.U, (float)uv.V));
        }
        return uvs.ToArray();
    }

    public string ToString(int tabCount = 0)
    {
        string output = string.Empty;
        string tabs = string.Empty;
        for (int i = 0; i < tabCount; i++)
        {
            tabs += "\t";
        }
        output += tabs + "{";
        string closure = "}\n";

        output += "Material: " + MaterialId + "\t";
        output += "Area: " + MaterialArea + "\t";
        output += "Points: " + Points.Count + "\t";
        output += "Normals: " + Normals.Count + "\t";
        output += "UVs: " + UVs.Count + "\t";
        output += "FVI: " + FaceVertexCount.Count;
        output += closure;

        return output;
    }
}
}

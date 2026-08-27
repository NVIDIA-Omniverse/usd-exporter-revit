// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUsdExportSdk
{
internal class Cylinder : Prim
{
    public revit.usd.export.GfVec3f StartPoint;
    public revit.usd.export.GfVec3f EndPoint;
    public double Radius;
    public long MaterialId;
    private long linkId = -1;

    public Cylinder(long stageId, string name, Prim parent, revit.usd.export.GfVec3f startPoint, revit.usd.export.GfVec3f endPoint, double radius, long linkId = -1) : base(stageId, name, parent)
    {
        PrimType = PrimType.Cylinder;
        StartPoint = startPoint;
        EndPoint = endPoint;
        Radius = radius;
        ActivateBranch();
    }
    public override void Write(long stageId)
    {
        bool exists = false;
        pxr.usd.usdGeom.cylinder.define(stageId, Parent.Path, Name, StartPoint, EndPoint, Radius);
        if (exists)
        {
            pxr.usd.prim.setPrimToOver(stageId, Path);
        }
        bindMaterial(stageId);
        base.Write(stageId);
    }

    private void bindMaterial(long stageId)
    {
        if (stageId == ExportManager.MainStage.Id)
        {
            string materialPath = MaterialManager.GetMaterialPath(ExportManager.MaterialStage.Id, MaterialId);
            if (!string.IsNullOrEmpty(materialPath) && materialPath.Length > ExportManager.MaterialStage.Default.Path.Length)
            {
                string material = materialPath.Remove(0, ExportManager.MaterialStage.Default.Path.Length);
                material = ExportManager.MainStage.Default.Path + material;
                revit.usd.export.core.bindMaterial(stageId, Path, material);
            }
            return;
        }
        else
        {
            Link link = ExportManager.TryGetLink(linkId);
            if (link != null)
            {
                Stage stage = ExportManager.TryGetStage(stageId);
                Stage matStage = ExportManager.TryGetStage(link.MaterialStageId);
                string materialPath = MaterialManager.GetMaterialPath(link.MaterialStageId, MaterialId);
                if (!string.IsNullOrEmpty(materialPath) && materialPath.Length > matStage.Default.Path.Length)
                {
                    string material = materialPath.Remove(0, matStage.Default.Path.Length);
                    material = stage.Default.Path + material;
                    revit.usd.export.core.bindMaterial(StageId, Path, material);
                }
            }
        }
    }
}
}

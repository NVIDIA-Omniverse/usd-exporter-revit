// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class VariantSet : Prim
{
    // long is Revit Type Id
    public List<VariantOption> Options;

    public VariantSet(long stageId, string name, Prim parent) : base(stageId, name, parent)
    {
        PrimType = PrimType.VariantSet;
        Kind = PrimKind.None;
        Options = new List<VariantOption>();
    }

    public bool HasOption(long typeId, Dictionary<long, double> materialAreas, out string optionName)
    {
        optionName = string.Empty;

        // Iterate without creating intermediate list
        foreach (VariantOption option in Options)
        {
            if (option.RevitTypeId != typeId)
                continue;

            Stage stage = ExportManager.TryGetStage(option.GeometryStageId);
            List<Mesh> meshes = stage.Default.Children.Where(p => p is Mesh).Cast<Mesh>().ToList();
            int missing = 0;

            foreach (KeyValuePair<long, double> mat in materialAreas)
            {
                // When section box is active, skip material area comparison because cropping
                // can cause the same family type to have different material areas
                if (ExportManager.IsSectionBoxActive)
                {
                    if (!meshes.Any(m => m.MeshData.MaterialId == mat.Key))
                    {
                        missing++;
                    }
                }
                else
                {
                    if (!meshes.Any(m => m.MeshData.MaterialId == mat.Key && m.MeshData.MaterialArea.Equals(mat.Value)))
                    {
                        missing++;
                    }
                }
            }

            if (missing == 0 || materialAreas.Count == 0)
            {
                optionName = option.Name;
                return true;
            }
            else if (meshes.Count + missing == materialAreas.Count)
            {
                optionName = option.Name;
                return true;
            }
        }
        return false;
    }

    public override void Write(long stageId)
    {
        pxr.usd.variantSet.addSetToPrim(stageId, Parent.Path, Name);
        foreach (VariantOption option in Options)
        {
            option.Write(stageId);
        }
        base.Write(stageId);
    }
}

internal class VariantOption : Prim
{
    public List<MaterialOverride> MaterialBindings;
    public long GeometryStageId;
    public long RevitTypeId;

    public VariantOption(long stageId, long revitTypeId, string name, Prim parent) : base(stageId, name, parent)
    {
        MaterialBindings = new List<MaterialOverride>();
        RevitTypeId = revitTypeId;
    }
    public override void Write(long stageId)
    {
        pxr.usd.variantSet.addOptionToSet(stageId, Parent.Parent.Path, Parent.Name, Name);
        pxr.usd.stage.setVariantEditTarget(stageId, Parent.Parent.Path, Parent.Name, Name);

        Stage stage = ExportManager.TryGetStage(stageId);
        Prim instance = stage.GetPrimAtPath(stage.Default.Path + "/Instance");
        if (instance != null)
        {
            if (ExportManager.Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsPayload)
            {
                pxr.usd.prim.addPayload(stageId, instance.Path, ReferencePath);
            }
            else
            {
                pxr.usd.prim.addReference(stageId, instance.Path, ReferencePath);
            }
        }

        foreach (MaterialOverride materialOverride in MaterialBindings)
        {
            usd.exporter.revit.core.bindMaterial(stageId, materialOverride.PrimPath, materialOverride.MaterialPath);
        }
        pxr.usd.stage.setEditTargetToRoot(stageId);
        base.Write(stageId);
    }
}
}

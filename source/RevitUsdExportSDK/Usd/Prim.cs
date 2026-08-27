// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUsdExportSdk
{
internal class Prim
{
    public PrimType PrimType;
    public PrimKind Kind;
    public Prim Parent;
    public List<Prim> Children = new List<Prim>();

    public long StageId;
    public string Path;
    public string Name;
    public string DisplayName;
    public string VariantSet = string.Empty;
    public string VariantOption = string.Empty;

    public long Id;

    public List<string> ReferencedPrimPaths = new List<string>();
    public string ReferencePath = string.Empty;
    public ReferenceType TypeOfReference = ReferenceType.None;

    public bool Active = false;
    public bool Instanceable = false;

    public Prim(long stageId, string name, Prim parent)
    {
        StageId = stageId;
        Name = revit.usd.export.core.getValidPrimName(stageId, name);
        // Ensure the name is unique amongst existing siblings by appending a collision-safe suffix.
        // Existing sibling names (already valid) are reserved so duplicate source names resolve to distinct prim names.
        if (parent != null && parent.Children.Any(c => c.Name == Name))
        {
            string[] reservedNames = parent.Children.Select(c => c.Name).ToArray();
            string[] uniqueNames = revit.usd.export.core.getValidPrimNames(stageId, new string[] { name }, reservedNames);
            if (uniqueNames != null && uniqueNames.Length > 0)
            {
                Name = uniqueNames[0];
            }
            else
            {
                Name = ResolveUniqueSiblingName(stageId, name, reservedNames);
            }
        }
        DisplayName = (name != Name) ? name : string.Empty;
        Path = (parent == null) ? "/" + Name : parent.Path + "/" + Name;
        if (parent != null)
        {
            Parent = parent;
            Parent.Children.Add(this);
        }
    }

    public virtual void Write(long stageId)
    {
        if (Active)
        {
            // set display name
            if (!string.IsNullOrEmpty(DisplayName))
            {
                revit.usd.export.core.setDisplayName(stageId, Path, DisplayName);
            }
            // set kind
            switch (Kind)
            {
                case PrimKind.None:
                    break;
                case PrimKind.Component:
                    pxr.usd.prim.setKind(stageId, Path, pxr.usd.Kind.eComponent);
                    break;
                case PrimKind.Assembly:
                    pxr.usd.prim.setKind(stageId, Path, pxr.usd.Kind.eAssembly);
                    break;
                case PrimKind.SubComponent:
                    pxr.usd.prim.setKind(stageId, Path, pxr.usd.Kind.eSubcomponent);
                    break;
                case PrimKind.Model:
                    pxr.usd.prim.setKind(stageId, Path, pxr.usd.Kind.eModel);
                    break;
                default:
                    break;
            }

            // references to internal prims and external assets (not current in memory stages)
            if (!string.IsNullOrEmpty(ReferencePath) && TypeOfReference != ReferenceType.ExternalStageReference && TypeOfReference != ReferenceType.ExternalStagePayload)
            {
                WriteReference(); // stage references need to write the stage first, this happens in stages.cs
            }
            if (Instanceable)
            {
                pxr.usd.prim.setInstanceable(stageId, Path, true);
            }
        }
    }

    public void AddStageReference(long stageId, bool asPayload)
    {
        Stage thisStage = ExportManager.TryGetStage(StageId);
        if (thisStage.ChildReferences.TryGetValue(this, out long existingStageId))
        {
            thisStage.ChildReferences[this] = stageId;
        }
        else
        {
            thisStage.ChildReferences.Add(this, stageId);
        }

        Stage refStage = ExportManager.TryGetStage(stageId);
        ReferencePath = thisStage.GetRelativePathToStage(refStage);
        TypeOfReference = (asPayload) ? ReferenceType.ExternalStagePayload : ReferenceType.ExternalStageReference;
        ActivateBranch();
    }
    public void WriteReference()
    {
        if (!string.IsNullOrEmpty(ReferencePath) && TypeOfReference != ReferenceType.None)
        {
            switch (TypeOfReference)
            {
                case ReferenceType.Internal:
                    pxr.usd.prim.addInternalReference(StageId, Path, ReferencePath);
                    break;
                case ReferenceType.ExternalAssetReference:
                    pxr.usd.prim.addReference(StageId, Path, ReferencePath);
                    break;
                case ReferenceType.ExternalStageReference:
                    pxr.usd.prim.addReference(StageId, Path, ReferencePath);
                    break;
                case ReferenceType.ExternalAssetPayload:
                    pxr.usd.prim.addPayload(StageId, Path, ReferencePath);
                    break;
                case ReferenceType.ExternalStagePayload:
                    pxr.usd.prim.addPayload(StageId, Path, ReferencePath);
                    break;
                default:
                    break;
            }
        }
    }

    public void AddInternalReference(string referencePrimPath)
    {
        Stage stage = ExportManager.TryGetStage(StageId);
        Prim prim = stage.GetPrimAtPath(referencePrimPath);
        if (prim != null)
        {
            if (prim.Parent is ClassPrim)
            {
                ((ClassPrim)prim.Parent).RefCount++;
            }
        }
        ReferencePath = referencePrimPath;
        TypeOfReference = ReferenceType.Internal;
        ActivateBranch();
    }

    public void AddAssetReference(string assetPath, bool asPayload)
    {
        ReferencePath = assetPath;
        TypeOfReference = (asPayload) ? ReferenceType.ExternalAssetPayload : ReferenceType.ExternalAssetReference;
        ActivateBranch();
    }

    public bool HasChild(string childName)
    {
        return Children.Any(c => string.IsNullOrEmpty(c.DisplayName) ? c.Name == childName : c.DisplayName == childName);
    }

    public Prim GetChild(string childName)
    {
        Prim prim = null;
        List<Prim> prims = Children.Where(c => string.IsNullOrEmpty(c.DisplayName) ? c.Name == childName : c.DisplayName == childName).ToList();
        if (prims.Count > 0)
        {
            prim = prims.First();
        }
        return prim;
    }

    public void Reparent(Prim newParent)
    {
        Parent.Children.Remove(this);
        Parent = newParent;
        newParent.Children.Add(this);
        Path = Parent.Path + "/" + Name;
    }

    public void ActivateBranch()
    {
        Active = true;
        if (Parent != null && !Parent.Active)
        {
            Parent.ActivateBranch();
        }
    }

    public void DeactivateBranch()
    {
        Active = false;
        if (!Active && Parent != null && !Parent.shouldStayActive())
        {
            Parent.DeactivateBranch();
        }
    }

    private bool shouldStayActive()
    {
        bool active = false;
        foreach (Prim child in Children)
        {
            if (child is Mesh || child is Cylinder || child is Material || !string.IsNullOrEmpty(child.ReferencePath))
            {
                return true;
            }
            else
            {
                return child.shouldStayActive();
            }
        }
        return active;
    }

        // Fallback when native getValidPrimNames fails: append numeric suffixes until the name is unique among siblings.
    private static string ResolveUniqueSiblingName(long stageId, string name, string[] reservedNames)
    {
        System.Diagnostics.Debug.WriteLine($"getValidPrimNames failed for \"{name}\"; using local suffix fallback");
        string candidate = revit.usd.export.core.getValidPrimName(stageId, name);
        int suffix = 1;
        while (reservedNames.Contains(candidate))
        {
            candidate = revit.usd.export.core.getValidPrimName(stageId, name + "_" + suffix);
            suffix++;
        }
        return candidate;
    }
}

internal enum PrimType
{
    Xform,
    Scope,
    Material,
    Mesh,
    Cylinder,
    VariantSet,
    VariantOption,
    Camera
}

internal enum PrimKind
{
    None,
    Component,
    Assembly,
    SubComponent,
    Model
}

internal enum ReferenceType
{
    None,
    Internal,
    ExternalStageReference,
    ExternalAssetReference, // for existing assets
    ExternalStagePayload,
    ExternalAssetPayload,
}
}

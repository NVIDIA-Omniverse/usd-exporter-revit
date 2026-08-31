// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class ClassPrim : Prim
{
    public int RefCount;
    public Xform Instance;
    public Scope MaterialScope;
    public ClassHolding Holding;
    public long FamilyTypeId;

    public Family Family;

    public ClassPrim(long stageId, string name, Prim parent, long familyTypeId, ClassHolding type) : base(stageId, name, parent)
    {
        Holding = type;
        FamilyTypeId = familyTypeId;
        Instance = new Xform(stageId, "Instance", PrimKind.Component, this);
        if (type == ClassHolding.InternalFamilyType)
        {
            MaterialScope = new Scope(stageId, ExportManager.Settings.Options.MaterialFolderName, Instance);
        }
    }
    public override void Write(long stageId)
    {
        bool exists = false;
        if (Holding == ClassHolding.InternalFamilyType)
        {
            pxr.usd.classPrim.define(stageId, Parent.Path, Name);
            if (exists)
            {
                pxr.usd.prim.setPrimToOver(stageId, Path);
            }
            Instance.Write(stageId);
            MaterialScope.Write(stageId);
            base.Write(stageId);
        }
        else if (Holding == ClassHolding.ExternalFamilyTypeVariant)
        {
            pxr.usd.classPrim.define(stageId, Parent.Path, Name);
            if (exists)
            {
                pxr.usd.prim.setPrimToOver(stageId, Path);
            }
            // this is set to an asset reference so it is not added to the reference cache
            // we need explicit control over authoring sequence here
            Instance.TypeOfReference = (Instance.TypeOfReference == ReferenceType.ExternalAssetReference) ? ReferenceType.ExternalStageReference : ReferenceType.ExternalStagePayload;
            Instance.Write(stageId);
            if (!Family.Written)
            {
                Family.Write();
            }
            Instance.WriteReference();
            pxr.usd.variantSet.setSelection(stageId, Instance.Path, Family.VariantSet.Name, Family.VariantSet.Options.First().Name);
            base.Write(stageId);
        }
    }

    public bool HasMeshes()
    {
        if (Holding == ClassHolding.ExternalFamilyTypeVariant)
        {
            if (Family != null)
            {
                foreach (VariantOption option in Family.VariantSet.Options)
                {
                    Stage geo = ExportManager.TryGetStage(option.GeometryStageId);
                    if (geo.Default.Children.Any(p => p is Mesh))
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            if (Instance.Children.Any(p => p is Mesh))
            {
                return true;
            }
        }
        return false;
    }
}

internal enum ClassHolding
{
    ExternalFamilyTypeVariant,
    InternalFamilyType
}
}

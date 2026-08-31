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
internal class Scope : Prim
{
    public Scope(long stageId, string name, Prim parent) : base(stageId, name, parent)
    {
        PrimType = PrimType.Scope;
        Kind = PrimKind.None;
    }
    public override void Write(long stageId)
    {
        if (Active)
        {
            pxr.usd.usdGeom.scope.define(stageId, Parent.Path, Name);
            base.Write(stageId);
            foreach (Prim child in Children)
            {
                if (child is Scope)
                {
                    Scope scope = (Scope)child;
                    scope.Write(stageId);
                }
                if (child is Xform)
                {
                    Xform xform = (Xform)child;
                    xform.Write(stageId);
                }
                if (child is Material)
                {
                    Material material = (Material)child;
                    material.Write(stageId);
                }
                if (child is Light)
                {
                    Light light = (Light)child;
                    light.Write(stageId);
                }
                if (child is ClassPrim)
                {
                    ClassPrim classPrim = (ClassPrim)child;
                    classPrim.Write(stageId);
                }
                if (child is Camera)
                {
                    Camera cam = (Camera)child;
                    cam.Write(stageId);
                }
            }
        }
    }
}
}

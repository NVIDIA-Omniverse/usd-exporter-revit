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
internal class Camera : Prim
{
    private double[,] matrix;
    public Camera(long stageId, string name, Prim parent, double[,] cameraData) : base(stageId, name, parent)
    {
        PrimType = PrimType.Camera;
        Kind = PrimKind.None;
        matrix = cameraData;
        ActivateBranch();
    }

    public override void Write(long stageId)
    {
        bool exists = false;
        usd.exporter.revit.core.defineCameraEx(stageId, Path, matrix);
        if (exists)
        {
            pxr.usd.prim.setPrimToOver(stageId, Path);
        }
        base.Write(stageId);
    }

    public static void ExportViewAsCamera(View3D view, Prim parent, bool active = false)
    {
        XYZ xDir = view.RightDirection.Normalize();
        XYZ upDir = view.UpDirection.Normalize();
        XYZ viewDir = view.ViewDirection.Normalize();
        XYZ eyePoint = view.GetOrientation().EyePosition;

        Scope camerasScope = null;
        if (parent.HasChild("Cameras"))
        {
            camerasScope = parent.GetChild("Cameras") as Scope;
        }
        else
        {
            camerasScope = new Scope(parent.StageId, "Cameras", parent);
        }

        double[,] data = new double[4, 4] { { xDir.X, xDir.Y, xDir.Z, 0.0 }, { upDir.X, upDir.Y, upDir.Z, 0.0 }, { viewDir.X, viewDir.Y, viewDir.Z, 0.0 }, { eyePoint.X, eyePoint.Y, eyePoint.Z, 1.0 } };
        Camera camera = new Camera(camerasScope.StageId, view.Name, camerasScope, data);
    }
}
}

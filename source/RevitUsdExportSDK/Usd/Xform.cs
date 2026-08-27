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
internal class Xform : Prim
{
    private double[,] matrix;
    private revit.usd.export.GfVec3d pivot = new revit.usd.export.GfVec3d(0.0, 0.0, 0.0);
    private List<BIMdata> bimData;
    public Xform(long stageId, string name, PrimKind kind, Prim parent = null) : base(stageId, name, parent)
    {
        Kind = kind;
        PrimType = PrimType.Xform;
        bimData = new List<BIMdata>();
    }
    public override void Write(long stageId)
    {
        if (Children.Count > 0 || !string.IsNullOrEmpty(ReferencePath))
        {
            bool exists = false;
            if (!exists)
            {
                revit.usd.export.core.defineXform(stageId, Path);
            }
            if (matrix != null)
            {
                revit.usd.export.core.setLocalTransformPivot(stageId, Path, matrix, pivot);
            }
            if (bimData.Count > 0)
            {
                List<BIMdata> instanceData = bimData.Where(b => b.NameSpace.Contains("Instance")).ToList();
                List<BIMdata> typeData = bimData.Where(b => b.NameSpace.Contains("Type")).ToList();

                foreach (BIMdata param in instanceData)
                {
                    string valid = revit.usd.export.core.getValidPrimName(stageId, param.Name);
                    pxr.usd.prim.createStringAttribute(stageId, Path, param.NameSpace + valid, param.Value);
                    if (param.Name != valid)
                    {
                        pxr.usd.prim.setAttributeDisplayName(stageId, Path, param.NameSpace + valid, param.Name);
                    }
                }
                foreach (BIMdata param in typeData)
                {
                    string valid = revit.usd.export.core.getValidPrimName(stageId, param.Name);
                    pxr.usd.prim.createStringAttribute(stageId, Path, param.NameSpace + valid, param.Value);
                    if (param.Name != valid)
                    {
                        pxr.usd.prim.setAttributeDisplayName(stageId, Path, param.NameSpace + valid, param.Name);
                    }
                }
            }

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
                if (child is Light)
                {
                    Light light = (Light)child;
                    light.Write(stageId);
                }
            }
        }
    }
    public void SetTransform(Transform t)
    {
        matrix = new double[4, 4] { { t.BasisX.X, t.BasisX.Y, t.BasisX.Z, 0.0 }, { t.BasisY.X, t.BasisY.Y, t.BasisY.Z, 0.0 }, { t.BasisZ.X, t.BasisZ.Y, t.BasisZ.Z, 0.0 }, { t.Origin.X, t.Origin.Y, t.Origin.Z, 1.0 } };
    }

    public void SetPivot(XYZ point)
    {
        pivot = new revit.usd.export.GfVec3d(point.X, point.Y, point.Z);
    }

    public void AddBIMData(Element e)
    {
        string instance = "BIM:Instance:";
        string type = "BIM:Type:";

        if (e.Document.IsWorkshared)
        {
            WorksetTable table = e.Document.GetWorksetTable();
            addProperty(instance, "Workset", table.GetWorkset(e.WorksetId).Name);
        }
        addProperty(instance, "ElementId", e.Id.GetValue().ToString());
        addProperty(instance, "Category", e.Category.Name);
        foreach (Parameter p in e.GetOrderedParameters())
        {
            Tuple<string, string> prop = getProperty(p, e);
            addProperty(instance, prop.Item1, prop.Item2);
        }
        ElementId typeId = e.GetTypeId();
        if (typeId.GetValue() != ElementId.InvalidElementId.GetValue())
        {
            Element t = e.Document.GetElement(typeId);
            addProperty(type, "Name", t.Name);
            foreach (Parameter p in t.GetOrderedParameters())
            {
                Tuple<string, string> prop = getProperty(p, t);
                addProperty(type, prop.Item1, prop.Item2);
            }
        }
    }
    private static Tuple<string, string> getProperty(Parameter p, Element e)
    {
        string none = "None";
        string key = p.Definition.Name;
        string value = string.Empty;
        if (p.StorageType == StorageType.ElementId)
        {
            if (p.HasValue)
            {
                try
                {
                    Element pe = e.Document.GetElement(p.AsElementId());
                    if (pe != null)
                    {
                        value = pe.Name;
                    }
                    else
                    {
                        value = none;
                    }
                }
                catch (Exception ex)
                {
                    revit.log.info(ex.Message);
                    value = none;
                }
            }
        }
        else
        {
            value = p.AsValueString();
        }
        return new Tuple<string, string>(key, value);
    }
    private void addProperty(string nameSpace, string key, string value)
    {
        if (value == null)
        {
            value = string.Empty;
        }
        value = value.Replace("\"", "in"); // " <--this will do bad things if exporting usda!
        bimData.Add(new BIMdata(nameSpace, key, value));
    }
}

internal class BIMdata
{
    public string NameSpace;
    public string Name;
    public string Value;

    public BIMdata(string nameSpace, string name, string value)
    {
        NameSpace = nameSpace;
        Name = name;
        Value = value;
    }
}
}

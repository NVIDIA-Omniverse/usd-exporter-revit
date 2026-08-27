// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Lighting;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUsdExportSdk
{
internal class ElementContext
{
    public long Id;
    public long TypeId;
    public string TypeName;
    public string FamilyName;
    public string Category;
    public string Document;
    public Prim Prim;
    public Transform LocalTransform;
    public List<MeshData> Meshes;
    public List<Part> Parts;
    public XYZ PivotPoint = new XYZ();

    public ElementContext(Element e, Prim prim, Transform t)
    {
        Id = e.Id.GetValue();
        TypeId = e.GetTypeId().GetValue();
        Element element = e.Document.GetElement(e.GetTypeId());
        if (element == null)
        {
            FamilyName = "null family";
            TypeName = "null family type";
        }
        if (element is ElementType)
        {
            ElementType et = (ElementType)element;
            FamilyName = et.FamilyName;
            TypeName = et.Name;
        }
        Document = e.Document.Title;
        Category = (e.Category != null) ? e.Category.Name : "NULL";
        Prim = prim;
        LocalTransform = t;
        Parts = new List<Part>();
        Meshes = new List<MeshData>();
        PivotPoint = GetPivotPoint(e);
    }

    public static XYZ GetPivotPoint(Element e)
    {
        if (e != null)
        {
            Location l = e.Location;
            if (l is LocationCurve)
            {
                return ((LocationCurve)l).Curve.Evaluate(0.5, true);
            }
            else if (l is LocationPoint)
            {
                return ((LocationPoint)l).Point;
            }
        }
        return new XYZ();
    }

    public void AddPart(Part p)
    {
        p.Active = true;
        Part active = getActivePart(this.Parts);
        if (active == null)
        {
            Parts.Add(p);
        }
        else
        {
            active.Parts.Add(p);
        }
    }

    public void DeactivateLastPart()
    {
        Part active = getActivePart(this.Parts);
        if (active != null)
        {
            active.Active = false;
        }
    }

    public Part GetActivePart()
    {
        return getActivePart(this.Parts);
    }

    private static Part getActivePart(List<Part> parts)
    {
        Part part = null;
        if (parts.Any(p => p.Active))
        {
            part = parts.Where(p => p.Active).First();
            if (part.Parts.Any(p => p.Active))
            {
                part = getActivePart(part.Parts);
            }
        }
        return part;
    }

    // determines if the element context is a Family Instance suitable for instacning
    // does not check if settings allow instancing
    public bool IsInstance()
    {
        if (ExportManager.DoNotInstanceCategories.Contains(this.Category))
        {
            return false;
        }
        // this element has mesh data that does not belong to a family symbol, it cannot be instanced
        if (this.Meshes.Count > 0)
        {
            return false;
        }
        // we only want ot instance elements composed of a single part/FamilySymbol
        // nested geometry (CADLinkType etc) will be composed into the prototype
        // but the root part must be a single FamilySymbol
        if (this.Parts.Count == 1)
        {
            Part part = this.Parts.First();
            if (part.ObjectTypeName == "FamilySymbol")
            {
                if (part.HasMeshes())
                {
                    return true;
                }
            }
        }
        return false;
    }

    // determines if settings allow instancing and if the element context is suitable for instancing
    public bool ShouldInstance()
    {
        if (!ExportManager.Settings.Options.InstanceFamilies)
        {
            return false;
        }
        return IsInstance();
    }

    public List<MeshData> GetPrototypeMeshData()
    {
        if (!IsInstance())
        {
            return new List<MeshData>();
        }

        Dictionary<long, MeshData> combined = new Dictionary<long, MeshData>();
        combineInto(combined, Parts[0].Collapse(false));
        return new List<MeshData>(combined.Values);
    }

    public Transform GetInstanceTransform()
    {
        Transform t = Transform.Identity;
        if (IsInstance())
        {
            t = Parts.First().LocalTransform;
        }
        return t;
    }

    public List<MeshData> Collapse()
    {
        Dictionary<long, MeshData> combined = new Dictionary<long, MeshData>(this.Meshes.Count + this.Parts.Count);

        combineInto(combined, this.Meshes);

        foreach (Part part in this.Parts)
        {
            combineInto(combined, part.Collapse(true));
        }

        return new List<MeshData>(combined.Values);
    }

    private static void combineInto(Dictionary<long, MeshData> target, IEnumerable<MeshData> meshes)
    {
        foreach (MeshData m in meshes)
        {
            if (target.TryGetValue(m.MaterialId, out MeshData existing))
            {
                existing.Add(m);
            }
            else
            {
                target.Add(m.MaterialId, m);
            }
        }
    }

    public override string ToString()
    {
        string output = string.Empty;
        output += "Element {\n";
        output += "\tId: " + Id + "\n";
        output += "\tTypeId: " + TypeId + "\n";
        output += "\tDocument: " + Document + "\n";
        output += "\tCategory: " + Category + "\n";
        output += "\tFamily Name: " + FamilyName + "\n";
        output += "\tFamily Type: " + TypeName + "\n";
        output += "\tPrim: " + Prim.Path + "\n";
        output += LocalTransform.ToString(1);

        if (Meshes.Count > 0)
        {
            output += "\tMeshes: \t Count = " + Meshes.Count + "\n";
        }

        // uncomment below for more data on meshes when debugging

        // output += "\tMeshes {\n";
        // foreach (MeshData m in Meshes)
        //{
        //     output += m.ToString(1);
        // }
        // output += "\t}\n";

        foreach (Part p in Parts)
        {
            p.ToString(ref output, 1);
        }
        output += "}\n";
        return output;
    }
}

internal class Part
{
    public string ObjectTypeName;
    public string SymbolName;
    public string FamilyName;
    public string Category;
    public long FamilyId;
    public long SymbolId;
    public Transform LocalTransform;
    public List<MeshData> Meshes;
    public List<Part> Parts;
    public bool Active = false;

    public Part(Element element, Transform t)
    {
        if (element == null)
        {
            SymbolName = "null symbol";
            SymbolId = -100;
            FamilyName = "null family";
            FamilyId = -100;
            ObjectTypeName = "NULL";
            Category = "null category";
        }
        else if (element is FamilySymbol)
        {
            FamilySymbol symbol = (FamilySymbol)element;
            SymbolName = symbol.Name;
            SymbolId = symbol.Id.GetValue();
            FamilyName = symbol.FamilyName;
            FamilyId = symbol.Family.Id.GetValue();
            ObjectTypeName = symbol.GetType().Name;
            Category = (symbol.Category != null) ? symbol.Category.Name : "NULL";
        }
        else
        {
            SymbolName = element.Name;
            SymbolId = element.Id.GetValue();
            FamilyName = "null family";
            FamilyId = -200;
            ObjectTypeName = element.GetType().Name;
            Category = (element.Category != null) ? element.Category.Name : "NULL";
        }
        LocalTransform = t;
        Meshes = new List<MeshData>();
        Parts = new List<Part>();
    }

    public bool HasMeshes()
    {
        bool hasMeshes = false;
        if (this.Meshes.Count > 0)
        {
            return true;
        }
        foreach (Part p in this.Parts)
        {
            if (p.HasMeshes())
            {
                return true;
            }
        }
        return hasMeshes;
    }

    public List<MeshData> Collapse(bool toRoot)
    {
        List<MeshData> output = new List<MeshData>();
        foreach (Part p in this.Parts)
        {
            output.AddRange(p.Collapse(true));
        }
        output.AddRange(this.Meshes);
        // when collapsing for a non-instanced export, we collapse the xforms completely
        // when collapsing for instancing, we collapse to the FamilySymbol part/the first part
        if (toRoot)
        {
            foreach (MeshData m in output)
            {
                for (int i = 0; i < m.Points.Count; i++)
                {
                    m.Points[i] = this.LocalTransform.OfPoint(m.Points[i]);
                }
            }
        }
        return output;
    }

    public void ToString(ref string s, int tabCount)
    {
        string tabs = string.Empty;
        for (int i = 0; i < tabCount; i++)
        {
            tabs += "\t";
        }
        s += tabs + "Part {\n";
        string closure = tabs + "}\n";

        tabCount++;
        tabs += "\t";
        s += tabs + "Revit API Class: " + ObjectTypeName + "\n";
        s += tabs + "Category: " + Category + "\n";
        s += tabs + "Family Name: " + FamilyName + " " + FamilyId + "\n";
        s += tabs + "Symbol Name: " + SymbolName + " " + SymbolId + "\n";
        s += LocalTransform.ToString(tabCount);

        if (Meshes.Count > 0)
        {
            s += tabs + "Meshes: \t Count = " + Meshes.Count + "\n";
        }

        // uncomment below for more data on meshes when debugging

        // s += tabs + "Meshes {\n";
        // foreach(MeshData m in Meshes)
        //{
        //     s += m.ToString(tabCount + 1);
        // }
        // s += tabs + "}\n";

        foreach (Part p in Parts)
        {
            p.ToString(ref s, tabCount);
        }
        s += closure;
    }
}
}

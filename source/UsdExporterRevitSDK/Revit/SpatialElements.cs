// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace UsdExporterRevitSdk
{
internal static class SpatialElements
{
    private static string _schemeParam = string.Empty;
    private static ColorFillScheme _scheme = null;

    public static void SetSchemeValues<T>(Document doc)
    {
        BuiltInCategory category = BuiltInCategory.OST_Rooms;
        UsdExporterRevitSettingType settingType = UsdExporterRevitSettingType.RoomColorScheme;
        if (typeof(T).Name is "Space")
        {
            category = BuiltInCategory.OST_MEPSpaces;
            settingType = UsdExporterRevitSettingType.SpaceColorScheme;
        }

        List<SpatialElement> elements = new FilteredElementCollector(doc).OfCategory(category).Cast<SpatialElement>().ToList();
        if (elements.Count > 0)
        {
            ElementId categoryId = new ElementId(category);
            List<ColorFillScheme> schemes = new FilteredElementCollector(doc).OfClass(typeof(ColorFillScheme)).Cast<ColorFillScheme>().Where(s => s.CategoryId == categoryId).ToList();

            Dictionary<ColorFillScheme, string> schemeParameterMap = new Dictionary<ColorFillScheme, string>();
            foreach (ColorFillScheme scheme in schemes)
            {
                string name = scheme.Name;
                string param = (scheme.ParameterDefinition.GetValue() > 0) ? doc.GetElement(scheme.ParameterDefinition).Name : elements[0].get_Parameter((BuiltInParameter)scheme.ParameterDefinition.GetValue()).Definition.Name;
                schemeParameterMap.Add(scheme, param);
            }

            string match = ExportManager.Settings.GetStringMatch(settingType, schemeParameterMap.Select(s => s.Key.Name).ToList());
            if (schemeParameterMap.Any(s => s.Key.Name == match))
            {
                KeyValuePair<ColorFillScheme, string> kvp = schemeParameterMap.First(s => s.Key.Name == match);
                _scheme = kvp.Key;
                _schemeParam = kvp.Value;
                return;
            }
        }
        _scheme = null;
        _schemeParam = string.Empty;
    }

    public static void ExportSpatialElements<T>(Document doc, Prim rootPrim, View3D view, RevitLinkInstance linkInstance = null)
    {
        if (_scheme != null && !string.IsNullOrEmpty(_schemeParam))
        {
            Transform t = null;
            Link link = null;
            if (linkInstance != null)
            {
                link = ExportManager.TryGetLink(linkInstance.Id.Value);
                ElementId lid = new ElementId(link.LinkId);
                t = linkInstance.GetTransform();
            }

            BuiltInCategory category = (typeof(T).Name is "Space") ? BuiltInCategory.OST_MEPSpaces : BuiltInCategory.OST_Rooms;
            string catName = (category == BuiltInCategory.OST_Rooms) ? "Rooms" : "Spaces";
            List<SpatialElement> elements = new FilteredElementCollector(doc).OfCategory(category).Cast<SpatialElement>().ToList();

            if (view.IsSectionBoxActive)
            {
                elements = getSpatialElementsInSectionBox(view, elements, t);
            }

            Dictionary<string, Material> schemeMaterials = new Dictionary<string, Material>();
            Scope instances = null;
            Scope categoryScope = null;

            // not a link
            if (t == null || link == null)
            {
                schemeMaterials = MaterialManager.ProcessColorScheme(ExportManager.MaterialStage.Default, _scheme, catName + "_");
                Prim geometryRoot = ExportManager.GetGeometryRoot(ExportManager.MainStage);
                if (geometryRoot.HasChild("Instances"))
                {
                    instances = geometryRoot.GetChild("Instances") as Scope;
                }
                else
                {
                    instances = new Scope(ExportManager.MainStage.Id, "Instances", geometryRoot);
                }

                categoryScope = new Scope(ExportManager.MainStage.Id, catName, instances);
            }
            else // is a link
            {
                Stage linkStage = ExportManager.TryGetStage(link.StageId);
                Stage linkMaterialStage = ExportManager.TryGetStage(link.MaterialStageId);
                if (_scheme != null)
                {
                    schemeMaterials = MaterialManager.ProcessColorScheme(linkMaterialStage.Default, _scheme, catName + "_");
                }
                else
                {
                    schemeMaterials = MaterialManager.CopySpatialElementMaterials(ExportManager.MaterialStage.Id, link.MaterialStageId);
                }

                if (linkStage.Default.HasChild("Instances"))
                {
                    instances = linkStage.Default.GetChild("Instances") as Scope;
                }
                else
                {
                    instances = new Scope(linkStage.Id, "Instances", linkStage.Default);
                }
                categoryScope = new Scope(link.StageId, catName, instances);
            }

            if (elements.Count > 0)
            {
                SpatialElementGeometryCalculator calc = new SpatialElementGeometryCalculator(doc);
                foreach (SpatialElement e in elements)
                {
                    if (e.Area > 0)
                    {
                        Xform xform = new Xform(categoryScope.StageId, e.Id.GetValue().ToString(), PrimKind.Component, categoryScope);
                        if (ExportManager.Settings.Options.IncludeBimData)
                        {
                            xform.AddBIMData(e);
                        }

                        xform.SetTransform(Transform.Identity);
                        xform.SetPivot(ElementContext.GetPivotPoint(e));

                        Material mat = null;
                        Mesh mesh = null;
                        var parameters = e.GetParameters(_schemeParam);
                        string parameterValue = string.Empty;
                        if (parameters.Count > 0)
                        {
                            parameterValue = parameters[0].AsValueString();
                        }
                        if (parameterValue == null)
                        {
                            parameterValue = string.Empty;
                        }
                        if (schemeMaterials.TryGetValue(parameterValue, out Material outMat))
                        {
                            mat = outMat;
                        }

                        SpatialElementGeometryResults results = calc.CalculateSpatialElementGeometry(e);
                        List<MeshData> data = new List<MeshData>();

                        long matId = (mat == null) ? -1 : mat.Id;

                        if (view.IsSectionBoxActive)
                        {
                            // create a solid from the bounding box
                            BoundingBoxXYZ bbox = view.GetSectionBox();
                            XYZ pt0 = new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
                            XYZ pt1 = new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
                            XYZ pt2 = new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
                            XYZ pt3 = new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);
                            Line edge0 = Line.CreateBound(pt0, pt1);
                            Line edge1 = Line.CreateBound(pt1, pt2);
                            Line edge2 = Line.CreateBound(pt2, pt3);
                            Line edge3 = Line.CreateBound(pt3, pt0);
                            List<Curve> edges = new List<Curve>();
                            edges.Add(edge0);
                            edges.Add(edge1);
                            edges.Add(edge2);
                            edges.Add(edge3);
                            double height = bbox.Max.Z - bbox.Min.Z;
                            CurveLoop baseLoop = CurveLoop.Create(edges);
                            List<CurveLoop> loopList = new List<CurveLoop>();
                            loopList.Add(baseLoop);
                            Solid preTransformBox = GeometryCreationUtilities.CreateExtrusionGeometry(loopList, XYZ.BasisZ, height);
                            Solid transformBox = SolidUtils.CreateTransformed(preTransformBox, bbox.Transform);
                            if (t != null)
                            {
                                // t is the transform of the link in the main model
                                // the spatial element geometry is output in the link's orientation
                                // we move the section box to the orientation of the link with the link transfrom's inverse
                                transformBox = SolidUtils.CreateTransformed(transformBox, t.Inverse);
                            }
                            // get the spatial element geometry contained within the section box
                            data = ExportManager.GetSolidMeshData(BooleanOperationsUtils.ExecuteBooleanOperation(results.GetGeometry(), transformBox, BooleanOperationsType.Intersect), matId, e.Area);
                        }
                        else
                        {
                            data = ExportManager.GetSolidMeshData(results.GetGeometry(), matId, e.Area);
                        }

                        if (data.Count > 0)
                        {

                            if (t == null || link == null)
                            {
                                mesh = new Mesh(xform.StageId, "Mesh_" + matId, xform, data[0]);
                                MaterialManager.UseMaterial(ExportManager.MaterialStage.Id, matId);
                            }
                            else
                            {
                                mesh = new Mesh(xform.StageId, "Mesh_" + matId, xform, data[0], link.LinkId);
                                MaterialManager.UseMaterial(link.MaterialStageId, matId);
                            }

                            for (int i = 1; i < data.Count; i++)
                            {
                                mesh.AddMeshData(data[i]);
                            }
                            mesh.CastShadows = false;
                        }
                    }
                }
            }
        }
    }

    private static List<SpatialElement> getSpatialElementsInSectionBox(View3D view, List<SpatialElement> elements, Transform t)
    {
        List<SpatialElement> sp = new List<SpatialElement>();
        List<XYZ> bbox = view.GetSectionBox().GetMinMax(t);
        foreach (SpatialElement e in elements)
        {
            if (e.Area > 0)
            {
                // first we check if the room location point is in the section box
                LocationPoint p = e.Location as LocationPoint;
                if (p.Point.X <= bbox[1].X && p.Point.X >= bbox[0].X && p.Point.Y <= bbox[1].Y && p.Point.Y >= bbox[0].Y && p.Point.Z <= bbox[1].Z && p.Point.Z >= bbox[0].Z)
                {
                    sp.Add(e);
                }
                // then we check if any of the boundary points that construct the spatial element are within the section box
                else
                {
                    SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                    IList<IList<BoundarySegment>> loops = e.GetBoundarySegments(options);
                    foreach (IList<BoundarySegment> loop in loops)
                    {
                        bool found = false;
                        foreach (BoundarySegment segment in loop)
                        {
                            Curve curve = segment.GetCurve();
                            XYZ p1 = curve.GetEndPoint(0);
                            XYZ p2 = curve.GetEndPoint(1);
                            if ((p1.X <= bbox[1].X && p1.X >= bbox[0].X && p1.Y <= bbox[1].Y && p1.Y >= bbox[0].Y && p1.Z <= bbox[1].Z && p1.Z >= bbox[0].Z) ||
                                (p2.X <= bbox[1].X && p2.X >= bbox[0].X && p2.Y <= bbox[1].Y && p2.Y >= bbox[0].Y && p2.Z <= bbox[1].Z && p2.Z >= bbox[0].Z))
                            {
                                sp.Add(e);
                                found = true;
                                break;
                            }
                        }
                        if (found)
                        {
                            break;
                        }
                    }
                }
            }
        }
        return sp;
    }
}
}

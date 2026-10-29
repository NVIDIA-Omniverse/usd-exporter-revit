// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace UsdExporterRevitCore
{
internal static class SpatialElements
{
    private const string SpatialNamespace = "BIM:Spatial:";
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
        Transform t = null;
        Link link = null;
        if (linkInstance != null)
        {
            link = ExportManager.TryGetLink(linkInstance.Id.Value);
            t = linkInstance.GetTransform();
        }

        BuiltInCategory category = (typeof(T).Name.Equals("Space")) ? BuiltInCategory.OST_MEPSpaces : BuiltInCategory.OST_Rooms;
        string catName = (category == BuiltInCategory.OST_Rooms) ? "Rooms" : "Spaces";
        List<SpatialElement> elements = new FilteredElementCollector(doc).OfCategory(category).Cast<SpatialElement>().Where(e => e.Area > 0).ToList();

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
            if (_scheme != null)
            {
                schemeMaterials = MaterialManager.ProcessColorScheme(ExportManager.MaterialStage.Default, _scheme, catName + "_");
            }
            Prim geometryRoot = ExportManager.GetGeometryRoot(ExportManager.MainStage);
            if (geometryRoot.HasChild(UsdPrimNames.Instances))
            {
                instances = geometryRoot.GetChild(UsdPrimNames.Instances) as Scope;
            }
            else
            {
                instances = new Scope(ExportManager.MainStage.Id, UsdPrimNames.Instances, geometryRoot);
            }

            categoryScope = new Scope(ExportManager.MainStage.Id, catName, instances);
        }
        else // is a link
        {
            Stage linkStage = ExportManager.GetStage(link.StageId);
            Stage linkMaterialStage = ExportManager.GetStage(link.MaterialStageId);
            if (_scheme != null)
            {
                schemeMaterials = MaterialManager.ProcessColorScheme(linkMaterialStage.Default, _scheme, catName + "_");
            }
            else
            {
                schemeMaterials = MaterialManager.CopySpatialElementMaterials(ExportManager.MaterialStage.Id, link.MaterialStageId);
            }

            if (linkStage.Default.HasChild(UsdPrimNames.Instances))
            {
                instances = linkStage.Default.GetChild(UsdPrimNames.Instances) as Scope;
            }
            else
            {
                // Create the Instances scope on its parent prim's stage.
                instances = new Scope(linkStage.Default.StageId, UsdPrimNames.Instances, linkStage.Default);
            }
            // Create the category scope on the Instances scope's stage.
            categoryScope = new Scope(instances.StageId, catName, instances);
        }

        if (elements.Count > 0)
        {
            SpatialElementGeometryCalculator calc = new SpatialElementGeometryCalculator(doc);
            Dictionary<long, List<string>> openingRelationships = BuildOpeningRelationships(doc, elements);
            foreach (SpatialElement e in elements)
            {
                // Return to ExportView's cancellation boundary before releasing stages.
                if (Exporter.IsCancelled())
                    return;
                SpatialElementGeometryResults results;
                try
                {
                    results = calc.CalculateSpatialElementGeometry(e);
                }
                catch (Exception ex)
                {
                    string source = linkInstance == null ? "host document" : $"link instance {linkInstance.Id.GetValue()}";
                    usd.exporter.revit.log.warning($"Skipping {e.GetType().Name} {e.Id.GetValue()} in {source} \"{doc.Title}\" because Revit could not calculate its spatial geometry. {ex}");
                    continue;
                }
                Xform xform = new Xform(categoryScope.StageId, e.Id.GetValue().ToString(), PrimKind.Component, categoryScope);
                if (ExportManager.Settings.Options.IncludeBimData)
                {
                    xform.AddBIMData(e);
                    AddSpatialData(xform, e, results, openingRelationships);
                }

                xform.SetTransform(Transform.Identity);
                xform.SetPivot(ElementContext.GetPivotPoint(e));

                long matId = -1;
                Mesh mesh = null;
                string parameterValue = string.Empty;
                if (!string.IsNullOrEmpty(_schemeParam))
                {
                    var parameters = e.GetParameters(_schemeParam);
                    if (parameters.Count > 0)
                    {
                        parameterValue = parameters[0].AsValueString() ?? string.Empty;
                    }
                }
                if (schemeMaterials.TryGetValue(parameterValue, out Material outMat))
                {
                    matId = outMat.Id;
                }

                List<MeshData> data = new List<MeshData>();

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
                    List<Curve> edges = new List<Curve>() { edge0, edge1, edge2, edge3 };
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
                        mesh = new Mesh(xform.StageId, $"Mesh_{matId}", xform, data[0]);
                        MaterialManager.UseMaterial(ExportManager.MaterialStage.Id, matId);
                    }
                    else
                    {
                        mesh = new Mesh(xform.StageId, $"Mesh_{matId}", xform, data[0], link.LinkId);
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

    private static void AddSpatialData(Xform xform, SpatialElement element, SpatialElementGeometryResults geometryResults, Dictionary<long, List<string>> openingRelationships)
    {
        Document doc = element.Document;
        double linearScale = GetLinearScale();
        xform.AddBIMProperty(SpatialNamespace, "UniqueId", element.UniqueId);
        xform.AddBIMProperty(SpatialNamespace, "ElementId", element.Id.GetValue().ToString(CultureInfo.InvariantCulture));
        xform.AddBIMProperty(SpatialNamespace, "DocumentUniqueId", doc.ProjectInformation?.UniqueId ?? string.Empty);
        xform.AddBIMProperty(SpatialNamespace, "Category", element.Category?.Name ?? string.Empty);
        xform.AddBIMProperty(SpatialNamespace, "Name", element.Name);
        xform.AddBIMProperty(SpatialNamespace, "Number", GetPropertyValue(element, "Number"));
        AddElementReference(xform, "Level", doc, element.LevelId);
        AddElementReference(xform, "Phase", doc, GetSpatialElementPhaseId(element));

        DesignOption designOption = element.DesignOption;
        if (designOption != null)
        {
            AddElementReference(xform, "DesignOption", doc, designOption.Id);
        }

        xform.AddBIMProperty(SpatialNamespace, "LinearUnit", ExportManager.Settings.Options.UnitType.ToString());
        xform.AddBIMProperty(SpatialNamespace, "Area", (element.Area * linearScale * linearScale).ToString("R", CultureInfo.InvariantCulture));
        AddDoubleProperty(xform, element, "Volume", linearScale * linearScale * linearScale);
        AddDoubleProperty(xform, element, "Perimeter", linearScale);
        AddDoubleProperty(xform, element, "UnboundedHeight", linearScale);
        AddBoundaryData(xform, element, geometryResults, linearScale);
        AddOpeningRelationships(xform, element, openingRelationships);
    }

    private static void AddBoundaryData(Xform xform, SpatialElement element, SpatialElementGeometryResults geometryResults, double linearScale)
    {
        SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
        IList<IList<BoundarySegment>> loops = element.GetBoundarySegments(options);
        int loopCount = loops?.Count ?? 0;
        xform.AddBIMProperty(SpatialNamespace, "BoundaryLoopCount", loopCount.ToString(CultureInfo.InvariantCulture));
        HashSet<long> boundingElementIds = new HashSet<long>();
        HashSet<string> boundingElementUniqueIds = new HashSet<string>();

        for (int loopIndex = 0; loopIndex < loopCount; loopIndex++)
        {
            IList<BoundarySegment> loop = loops[loopIndex];
            string loopPrefix = $"BoundaryLoop{loopIndex}_";
            xform.AddBIMProperty(SpatialNamespace, loopPrefix + "SegmentCount", loop.Count.ToString(CultureInfo.InvariantCulture));
            for (int segmentIndex = 0; segmentIndex < loop.Count; segmentIndex++)
            {
                BoundarySegment segment = loop[segmentIndex];
                string segmentPrefix = loopPrefix + $"Segment{segmentIndex}_";
                ElementId boundaryId = segment.ElementId;
                ElementId linkedBoundaryId = segment.LinkElementId;
                ElementId sourceBoundaryId = linkedBoundaryId != ElementId.InvalidElementId ? linkedBoundaryId : boundaryId;
                long sourceBoundaryValue = sourceBoundaryId.GetValue();
                xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "ElementId", sourceBoundaryValue.ToString(CultureInfo.InvariantCulture));
                Element boundaryElement = element.Document.GetElement(boundaryId);
                Document boundaryDocument = element.Document;
                if (linkedBoundaryId != ElementId.InvalidElementId)
                {
                    RevitLinkInstance boundaryLink = boundaryElement as RevitLinkInstance;
                    boundaryDocument = boundaryLink?.GetLinkDocument();
                    boundaryElement = boundaryDocument?.GetElement(linkedBoundaryId);
                    xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "LinkInstanceElementId", boundaryId.GetValue().ToString(CultureInfo.InvariantCulture));
                    xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "LinkInstanceUniqueId", boundaryLink?.UniqueId ?? string.Empty);
                }
                if (sourceBoundaryId != ElementId.InvalidElementId)
                {
                    boundingElementIds.Add(sourceBoundaryValue);
                    if (boundaryElement != null)
                    {
                        boundingElementUniqueIds.Add(boundaryElement.UniqueId);
                        xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "UniqueId", boundaryElement.UniqueId);
                        xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "DocumentUniqueId", boundaryDocument?.ProjectInformation?.UniqueId ?? string.Empty);
                        xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "Category", boundaryElement.Category?.Name ?? string.Empty);
                        xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "Name", boundaryElement.Name);
                        Element boundaryType = boundaryDocument?.GetElement(boundaryElement.GetTypeId());
                        xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "Type", boundaryType?.Name ?? string.Empty);
                    }
                }

                Curve curve = segment.GetCurve();
                xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "CurveType", curve.GetType().Name);
                xform.AddBIMProperty(SpatialNamespace, segmentPrefix + "CurvePoints", FormatCurvePoints(curve, linearScale));
            }
        }

        AddFaceBoundingElements(element.Document, geometryResults, boundingElementIds, boundingElementUniqueIds);

        string ids = string.Join(",", boundingElementIds.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));
        xform.AddBIMProperty(SpatialNamespace, "BoundingElementIds", ids);
        xform.AddBIMProperty(SpatialNamespace, "BoundingElementUniqueIds", string.Join(",", boundingElementUniqueIds.OrderBy(id => id, StringComparer.Ordinal)));
    }

    private static void AddFaceBoundingElements(Document document, SpatialElementGeometryResults geometryResults, HashSet<long> boundingElementIds, HashSet<string> boundingElementUniqueIds)
    {
        foreach (Face face in geometryResults.GetGeometry().Faces)
        {
            IList<SpatialElementBoundarySubface> subfaces = geometryResults.GetBoundaryFaceInfo(face);
            if (subfaces == null)
            {
                continue;
            }
            foreach (SpatialElementBoundarySubface subface in subfaces)
            {
                LinkElementId boundaryId = subface.SpatialBoundaryElement;
                bool isLinked = boundaryId.LinkInstanceId != ElementId.InvalidElementId;
                ElementId sourceElementId = isLinked ? boundaryId.LinkedElementId : boundaryId.HostElementId;
                if (sourceElementId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Document sourceDocument = document;
                if (isLinked)
                {
                    RevitLinkInstance linkInstance = document.GetElement(boundaryId.LinkInstanceId) as RevitLinkInstance;
                    sourceDocument = linkInstance?.GetLinkDocument();
                }

                boundingElementIds.Add(sourceElementId.GetValue());
                Element sourceElement = sourceDocument?.GetElement(sourceElementId);
                if (sourceElement != null)
                {
                    boundingElementUniqueIds.Add(sourceElement.UniqueId);
                }
            }
        }
    }

    private static void AddOpeningRelationships(Xform xform, SpatialElement element, Dictionary<long, List<string>> openingRelationships)
    {
        if (element.GetType().Name != "Room")
        {
            return;
        }

        openingRelationships.TryGetValue(element.Id.GetValue(), out List<string> relationships);
        xform.AddBIMProperty(SpatialNamespace, "OpeningRelationships", string.Join(",", relationships ?? new List<string>()));
    }

    private static Dictionary<long, List<string>> BuildOpeningRelationships(Document doc, List<SpatialElement> elements)
    {
        Dictionary<long, List<string>> relationships = new Dictionary<long, List<string>>();
        List<Phase> phases = elements.Where(element => element.GetType().Name == "Room").Select(element => doc.GetElement(GetSpatialElementPhaseId(element)) as Phase).ToList();
        if (phases.Count == 0)
        {
            return relationships;
        }

        phases = phases.Where(phase => phase != null).Distinct().ToList();
        IEnumerable<FamilyInstance> instances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>();
        foreach (FamilyInstance instance in instances)
        {
            if (!IsDoorOrWindow(instance))
            {
                continue;
            }

            AddOpeningRelationship(relationships, instance, "FromRoom", GetRelatedSpatialElement(instance, "FromRoom", null));
            AddOpeningRelationship(relationships, instance, "ToRoom", GetRelatedSpatialElement(instance, "ToRoom", null));
            foreach (Phase phase in phases)
            {
                AddOpeningRelationship(relationships, instance, "FromRoom", GetRelatedSpatialElement(instance, "FromRoom", phase));
                AddOpeningRelationship(relationships, instance, "ToRoom", GetRelatedSpatialElement(instance, "ToRoom", phase));
            }
        }
        return relationships;
    }

    private static void AddOpeningRelationship(Dictionary<long, List<string>> relationships, FamilyInstance instance, string relation, SpatialElement spatialElement)
    {
        if (spatialElement == null)
        {
            return;
        }

        long roomId = spatialElement.Id.GetValue();
        if (!relationships.TryGetValue(roomId, out List<string> roomRelationships))
        {
            roomRelationships = new List<string>();
            relationships.Add(roomId, roomRelationships);
        }
        long hostId = instance.Host?.Id.GetValue() ?? ElementId.InvalidElementId.GetValue();
        string hostUniqueId = instance.Host?.UniqueId ?? string.Empty;
        string value = string.Join(
            ":",
            new[] { instance.Id.GetValue().ToString(CultureInfo.InvariantCulture),
                    relation,
                    hostId.ToString(CultureInfo.InvariantCulture),
                    SanitizeValue(instance.Category?.Name),
                    SanitizeValue(instance.Name),
                    instance.UniqueId,
                    hostUniqueId,
                    instance.Document.ProjectInformation?.UniqueId ?? string.Empty }
        );
        if (!roomRelationships.Contains(value))
        {
            roomRelationships.Add(value);
        }
    }

    private static SpatialElement GetRelatedSpatialElement(FamilyInstance instance, string propertyName, Phase phase)
    {
        object value = (phase == null) ? GetObjectProperty(instance, propertyName) : null;
        if (value == null && phase != null)
        {
            MethodInfo method = instance.GetType().GetMethod("get_" + propertyName, new[] { typeof(Phase) });
            value = method?.Invoke(instance, new object[] { phase });
        }
        return value as SpatialElement;
    }

    private static bool IsDoorOrWindow(FamilyInstance instance)
    {
        long categoryId = instance.Category?.Id.GetValue() ?? ElementId.InvalidElementId.GetValue();
        return categoryId == new ElementId(BuiltInCategory.OST_Doors).GetValue() || categoryId == new ElementId(BuiltInCategory.OST_Windows).GetValue();
    }

    private static ElementId GetSpatialElementPhaseId(SpatialElement element)
    {
        ElementId phaseId = element.get_Parameter(BuiltInParameter.ROOM_PHASE)?.AsElementId();
        return phaseId != null && phaseId != ElementId.InvalidElementId ? phaseId : element.CreatedPhaseId;
    }

    private static void AddElementReference(Xform xform, string name, Document doc, ElementId id)
    {
        if (id == null || id == ElementId.InvalidElementId)
        {
            return;
        }
        Element referencedElement = doc.GetElement(id);
        xform.AddBIMProperty(SpatialNamespace, name + "ElementId", id.GetValue().ToString(CultureInfo.InvariantCulture));
        xform.AddBIMProperty(SpatialNamespace, name + "UniqueId", referencedElement?.UniqueId ?? string.Empty);
        xform.AddBIMProperty(SpatialNamespace, name + "DocumentUniqueId", doc.ProjectInformation?.UniqueId ?? string.Empty);
        xform.AddBIMProperty(SpatialNamespace, name + "Name", referencedElement?.Name ?? string.Empty);
    }

    private static void AddDoubleProperty(Xform xform, object source, string propertyName, double scale)
    {
        object value = GetObjectProperty(source, propertyName);
        if (value is double parsed)
        {
            xform.AddBIMProperty(SpatialNamespace, propertyName, (parsed * scale).ToString("R", CultureInfo.InvariantCulture));
        }
    }

    private static double GetLinearScale()
    {
        return ExportManager.REVIT_DEFAULT_MPU / Stage.GetMetersPerUnitValue(ExportManager.Settings.Options.UnitType);
    }

    private static string GetPropertyValue(object source, string propertyName)
    {
        return GetObjectProperty(source, propertyName)?.ToString() ?? string.Empty;
    }

    private static object GetObjectProperty(object source, string propertyName)
    {
        PropertyInfo property = source.GetType().GetProperties().FirstOrDefault(candidate => candidate.Name == propertyName && candidate.GetIndexParameters().Length == 0);
        if (property == null)
        {
            return null;
        }
        return property.GetValue(source);
    }

    private static string FormatCurvePoints(Curve curve, double linearScale)
    {
        return string.Join(
            ";",
            curve.Tessellate().Select(
                point => string.Join(",", (point.X * linearScale).ToString("R", CultureInfo.InvariantCulture), (point.Y * linearScale).ToString("R", CultureInfo.InvariantCulture), (point.Z * linearScale).ToString("R", CultureInfo.InvariantCulture))
            )
        );
    }

    private static string SanitizeValue(string value)
    {
        return (value ?? string.Empty).Replace(":", "_").Replace(",", "_");
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

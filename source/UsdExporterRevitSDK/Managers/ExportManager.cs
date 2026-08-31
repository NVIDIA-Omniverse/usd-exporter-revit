// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal static class ExportManager
{
    public static Stage MainStage;
    public static Stage MaterialStage;
    public static UsdExporterRevitSettings Settings;

    private static Dictionary<long, Stage> references = new Dictionary<long, Stage>();

    private static Dictionary<string, Dictionary<long, Prim>> modelElementPrimMap = new Dictionary<string, Dictionary<long, Prim>>();
    private static Document mainDoc;

    // Reference to the CoordinateSystem prim that holds the coordinate transform
    // This is used as the parent for geometry-related scopes (Cameras, Instances, Prototypes)
    public static Xform CoordinateSystemPrim = null;

    public static readonly usd.exporter.revit.GfVec3d IdentityScale = new usd.exporter.revit.GfVec3d(1.0, 1.0, 1.0);
    public static readonly double[,] IdentityMatrix = new double[4, 4] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };
    public static readonly double RadiansToDegrees = 180.0 / Math.PI;
    public static readonly double REVIT_DEFAULT_MPU = 0.3048;

    public static readonly List<string> DoNotInstanceCategories = new List<string>() { "Curtain Wall Mullions", "Curtain Panels", "Parking" };

    public static string OvTempFolder = string.Empty;

    // Flag to indicate if section box is active during export
    public static bool IsSectionBoxActive = false;

    /// <summary>
    /// Clean up all stages by evicting them from the USD stage cache.
    /// This releases file handles so USD files can be deleted or modified.
    /// </summary>
    public static void CleanupStages()
    {
        usd.exporter.revit.core.clearStageCache();
    }

    public static void Initialize(Document doc, UsdExporterRevitSettings settings)
    {
        usd.exporter.revit.core.startup();
        usd.exporter.revit.core.startupLog();
        Settings = settings;
        references.Clear();
        MainStage = new Stage(settings.File.OutputFolder, settings.File.FileName, settings.File.Extension, settings.File.FileName, false);

        // Apply coordinate system transform directly to the default prim if needed
        ApplyCoordinateSystemTransformToDefaultPrim(doc, MainStage.Default, settings.Options.CoordinateSystem);

        if (settings.Options.MaterialStyle == MaterialStyle.InternalLibrary)
        {
            MaterialStage = MainStage;
        }
        else
        {
            MaterialStage = new Stage(settings.File.OutputFolder, settings.Options.MaterialFolderName, settings.File.Extension, settings.Options.MaterialFolderName, true);
            if (settings.Options.MaterialStyle == MaterialStyle.ExternalLibraryAsPayload)
            {
                MainStage.Default.AddStageReference(MaterialStage.Id, true);
            }
            else // reference
            {
                MainStage.Default.AddStageReference(MaterialStage.Id, false);
            }
        }

        modelElementPrimMap.Clear();
        mainDoc = doc;

        OvTempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ov\temp");
        if (!Directory.Exists(OvTempFolder))
        {
            Directory.CreateDirectory(OvTempFolder);
        }
        else
        {
            DirectoryInfo dir = new DirectoryInfo(OvTempFolder);
            foreach (DirectoryInfo d in dir.GetDirectories())
            {
                d.Delete(true);
            }
            foreach (FileInfo file in dir.GetFiles())
            {
                if (!file.Extension.Contains("rfa"))
                {
                    file.Delete();
                }
            }
        }
    }
    public static void AddReference(long id, Stage stage)
    {
        if (references.TryGetValue(id, out Stage existingStage))
        {
            references[id] = stage;
        }
        else
        {
            references.Add(id, stage);
        }
    }

    public static void SetVisibilityForStage(Stage stage)
    {
        // For MainStage, check geometry root (CoordinateSystem if it exists), otherwise default prim
        // For link stages, use default prim directly
        Prim rootPrim = (stage.Id == MainStage.Id) ? GetGeometryRoot(stage) : stage.Default;

        if (rootPrim.HasChild("Instances"))
        {
            Prim instances = rootPrim.GetChild("Instances");
            if (instances.HasChild("Rooms"))
            {
                Prim rooms = instances.GetChild("Rooms");
                pxr.usd.prim.setVisibility(stage.Id, rooms.Path, false);
            }
            if (instances.HasChild("Spaces"))
            {
                Prim spaces = instances.GetChild("Spaces");
                pxr.usd.prim.setVisibility(stage.Id, spaces.Path, false);
            }
        }
        if (stage.Default.HasChild("Drawings"))
        {
            Prim drawings = stage.Default.GetChild("Drawings");
            pxr.usd.prim.setVisibility(stage.Id, drawings.Path, false);
        }
        if (rootPrim.HasChild("Cameras"))
        {
            Prim cameras = rootPrim.GetChild("Cameras");
            pxr.usd.prim.setVisibility(stage.Id, cameras.Path, false);
        }
    }
    public static Stage TryGetStage(long stageId)
    {
        Stage stage = null;
        if (MainStage.Id == stageId)
        {
            return MainStage;
        }
        else if (MaterialStage.Id == stageId)
        {
            return MaterialStage;
        }
        else
        {
            references.TryGetValue(stageId, out stage);
        }
        return stage;
    }

    public static Link TryGetLink(long elementId)
    {
        // Search MainStage.Links dictionary directly for constant time lookup
        if (MainStage.Links.TryGetValue(elementId, out Link link))
        {
            return link;
        }

        // Then check nested link stages
        foreach (KeyValuePair<long, Link> l in MainStage.Links)
        {
            Stage linkStage = TryGetStage(l.Value.StageId);
            if (linkStage != null && linkStage.Links.TryGetValue(elementId, out Link nestedLink))
            {
                return nestedLink;
            }
        }
        return null;
    }

    public static ClassPrim TryGetClass(long stageId, long elementId, Dictionary<long, double> mats)
    {
#if DEBUG && EXPORT_MANAGER
        usd.exporter.revit.log.info($"Getting Class in stage {stageId} for type {elementId}");
        foreach (KeyValuePair<long, double> m in mats)
        {
            usd.exporter.revit.log.info($"input mat--> id: {m.Key}, area: {m.Value}");
        }
#endif
        ClassPrim _class = null;
        if (MainStage.Id == stageId)
        {
#if DEBUG && EXPORT_MANAGER
            usd.exporter.revit.log.info("Looking in main stage");
#endif
            List<ClassPrim> classes = new List<ClassPrim>();
            foreach (ClassPrim cp in MainStage.ClassPrims)
            {
                if (cp.FamilyTypeId == elementId)
                {
                    classes.Add(cp);
                }
            }

            if (classes.Count > 0)
            {
                if (Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses)
                {
                    foreach (ClassPrim c in classes)
                    {
                        List<Mesh> meshes = c.Instance.Children.Where(p => p is Mesh).Cast<Mesh>().ToList();
                        int missing = 0;
                        foreach (KeyValuePair<long, double> mat in mats)
                        {
                            // When section box is active, skip material area comparison because cropping
                            // can cause the same family type to have different material areas
                            if (IsSectionBoxActive)
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
                        if (missing == 0 || mats.Count == 0)
                        {
                            _class = c;
                        }
                        else if (meshes.Count + missing == mats.Count)
                        {
                            _class = c;
                        }
                    }
                }
                else
                {
                    if (classes.Count == 1)
                    {
                        _class = classes.First();
                    }
                    else
                    {
                        usd.exporter.revit.log.warning($"multiple classes found for Family {elementId}");
                    }
                }
            }
        }
        else
        {
#if DEBUG && EXPORT_MANAGER
            usd.exporter.revit.log.info("Looking in link stage");
#endif
            foreach (KeyValuePair<long, Link> link in MainStage.Links)
            {
                if (link.Value.StageId == stageId)
                {
                    Stage stage = TryGetStage(link.Value.StageId);
                    if (stage != null)
                    {
                        List<ClassPrim> classes = new List<ClassPrim>();
                        foreach (ClassPrim cp in stage.ClassPrims)
                        {
                            if (cp.FamilyTypeId == elementId)
                            {
                                classes.Add(cp);
                            }
                        }

                        if (classes.Count > 0)
                        {
                            if (Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses)
                            {
                                foreach (ClassPrim c in classes)
                                {
                                    List<Mesh> meshes = c.Instance.Children.Where(p => p is Mesh).Cast<Mesh>().ToList();
                                    int missing = 0;
                                    foreach (KeyValuePair<long, double> mat in mats)
                                    {
                                        // When section box is active, skip material area comparison because cropping
                                        // can cause the same family type to have different material areas
                                        if (IsSectionBoxActive)
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
                                    if (missing == 0 || mats.Count == 0)
                                    {
                                        _class = c;
                                    }
                                    else if (meshes.Count + missing == mats.Count)
                                    {
                                        _class = c;
                                    }
                                }
                            }
                            else
                            {
                                foreach (ClassPrim c in classes)
                                {
                                    string optionName;
                                    if (c.Family.VariantSet.HasOption(elementId, mats, out optionName))
                                    {
                                        _class = c;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
#if DEBUG && EXPORT_MANAGER
        string nullString = (_class == null) ? "NULL, match not found" : $"at path {_class.Path}";
        usd.exporter.revit.log.info($"return class " + nullString);
#endif
        return _class;
    }

    public static Family TryGetFamily(long stageId, long elementId)
    {
        if (MainStage.Id == stageId)
        {
            // Search MainStage.Families dictionary directly for constant time lookup
            if (MainStage.Families.TryGetValue(elementId, out Family family))
            {
                return family;
            }
        }
        else
        {
            foreach (KeyValuePair<long, Link> link in MainStage.Links)
            {
                if (link.Value.StageId == stageId)
                {
                    Stage stage = TryGetStage(link.Value.StageId);
                    if (stage != null)
                    {
                        if (stage.Families.TryGetValue(elementId, out Family family))
                        {
                            return family;
                        }
                    }
                }
            }
        }
        return null;
    }

    public static Document GetMainDocument()
    {
        return mainDoc;
    }
    public static void AddElement(string modelName, long id, Prim prim)
    {
        if (!modelElementPrimMap.TryGetValue(modelName, out Dictionary<long, Prim> modelDict))
        {
            modelElementPrimMap.Add(modelName, new Dictionary<long, Prim>());
        }
        modelElementPrimMap[modelName][id] = prim;
    }
    public static Prim GetPrim(string modelName, long id)
    {
        if (modelElementPrimMap.TryGetValue(modelName, out Dictionary<long, Prim> modelDict))
        {
            if (modelDict.TryGetValue(id, out Prim prim))
            {
                return prim;
            }
        }
        return null;
    }

    public static Scope GetFamilyPrototypeScope(long stageId, Element element)
    {
        Scope scope = null;
        Stage stage = TryGetStage(stageId);
        if (stage != null)
        {
            Prim rootPrim = GetGeometryRoot(stage);
            if (rootPrim.HasChild("Prototypes"))
            {
                Scope proto = rootPrim.GetChild("Prototypes") as Scope;
                if (proto != null)
                {
                    if (proto.HasChild(element.Category.Name))
                    {
                        Scope category = proto.GetChild(element.Category.Name) as Scope;
                        if (category != null)
                        {
                            if (ExportManager.Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses)
                            {
                                ElementType t = element.Document.GetElement(element.GetTypeId()) as ElementType;
                                if (t != null)
                                {
                                    if (category.HasChild(t.FamilyName))
                                    {
                                        scope = category.GetChild(t.FamilyName) as Scope;
                                    }
                                }
                            }
                            else
                            {
                                scope = category;
                            }
                        }
                    }
                }
            }
        }
        return scope;
    }

    public static void CreatePrototypeBranch(Element element, Prim root)
    {
        if (element != null && element.Category != null)
        {
            Scope proto = null;
            if (root.HasChild("Prototypes"))
            {
                proto = root.GetChild("Prototypes") as Scope;
            }
            else
            {
                proto = new Scope(root.StageId, "Prototypes", root);
            }
            if (proto != null)
            {
                Scope category = null;
                if (proto.HasChild(element.Category.Name))
                {
                    category = proto.GetChild(element.Category.Name) as Scope;
                }
                else
                {
                    category = new Scope(proto.StageId, element.Category.Name, proto);
                }
                if (ExportManager.Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses)
                {
                    if (category != null)
                    {
                        ElementType t = element.Document.GetElement(element.GetTypeId()) as ElementType;
                        if (t != null)
                        {
                            if (!category.HasChild(t.FamilyName))
                            {
                                // We are not storing the class, but we are leaving it in because it is necessary.
                                new Scope(category.StageId, t.FamilyName, category); // NOSONAR
                            }
                        }
                    }
                }
            }
        }
    }

    public static Prim CreateXformBranch(Element element, Prim root, long linkId = -1)
    {
        Prim prim = null;
        if (element != null && element.Category != null)
        {
            Scope instances = null;
            Scope category = null;
            if (element.Category.Name == "RVT Links")
            {
                return ExportManager.GetPrim(element.Document.Title, element.Id.GetValue());
            }
            else
            {
                if (root.HasChild("Instances"))
                {
                    instances = root.GetChild("Instances") as Scope;
                }
                else
                {
                    instances = new Scope(root.StageId, "Instances", root);
                }
                if (instances != null)
                {
                    if (instances.HasChild(element.Category.Name))
                    {
                        category = instances.GetChild(element.Category.Name) as Scope;
                    }
                    else
                    {
                        category = new Scope(root.StageId, element.Category.Name, instances);
                    }
                }
            }

            if (category != null)
            {
                ElementType t = element.Document.GetElement(element.GetTypeId()) as ElementType;
                if (t != null)
                {
                    Scope family = null;
                    if (category.HasChild(t.FamilyName))
                    {
                        family = category.GetChild(t.FamilyName) as Scope;
                    }
                    else
                    {
                        family = new Scope(category.StageId, t.FamilyName, category);
                    }
                    if (family != null)
                    {
                        if (element is FamilyInstance && !DoNotInstanceCategories.Contains(element.Category.Name) && Settings.Options.InstanceFamilies &&
                            (Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsReference || Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsPayload))
                        {
                            prim = createXform(element, family, linkId);
                        }
                        else
                        {
                            Scope familyType = null;
                            if (family.HasChild(t.Name))
                            {
                                familyType = family.GetChild(t.Name) as Scope;
                            }
                            else
                            {
                                familyType = new Scope(family.StageId, t.Name, family);
                            }
                            if (familyType != null)
                            {
                                prim = createXform(element, familyType, linkId);
                            }
                        }
                    }
                }
                else
                {
                    // some things like toposolids may not have family types
                    prim = createXform(element, category, linkId);
                }
            }
        }
        return prim;
    }

    private static Xform createXform(Element element, Prim parent, long linkId = -1)
    {
        Xform xform = null;
        if (parent.HasChild(element.Id.GetValue().ToString()))
        {
            xform = parent.GetChild(element.Id.GetValue().ToString()) as Xform;
        }
        else
        {
            xform = new Xform(parent.StageId, element.Id.GetValue().ToString(), PrimKind.Component, parent);
            if (ExportManager.Settings.Options.IncludeBimData)
            {
                xform.AddBIMData(element);
            }
            ExportManager.AddElement(element.Document.Title, element.Id.GetValue(), xform);
        }
        if (ExportManager.IsCylinder(element))
        {
            Location location = element.Location;
            if (location is LocationCurve)
            {
                LocationCurve lc = (LocationCurve)location;
                Curve curve = lc.Curve;
                if (curve is Line)
                {
                    Line line = (Line)curve;
                    XYZ start = line.GetEndPoint(0);
                    XYZ end = line.GetEndPoint(1);
                    double radius = 1.0;
                    switch (element.Category.Name)
                    {
                        case "Conduits":
                            Parameter outerDiameter = element.GetParameters("Outside Diameter")[0];
                            radius = outerDiameter.AsDouble() / 2.0;
                            break;
                        default:
                            Parameter p = element.GetParameters("Diameter")[0];
                            radius = p.AsDouble() / 2.0;
                            break;
                    }
                    Cylinder cylinder = new Cylinder(
                        xform.StageId,
                        "Cylinder",
                        xform,
                        new usd.exporter.revit.GfVec3f((float)start.X, (float)start.Y, (float)start.Z),
                        new usd.exporter.revit.GfVec3f((float)end.X, (float)end.Y, (float)end.Z),
                        radius,
                        linkId
                    ); // material is picked up
                       // during revit export
                }
            }
        }
        return xform;
    }

    public static List<MeshData> GetSolidMeshData(Solid solid, long materialId, double area, Transform t = null)
    {
        List<MeshData> data = new List<MeshData>();
        foreach (Face face in solid.Faces)
        {
            Autodesk.Revit.DB.Mesh m = face.Triangulate();
            if (m != null)
            {
                data.Add(GetMeshData(m, materialId, area, t));
            }
        }
        return data;
    }

    public static MeshData GetMeshData(Autodesk.Revit.DB.Mesh mesh, long materialId, double area, Transform t = null)
    {
        MeshData data = null;
        if (mesh != null)
        {
            List<XYZ> points = new List<XYZ>();
            List<XYZ> normals = new List<XYZ>();
            List<UV> primvars_st = new List<UV>();
            List<int> faceVertexIndices = new List<int>();
            List<int> faceVertexCounts = new List<int>();

            // Transform and normalize normals
            if (t != null)
            {
                normals.AddRange(mesh.GetNormals().Select(n => t.OfVector(n).Normalize()));
            }
            else
            {
                normals.AddRange(mesh.GetNormals().Select(n => n.Normalize()));
            }
            for (int i = 0; i < mesh.NumTriangles; i++)
            {
                MeshTriangle tri = mesh.get_Triangle(i);
                XYZ p0 = (t == null) ? tri.get_Vertex(0) : t.OfPoint(tri.get_Vertex(0));
                XYZ p1 = (t == null) ? tri.get_Vertex(1) : t.OfPoint(tri.get_Vertex(1));
                XYZ p2 = (t == null) ? tri.get_Vertex(2) : t.OfPoint(tri.get_Vertex(2));
                points.Add(p0);
                faceVertexIndices.Add(points.Count - 1);
                primvars_st.Add(UV.Zero);
                points.Add(p1);
                faceVertexIndices.Add(points.Count - 1);
                primvars_st.Add(UV.Zero);
                points.Add(p2);
                faceVertexIndices.Add(points.Count - 1);
                primvars_st.Add(UV.Zero);
                faceVertexCounts.Add(3);
            }
            data = new MeshData(points, normals, primvars_st, faceVertexIndices, faceVertexCounts, materialId, area);
        }
        return data;
    }

    public static void SetTemporaryViewSettings(View view, UIDocument uiDoc)
    {
        Document doc = view.Document;

        List<PhaseFilter> phasesFilters = new FilteredElementCollector(doc).OfClass(typeof(PhaseFilter)).OfType<PhaseFilter>().ToList();
        List<string> phaseFilterNames = phasesFilters.Select(p => p.Name).ToList();
        string phaseFilterName = ExportManager.Settings.GetStringMatch(UsdExporterRevitSettingType.PhaseFilter, phaseFilterNames);

        List<View3D> viewTemplates = new FilteredElementCollector(doc).OfClass(typeof(View3D)).OfType<View3D>().Where(v => v.IsTemplate).ToList();
        List<string> viewTemplateNames = viewTemplates.Select(v => v.Name).ToList();
        string viewTemplateName = ExportManager.Settings.GetStringMatch(UsdExporterRevitSettingType.ViewTemplate, viewTemplateNames);

        using (Transaction t = new Transaction(doc))
        {
            if (t.Start("Set Temporary View Settings for Export") == TransactionStatus.Started)
            {
                view.EnableTemporaryViewPropertiesMode(view.Id);
                if (!string.IsNullOrEmpty(ExportManager.Settings.View.DetailLevel))
                {
                    switch (ExportManager.Settings.View.DetailLevel)
                    {
                        case "Fine":
                            view.DetailLevel = ViewDetailLevel.Fine;
                            break;
                        case "Medium":
                            view.DetailLevel = ViewDetailLevel.Medium;
                            break;
                        case "Coarse":
                            view.DetailLevel = ViewDetailLevel.Coarse;
                            break;
                        default:
                            view.DetailLevel = ViewDetailLevel.Fine;
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(ExportManager.Settings.View.PhaseFilter) && !string.IsNullOrEmpty(phaseFilterName))
                {
                    PhaseFilter filter = phasesFilters.Where(p => p.Name == phaseFilterName).FirstOrDefault();
                    List<Parameter> parameters = view.GetParameters("Phase Filter").ToList();
                    if (filter != null && parameters.Count > 0)
                    {
                        parameters[0].Set(filter.Id);
                    }
                }
                if (!string.IsNullOrEmpty(ExportManager.Settings.View.ViewTemplate) && !string.IsNullOrEmpty(viewTemplateName))
                {
                    View3D template = viewTemplates.Where(v => v.Name == viewTemplateName).FirstOrDefault();
                    if (template != null)
                    {
                        view.ViewTemplateId = template.Id;
                    }
                }
            }
            t.Commit();
            uiDoc.RefreshActiveView();
        }
    }

    public static void RemoveTemporaryViewSettings(View view, UIDocument uiDoc)
    {
        Document doc = view.Document;
        using (Transaction t = new Transaction(doc))
        {
            if (t.Start($"Restore {view.Name}") == TransactionStatus.Started)
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
                view.EnableTemporaryViewPropertiesMode(ElementId.InvalidElementId);
                doc.Regenerate();
            }
            t.Commit();
            uiDoc.RefreshActiveView();
        }
    }

    private readonly static List<string> lineBasedCategories = new List<string>() { "Pipes", "Conduits", "Ducts" };

    private static bool isLineBased(Element element)
    {
        bool isLine = false;
        Location location = element.Location;
        if (location != null)
        {
            if (location is LocationCurve)
            {
                LocationCurve curve = (LocationCurve)location;
                if (curve.Curve is Line)
                {
                    isLine = true;
                }
            }
        }
        return isLine;
    }
    public static bool IsCylinder(Element element)
    {
        bool isCylinder = false;
        if (element.Category != null)
        {
            if (lineBasedCategories.Contains(element.Category.Name))
            {
                switch (element.Category.Name)
                {
                    case "Pipes":
                        return isLineBased(element);
                    case "Conduits":
                        return isLineBased(element);
                    case "Ducts":
                        ElementId typeId = element.GetTypeId();
                        Element e = element.Document.GetElement(typeId);
                        if (e is DuctType)
                        {
                            DuctType ductType = e as DuctType;
                            if (ductType.Shape == ConnectorProfileType.Round)
                            {
                                return isLineBased(element);
                            }
                        }
                        break;
                    default:
                        return false;
                }
            }
        }
        return isCylinder;
    }

    public static long GetValue(this ElementId id)
    {
        return id.Value;
    }

    public static List<XYZ> GetMinMax(this BoundingBoxXYZ bbox, Transform t = null)
    {
        XYZ min;
        XYZ max;
        if (t != null)
        {
            min = t.Inverse.OfPoint(bbox.Transform.OfPoint(bbox.Min));
            max = t.Inverse.OfPoint(bbox.Transform.OfPoint(bbox.Max));
        }
        else
        {
            min = bbox.Transform.OfPoint(bbox.Min);
            max = bbox.Transform.OfPoint(bbox.Max);
        }
        double minX = (min.X < max.X) ? min.X : max.X;
        double minY = (min.Y < max.Y) ? min.Y : max.Y;
        double minZ = (min.Z < max.Z) ? min.Z : max.Z;
        double maxX = (min.X > max.X) ? min.X : max.X;
        double maxY = (min.Y > max.Y) ? min.Y : max.Y;
        double maxZ = (min.Z > max.Z) ? min.Z : max.Z;
        min = new XYZ(minX, minY, minZ);
        max = new XYZ(maxX, maxY, maxZ);
        return new List<XYZ>() { min, max };
    }

    private static List<char> theBaddies = new List<char>() { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#' };

    public static string RemoveBadWindowsFilePathChars(this string value)
    {
        foreach (char c in theBaddies)
        {
            value = value.Replace(c, '_');
        }
        return value;
    }

#if DEBUG
    public static void Log(this Transform t)
    {
        usd.exporter.revit.log.info($"[{t.BasisX.X},{t.BasisX.Y},{t.BasisX.Z},0]");
        usd.exporter.revit.log.info($"[{t.BasisY.X},{t.BasisY.Y},{t.BasisY.Z},0]");
        usd.exporter.revit.log.info($"[{t.BasisZ.X},{t.BasisZ.Y},{t.BasisZ.Z},0]");
        usd.exporter.revit.log.info($"[{t.Origin.X},{t.Origin.Y},{t.Origin.Z},1]");
    }
#endif
    public static string ToString(this Transform t, int tabCount = 0)
    {
        string tabs = string.Empty;
        for (int i = 0; i < tabCount; i++)
        {
            tabs += "\t";
        }
        string output = string.Empty;
        output += tabs + "Transform {\n";
        string closure = tabs + "}\n";
        tabs += "\t";
        output += $"{tabs}[{t.BasisX.X},{t.BasisX.Y},{t.BasisX.Z},0]\n";
        output += $"{tabs}[{t.BasisY.X},{t.BasisY.Y},{t.BasisY.Z},0]\n";
        output += $"{tabs}[{t.BasisZ.X},{t.BasisZ.Y},{t.BasisZ.Z},0]\n";
        output += $"{tabs}[{t.Origin.X},{t.Origin.Y},{t.Origin.Z},1]\n";
        output += closure;
        return output;
    }

    /// <summary>
    /// Gets the transform from internal origin to a base point (survey point or project base point).
    /// The transform includes both translation and rotation from the base point's location.
    /// </summary>
    /// <param name="basePoint">The base point (survey point or project base point). Can be null.</param>
    /// <returns>The transform from internal origin to the base point, or Identity if the base point is null or invalid.</returns>
    private static Transform GetTransformFromBasePoint(BasePoint basePoint)
    {
        if (basePoint == null)
        {
            return Transform.Identity;
        }

        // Get position directly from BasePoint.Position property
        XYZ position = basePoint.Position;
        if (position == null)
        {
            return Transform.Identity;
        }

        // Get rotation from Location if available
        double rotation = 0.0;
        Location location = basePoint.Location;
        if (location != null)
        {
            LocationPoint locationPoint = location as LocationPoint;
            if (locationPoint != null)
            {
                rotation = locationPoint.Rotation;
            }
        }

        // Create transform from position and rotation
        Transform translation = Transform.CreateTranslation(position);
        Transform rotationTransform = Transform.CreateRotation(XYZ.BasisZ, rotation);
        Transform result = translation.Multiply(rotationTransform);
        return result;
    }

    /// <summary>
    /// Applies the coordinate system transform by creating a child prim under the default prim.
    /// The default prim stays at identity (0,0,0) as the asset root, while the coordinate
    /// system transform is applied to a child prim. This keeps predictable asset-space behavior
    /// with the logical origin at the default prim.
    /// </summary>
    private static void ApplyCoordinateSystemTransformToDefaultPrim(Document doc, Xform defaultPrim, int coordinateSystem)
    {
        // Validate inputs to prevent ArgumentNullException
        if (doc == null)
        {
            usd.exporter.revit.log.warning("Document is null. Cannot apply coordinate system transform.");
            return;
        }

        if (defaultPrim == null)
        {
            usd.exporter.revit.log.warning("Default prim is null. Cannot apply coordinate system transform.");
            return;
        }

        if (coordinateSystem == 0) // InternalOrigin
        {
            // no custom transformation needed
            return;
        }

        Transform coordinateTransform = Transform.Identity;
        string coordinateSystemName = "coordinate_system"; // Default name

        if (coordinateSystem == 1) // Project Base Point
        {
            coordinateSystemName = "project_base_point";
            BasePoint projectBasePoint = BasePoint.GetProjectBasePoint(doc);
            Transform baseTransform = GetTransformFromBasePoint(projectBasePoint);
            // Invert the transform so that the project base point becomes the origin in USD
            coordinateTransform = baseTransform.Inverse;
        }
        else if (coordinateSystem == 2) // SurveyPoint
        {
            coordinateSystemName = "survey_point";
            BasePoint surveyPoint = BasePoint.GetSurveyPoint(doc);
            Transform baseTransform = GetTransformFromBasePoint(surveyPoint);
            // Invert the transform so that the survey point becomes the origin in USD
            coordinateTransform = baseTransform.Inverse;
        }
        else if (coordinateSystem == 3) // SharedCoordinates
        {
            coordinateSystemName = "shared_coordinates";
            ProjectLocation projectLocation = doc.ActiveProjectLocation;
            if (projectLocation != null)
            {
                coordinateTransform = projectLocation.GetTotalTransform();
                // Invert the transform so that the shared coordinate becomes the origin in USD
                coordinateTransform = coordinateTransform.Inverse;
            }
        }

        if (!coordinateTransform.IsIdentity)
        {
            // Create a child prim under the default prim with the coordinate system transform
            // The default prim stays at identity (0,0,0) as the asset root
            CoordinateSystemPrim = new Xform(defaultPrim.StageId, coordinateSystemName, PrimKind.Assembly, defaultPrim);
            CoordinateSystemPrim.SetTransform(coordinateTransform);
            CoordinateSystemPrim.Active = true;
        }
        else
        {
            // No coordinate system transform, so CoordinateSystemPrim is null
            // Content will be added directly to default prim
            CoordinateSystemPrim = null;
        }
    }

    /// <summary>
    /// Gets the appropriate root prim for geometry-related content.
    /// Returns CoordinateSystemPrim if it exists (non-identity coordinate system),
    /// otherwise returns the default prim (InternalOrigin case).
    /// </summary>
    public static Prim GetGeometryRoot(Stage stage)
    {
        if (CoordinateSystemPrim != null && CoordinateSystemPrim.StageId == stage.Id)
        {
            return CoordinateSystemPrim;
        }
        return stage.Default;
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Lighting;
using Autodesk.Revit.DB.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
internal class ExportContext : IExportContext, IDisposable
{
    private Prim _prim = null;
    private Element _element = null;
    private double _area = 0;
    private ElementId _materialId = null;
    private Stage _stage = null;
    private Document _doc = null;
    bool inLink = false;
    Link _link = null;
    private ProgressUpdate _progressUpdate = null;

    // flags for knowing if we are in a nested instance
    bool inInstance = false;
    bool inNestedInstance = false;

    List<ElementContext> _ec = new List<ElementContext>();

    List<Transform> _transforms = new List<Transform>() { Transform.Identity };

    public ExportContext()
    {
        _progressUpdate = new ProgressUpdate();
    }
    public void Dispose()
    {
    }

    public void Finish()
    {
    }

    public bool IsCanceled()
    {
        return Exporter.IsCancelled();
    }

    public RenderNodeAction OnElementBegin(ElementId elementId)
    {
        _element = _doc.GetElement(elementId);
        if (_element == null)
        {
            return RenderNodeAction.Skip;
        }

        // Check if element is Arc curve, if so, return skip because it's unsupported
        if (_element is CurveElement)
        {
            Curve curve = null;
            if (_element is ModelCurve modelCurve)
            {
                curve = modelCurve.GeometryCurve;
            }
            else if (_element is DetailCurve detailCurve)
            {
                curve = detailCurve.GeometryCurve;
            }
            if (curve is Arc)
            {
                usd.exporter.revit.log.warning($"Arc curve is unsupported, skipping element {_element.Id.GetValue()}");
                return RenderNodeAction.Skip;
            }
        }

        var parameters = _element.GetParameters("Area");
        _area = 0;
        if (parameters.Count > 0)
        {
            // ensure parameter is a double before using it
            Parameter areaParam = parameters[0];
            if (areaParam.HasValue && areaParam.StorageType == StorageType.Double)
            {
                _area = areaParam.AsDouble();
            }
        }

#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"{elementId.GetValue()} {_element.Category.Name} {_element.Name}");
#endif
        if (inLink)
        {
            // Use geometry root for MainStage (to apply coordinate system transform),
            // but use default prim directly for link stages (they're separate assets)
            Prim rootPrim = (_stage.Id == ExportManager.MainStage.Id) ? ExportManager.GetGeometryRoot(_stage) : _stage.Default;
            _prim = ExportManager.CreateXformBranch(_element, rootPrim, _link.LinkId);
            if (ExportManager.Settings.Options.InstanceFamilies && _element is FamilyInstance)
            {
                ExportManager.CreatePrototypeBranch(_element, rootPrim);
            }
        }
        else
        {
            _prim = ExportManager.GetPrim(_doc.Title, elementId.GetValue());
        }
        if (_prim == null || _element == null)
        {
            return RenderNodeAction.Skip;
        }
        _ec.Add(new ElementContext(_element, _prim, Transform.Identity));
        return RenderNodeAction.Proceed;
    }

    public void OnElementEnd(ElementId elementId)
    {
        if (_element == null)
        {
#if DEBUG && EXPORT_CONTEXT
            usd.exporter.revit.log.info("Element already cleared, probably was a link");
#endif
            return;
        }
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"{elementId.GetValue()}");
#endif
        if (_ec.Count > 0)
        {
#if DEBUG && EXPORT_CONTEXT
            usd.exporter.revit.log.info(_ec.Last().ToString());
#endif
            ElementContext ec = _ec.Last();

            // if it shouldn't be instanced, make a regular mesh
            if (!ec.ShouldInstance())
            {
                // if it is an instance, we use the prototype mesh (centered around the origin) and transform later
                List<MeshData> meshData = ec.IsInstance() ? ec.GetPrototypeMeshData() : ec.Collapse();
                foreach (MeshData m in meshData)
                {
                    if (_link == null)
                    {
                        Mesh mesh = new Mesh(ec.Prim.StageId, $"Mesh_{m.MaterialId}", ec.Prim, m);
                        setCastShadows(mesh, m.MaterialId, ExportManager.MaterialStage.Id);
                    }
                    else
                    {
                        Mesh mesh = new Mesh(ec.Prim.StageId, $"Mesh_{m.MaterialId}", ec.Prim, m, _link.LinkId);
                        setCastShadows(mesh, m.MaterialId, _link.MaterialStageId);
                    }
                }
            }
            else // do instancing
            {
                List<MeshData> meshData = ec.GetPrototypeMeshData();
                Dictionary<long, double> mats = meshData.ToDictionary(m => m.MaterialId, m => m.MaterialArea);

                // internal class workflow
                if (ExportManager.Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses)
                {
                    ClassPrim classPrim = ExportManager.TryGetClass(ec.Prim.StageId, ec.TypeId, mats);
                    if (classPrim != null)
                    {
                        if (classPrim.Active)
                        {
                            ec.Prim.AddInternalReference(classPrim.Instance.Path);
                            ec.Prim.Instanceable = true;
                        }
                    }
                    else
                    {
                        Scope familyProto = ExportManager.GetFamilyPrototypeScope(ec.Prim.StageId, _element);
                        if (familyProto == null)
                        {
                            Prim rootPrim = (_stage.Id == ExportManager.MainStage.Id) ? ExportManager.GetGeometryRoot(_stage) : _stage.Default;
                            ExportManager.CreatePrototypeBranch(_element, rootPrim);
                            familyProto = ExportManager.GetFamilyPrototypeScope(ec.Prim.StageId, _element);
                            if (familyProto == null)
                            {
                                usd.exporter.revit.log.error(ec.Prim.Path);
                            }
                        }
                        classPrim = new ClassPrim(_stage.Id, ec.TypeName + "_" + familyProto.Children.Count, familyProto, ec.TypeId, ClassHolding.InternalFamilyType);

                        foreach (MeshData m in meshData)
                        {
                            // define meshes
                            if (!inLink)
                            {
                                Mesh mesh = new Mesh(classPrim.StageId, $"Mesh_{m.MaterialId}", classPrim.Instance, m);
                                setCastShadows(mesh, m.MaterialId, ExportManager.MaterialStage.Id);
                            }
                            else
                            {
                                Mesh mesh = new Mesh(classPrim.StageId, $"Mesh_{m.MaterialId}", classPrim.Instance, m, _link.LinkId);
                                setCastShadows(mesh, m.MaterialId, _link.MaterialStageId);
                            }

                            // define materials
                            Material source = (!inLink) ? MaterialManager.GetMaterial(ExportManager.MaterialStage.Id, m.MaterialId) : MaterialManager.GetMaterial(_link.MaterialStageId, m.MaterialId);
                            Material copy = source.CopyTo(classPrim.MaterialScope);
                            copy.InUse = true;
                            copy.ActivateBranch();
                        }

                        // add reference to the instance
                        _stage.ClassPrims.Add(classPrim);
                        ec.Prim.AddInternalReference(classPrim.Instance.Path);
                        ec.Prim.Instanceable = true;
                    }
                }
                else // external asset workflow
                {
                    ClassPrim classPrim = ExportManager.TryGetClass(_stage.Id, ec.Parts.First().FamilyId, mats);
                    Family family = ExportManager.TryGetFamily(_stage.Id, ec.Parts.First().FamilyId);
                    // construct the class if missing
                    if (classPrim == null)
                    {
                        Scope familyProto = ExportManager.GetFamilyPrototypeScope(ec.Prim.StageId, _element);
                        classPrim = new ClassPrim(_stage.Id, ec.FamilyName, familyProto, ec.Parts.First().FamilyId, ClassHolding.ExternalFamilyTypeVariant);
                        _stage.ClassPrims.Add(classPrim);
                    }
                    // construct the family if missing
                    if (family == null)
                    {
                        string pathsafe = ec.Parts.First().FamilyName;
                        pathsafe = (pathsafe.Length > 20) ? pathsafe.Substring(0, 20) : pathsafe; // some of these family names are realllllly long
                        pathsafe = pathsafe.TrimEnd();
                        pathsafe += "_" + ec.Parts.First().FamilyId.ToString(); // add family id to ensure uniqueness
                        family = new Family(ec.Parts.First().FamilyId, _stage.FolderPath + "/Families/" + pathsafe, pathsafe, _stage.Extension, ec.FamilyName);
                        classPrim.Family = family;
                        _stage.Families.Add(ec.Parts.First().FamilyId, classPrim.Family);
                    }

                    if (classPrim.Family == null)
                    {
                        classPrim.Family = family;
                    }

                    // construct the option if missing
                    string optionName;
                    if (!classPrim.Family.VariantSet.HasOption(ec.TypeId, mats, out optionName))
                    {
                        // optionName = ec.Parts.First().SymbolName;
                        VariantOption variant = classPrim.Family.AddVariantOption(ec.TypeId, ec.Parts.First().SymbolName);
                        optionName = variant.Name;
                        ec.Prim.VariantSet = classPrim.Family.VariantSet.Name;
                        ec.Prim.VariantOption = variant.Name;

                        Stage famStage = ExportManager.TryGetStage(classPrim.Family.StageId);
                        string refPath = _stage.GetRelativePathToStage(famStage);
                        bool asPayload = ExportManager.Settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsPayload;
                        classPrim.Instance.AddAssetReference(refPath, asPayload);

                        Stage geometryStage = ExportManager.TryGetStage(variant.GeometryStageId);
                        foreach (MeshData data in meshData)
                        {
                            if (!inLink)
                            {
                                Mesh mesh = new Mesh(geometryStage.Default.StageId, $"Mesh_{data.MaterialId}", geometryStage.Default, data);
                                setCastShadows(mesh, data.MaterialId, family.MaterialStageId);
                            }
                            else
                            {
                                Mesh mesh = new Mesh(geometryStage.Default.StageId, $"Mesh_{data.MaterialId}", geometryStage.Default, data, _link.LinkId);
                                setCastShadows(mesh, data.MaterialId, family.MaterialStageId);
                            }

                            long fromStageId = (inLink) ? _link.MaterialStageId : ExportManager.MaterialStage.Id;
                            Material copy = MaterialManager.CopyMaterial(data.MaterialId, fromStageId, classPrim.Family.MaterialStageId);
                            copy.InUse = true;
                            copy.ActivateBranch();
                        }
                        classPrim.Instance.ActivateBranch();
                    }

                    // add reference to instance and set variant selection
                    ec.Prim.AddInternalReference(classPrim.Instance.Path);
                    ec.Prim.Instanceable = true;
                    ec.Prim.VariantSet = classPrim.Family.VariantSet.Name;
                    ec.Prim.VariantOption = optionName;
                }
            }

            // mapped families get their transform independently
            if (!ExportManager.Settings.Mappings.FamilyTypes.UserMapped.Any(f => f.Id == ec.TypeId))
            {
                bool isInstance = ec.IsInstance();
                Xform xform = (Xform)ec.Prim;

                // If it is an instance, use the instance transform (from the root Part).
                // Otherwise, use LocalTransform (usually Identity) because ec.Collapse()
                // already bakes all Part transforms into the mesh vertex positions.
                Transform t = isInstance ? ec.GetInstanceTransform() : ec.LocalTransform;

                // Fix FamilySymbol transform for imported DWG geometry without root meshes
                if (!isInstance && ec.Meshes.Count == 0)
                {
                    foreach (Part part in ec.Parts)
                    {
                        if (!part.HasMeshes() && part.ObjectTypeName == "FamilySymbol")
                        {
                            t = part.LocalTransform;
                            break;
                        }
                    }
                }

                xform.SetTransform(t);
            }
            _ec.RemoveAt(_ec.Count - 1);
        }

        if (!(_element.Category.Name == "Lighting Fixtures" && ExportManager.Settings.Options.IncludeLights))
        {
            // ElementEnd happens BEFORE OnLight, so we need to keep these around and nullify later
            // if we are exporting light emitting elements
            _prim = null;
            _element = null;
        }
    }

    public RenderNodeAction OnFaceBegin(FaceNode node)
    {
        return RenderNodeAction.Proceed;
    }

    public void OnFaceEnd(FaceNode node)
    {
    }

    public RenderNodeAction OnInstanceBegin(InstanceNode node)
    {
        if (inInstance)
        {
            inNestedInstance = true;
        }
        else
        {
            inInstance = true;
        }
        Transform t = node.GetTransform();

        // Check if the family instance is mirrored and correct the transform
        if (_element is FamilyInstance)
        {
            FamilyInstance fi = (FamilyInstance)_element;
            t = CorrectMirroredTransform(t, fi);
        }

        Transform multi = _transforms.Last().Multiply(t);
        _transforms.Add(multi);

        ElementId symbolId = node.GetSymbolGeometryId().SymbolId;
        _ec.Last().AddPart(new Part(_doc.GetElement(symbolId), t));

        ElementId typeId = _element.GetTypeId();

        _progressUpdate.DisplayMessage = $"Export Element Geometry: {_element.Name} {_prim.DisplayName}";
        Exporter.UpdateProgress(_progressUpdate);

#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"Type Id: {_typeId.GetValue()}");
        usd.exporter.revit.log.info($"Symbol Id: {_symbolId.GetValue()} <-- this one can vary from the type id!");
        FamilySymbol fSymbol = _doc.GetElement(_symbolId) as FamilySymbol;
        object import = _doc.GetElement(_symbolId);
        usd.exporter.revit.log.info($"symbol id element type: {import.GetType()}");
        if (fSymbol != null)
        {
            usd.exporter.revit.log.info($"Family Id:{fSymbol.Family.Id.GetValue()}");
        }
        else
        {
            usd.exporter.revit.log.info($"Family Id: NULL");
        }
        usd.exporter.revit.log.info("instance node transform:");
        t.Log();
        usd.exporter.revit.log.info("multiplied transform:");
        multi.Log();
#endif
        if (ExportManager.Settings.Mappings.FamilyTypes.UserMapped.Any(f => f.Id == typeId.GetValue()))
        {
            UserFamilyTypeMapping map = ExportManager.Settings.Mappings.FamilyTypes.UserMapped.Where(f => f.Id == typeId.GetValue()).First();
            _prim.AddAssetReference(map.AssetPath, true);
            Transform scale = Transform.Identity;

            // Probe MPU only for local assets.
            double assetMetersPerUnit = -1.0;
            if (usd.exporter.revit.file.client.isLocalUri(map.AssetPath))
            {
                assetMetersPerUnit = usd.exporter.revit.core.getMetersPerUnitFromFile(map.AssetPath);
            }

            // Revit defaults to feet (e.g. ExportManager.REVIT_DEFAULT_MPU)
            // here we only scale the external asset based on the ratio to ExportManager.REVIT_DEFAULT_MPU
            // the actual scaling to desired output MPU from feet will be done by convertMetersPerUnit function call later
            if (assetMetersPerUnit != ExportManager.REVIT_DEFAULT_MPU && assetMetersPerUnit != -1.0)
            {
                double scaleFactor = assetMetersPerUnit / ExportManager.REVIT_DEFAULT_MPU;
                scale = scale.ScaleBasis(scaleFactor);
#if DEBUG
                usd.exporter.revit.log.info("scaled transform");
                scale.Log();
#endif
            }

            Location location = _element.Location;
            if (location is LocationPoint)
            {
                LocationPoint lp = (LocationPoint)location;
                Transform transform = Transform.CreateTranslation(lp.Point);
                Transform rotation = Transform.CreateRotation(XYZ.BasisZ, lp.Rotation);
                Transform final = transform.Multiply(scale).Multiply(rotation);
#if DEBUG
                usd.exporter.revit.log.info("rotation transform");
                rotation.Log();
                usd.exporter.revit.log.info("point transform");
                transform.Log();
                usd.exporter.revit.log.info("final transform");
                final.Log();
#endif
                ((Xform)_prim).SetTransform(final);
            }
            return RenderNodeAction.Skip;
        }
        return RenderNodeAction.Proceed;
    }

    public void OnInstanceEnd(InstanceNode node)
    {
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"Instance End: {node.GetSymbolGeometryId().SymbolId.GetValue()}");
#endif

        if (_transforms.Count > 1)
        {
            _transforms.RemoveAt(_transforms.Count - 1);
        }
        if (inNestedInstance)
        {
            inNestedInstance = false;
        }
        else
        {
            inInstance = false;
        }
        _ec.Last().DeactivateLastPart();
    }

    public void OnLight(LightNode node)
    {
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"Light");
#endif
        if (ExportManager.Settings.Options.IncludeLights)
        {
            Asset a = node.GetAsset();
            Transform t = node.GetTransform();

            Scope lights;
            if (_stage.Default.Children.Any(p => p.Name == "Lights"))
            {
                lights = _stage.Default.Children.Where(p => p.Name == "Lights").First() as Scope;
            }
            else
            {
                lights = new Scope(_stage.Id, "Lights", _stage.Default);
            }

            Light light;
            LightType lightType = null;
            try
            {
                if (_element is FamilyInstance)
                {
                    lightType = LightType.GetLightTypeFromInstance(_element.Document, _element.Id);
                }
                else
                {
                    lightType = LightType.GetLightType(_element.Document, _element.Id);
                }
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
            {
                usd.exporter.revit.log.warning($"Could not convert Element {_element.Id.GetValue()} to LightType, please consider rebuilding this light with a newer Revit template");
                usd.exporter.revit.log.warning($"Failed to process Light, creating default sphere light: {ex.Message}");
            }
            if (lightType == null)
            {
                light = Light.DefaultLight(lights.StageId, _element.Name + "_" + _element.Id.GetValue().ToString(), lights);
            }
            else
            {
                light = new Light(lights.StageId, _element.Name + "_" + _element.Id.GetValue().ToString(), lights, a, lightType);
            }

            Transform multi = _transforms.Last().Multiply(t);
            light.SetTransform(multi);

            if (!inInstance)
            {
                usd.exporter.revit.log.info(_ec.ToString());
                _ec.RemoveAt(_ec.Count - 1);
                _element = null;
                _prim = null;
            }
        }
    }

    public RenderNodeAction OnLinkBegin(LinkNode node)
    {
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"Link {_element.Name}");
#endif
        if (ExportManager.Settings.Options.IncludeLinks)
        {
            Link link = ExportManager.TryGetLink(_element.Id.GetValue());
            if (link != null)
            {
                inLink = true;
                _link = link;
                _stage = ExportManager.TryGetStage(link.StageId);
                _doc = node.GetDocument();
                _link.Transform = node.GetTransform();
                return RenderNodeAction.Proceed;
            }
        }
        return RenderNodeAction.Skip;
    }

    public void OnLinkEnd(LinkNode node)
    {
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info("End Link");
#endif
        _stage = ExportManager.MainStage;
        _doc = ExportManager.GetMainDocument();
        Xform linkXform = ExportManager.GetPrim(_doc.Title, _link.LinkId) as Xform;
        usd.exporter.revit.log.info(linkXform.Path);
        linkXform.SetTransform(_link.Transform);
        inLink = false;
    }

    public void OnMaterial(MaterialNode node)
    {
        // _materialId is assigned to the mesh data during OnPolymesh
        _materialId = node.MaterialId;
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"{_materialId.GetValue()}");
#endif

        if (inLink)
        {
            MaterialManager.UseMaterial(_link.MaterialStageId, _materialId.GetValue());
        }
        else
        {
            MaterialManager.UseMaterial(ExportManager.MaterialStage.Id, _materialId.GetValue());
        }

        if (ExportManager.IsCylinder(_element))
        {
            long materialIdValue = _materialId.GetValue();
            foreach (Cylinder c in _prim.Children.OfType<Cylinder>())
            {
                c.MaterialId = materialIdValue;
            }
        }
    }

    public void OnPolymesh(PolymeshTopology node)
    {
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"Mesh");
#endif

        if (ExportManager.IsCylinder(_element))
        {
            // alredy written pre export, materials are picked up during MaterialNode export
            return;
        }

        int NumberOfPoints = node.NumberOfPoints;
        int NumberOfUVs = node.NumberOfUVs;
        int NumberOfNormals = node.NumberOfNormals;
        int NumberOfFacets = node.NumberOfFacets;

        List<XYZ> points = node.GetPoints().ToList();
        List<XYZ> normals = new List<XYZ>(NumberOfPoints);
        List<UV> primvars_st = node.GetUVs().ToList();
        List<int> faceVertexIndices = new List<int>(NumberOfFacets * 3);
        List<int> faceVertexCounts = Enumerable.Repeat(3, NumberOfFacets).ToList();

        // The number of points and the number of UVs must be the same (uvsInterporation = vertex).
        if (NumberOfUVs == 0 || primvars_st.Count != points.Count)
        {
            primvars_st = Enumerable.Repeat(UV.Zero, NumberOfPoints).ToList();
        }

        IList<PolymeshFacet> nodeFacets = node.GetFacets();
        foreach (PolymeshFacet facet in nodeFacets)
        {
            faceVertexIndices.Add(facet.V1);
            faceVertexIndices.Add(facet.V2);
            faceVertexIndices.Add(facet.V3);
        }
#if DEBUG && EXPORT_CONTEXT
        // Check that the vertex indices for each triangle are correct.
        for (int i = 0, vPos = 0; i < nodeFacets.Count; ++i, vPos += 3)
        {
            if (faceVertexIndices[vPos + 0] == faceVertexIndices[vPos + 1] || faceVertexIndices[vPos + 0] == faceVertexIndices[vPos + 2] || faceVertexIndices[vPos + 1] == faceVertexIndices[vPos + 2])
            {
                usd.exporter.revit.log.warning($"The vertex index specification for a mesh face is invalid: {_element.Name} {_prim.DisplayName}");
                break;
            }
        }
        for (int i = 0; i < faceVertexIndices.Count; ++i)
        {
            if (faceVertexIndices[i] < 0 || faceVertexIndices[i] >= NumberOfPoints)
            {
                usd.exporter.revit.log.warning($"Vertex index out of range ({faceVertexIndices[i]}, NumberOfPoints = {NumberOfPoints}): {_element.Name} {_prim.DisplayName}");
                break;
            }
        }
#endif
        // If there is ONE normal for the whole face, add it for each of the index positions.
        if (NumberOfNormals == 1)
        {
            XYZ nodeNormal = node.GetNormal(0);

            for (int x = 0; x < points.Count; x++)
            {
                // Need to add it for each of the vertices on the facet
                normals.Add(nodeNormal);
            }
        }

        // If there is one normal per facet, add it for each facet
        else if (NumberOfFacets == NumberOfNormals)
        {
            normals.AddRange(node.GetNormals());
        }

        // Otherwise, this would be one normal per vertex
        else if (NumberOfNormals == (NumberOfFacets * 3))
        {
            normals.AddRange(node.GetNormals());
        }

        double area = 0;
        if (_materialId.GetValue() < 0)
        {
            area = _area; // if material is invalid, use element's area
        }
        else
        {
            area = _element.GetMaterialArea(_materialId, false); // use material area of element if available
        }

        // There must be the same number of vertices and normals (normalsInterporation = vertex).
        // There must be the same number of vertices and uvs (uvsInterporation = vertex).
        MeshData data = new MeshData(points, normals, primvars_st, faceVertexIndices, faceVertexCounts, _materialId.GetValue(), area);

        Part activePart = _ec.Last().GetActivePart();
        if (activePart == null)
        {
            _ec.Last().Meshes.Add(data); // if we are not in an instance, meshes are assigned to the root
        }
        else
        {
            activePart.Meshes.Add(data); // if we are in an instance, meshes are assigned to the active part
        }
    }

    private static void setCastShadows(Mesh mesh, long materialId, long materialStageId)
    {
        bool isGlass = MaterialManager.IsGlass(materialStageId, materialId);
        if (isGlass)
        {
            mesh.CastShadows = false;
        }
    }

    public void OnRPC(RPCNode node)
    {
#if DEBUG && EXPORT_CONTEXT
        usd.exporter.revit.log.info($"RPC");
#endif
        if (_element != null)
        {
            ElementId typeId = _element.GetTypeId();
            if (ExportManager.Settings.Mappings.FamilyTypes.UserMapped.Any(f => f.Id == typeId.GetValue()))
            {
                UserFamilyTypeMapping map = ExportManager.Settings.Mappings.FamilyTypes.UserMapped.Where(f => f.Id == typeId.GetValue()).First();
                _prim.AddAssetReference(map.AssetPath, true);
            }
            else
            {
                Transform t = null;
                Xform xform = (Xform)_prim;

                // It offsets the Transform because it is a child of _prim.
                t = Transform.Identity;
                t.Origin = -ElementContext.GetPivotPoint(_element);

                List<MeshData> data = new List<MeshData>();
                Options options = new Options();
                options.DetailLevel = ViewDetailLevel.Fine;
                GeometryElement geometry = _element.get_Geometry(options);
                foreach (GeometryObject g in geometry)
                {
                    if (g is GeometryInstance)
                    {
                        // Objects with (Id < 0) are RPC objects that will not be called by OnPolymesh.
                        GeometryInstance gi = (GeometryInstance)g;
                        var geom = gi.GetInstanceGeometry();
                        foreach (var ig in geom)
                        {
                            if (ig is Solid)
                            {
                                Solid gSolid = (Solid)ig;
                                if (gSolid.Id < 0)
                                {
                                    data.AddRange(ExportManager.GetSolidMeshData(gSolid, -1, 0, t));
                                }
                            }
                            else if (ig is Autodesk.Revit.DB.Mesh)
                            {
                                Autodesk.Revit.DB.Mesh gMesh = (Autodesk.Revit.DB.Mesh)ig;
                                if (gMesh.Id < 0)
                                {
                                    data.Add(ExportManager.GetMeshData(gMesh, -1, 0, t));
                                }
                            }
                        }
                    }
                    if (g is Solid)
                    {
                        Solid solid = (Solid)g;
                        if (solid.Id < 0)
                        {
                            data.AddRange(ExportManager.GetSolidMeshData(solid, -1, 0, t));
                        }
                    }
                    else if (g is Autodesk.Revit.DB.Mesh)
                    {
                        Autodesk.Revit.DB.Mesh mesh = (Autodesk.Revit.DB.Mesh)g;
                        if (mesh.Id < 0)
                        {
                            data.Add(ExportManager.GetMeshData(mesh, -1, 0, t));
                        }
                    }
                }
                if (data.Count > 0)
                {
                    // Added MeshData with no material assigned.
                    Part activePart = _ec.Last().GetActivePart();
                    if (activePart == null)
                    {
                        // if we are not in an instance, meshes are assigned to the root
                        _ec.Last().Meshes.AddRange(data);
                    }
                    else
                    {
                        // if we are in an instance, meshes are assigned to the active part
                        activePart.Meshes.AddRange(data);
                    }
                }
            }
        }
    }

    public RenderNodeAction OnViewBegin(ViewNode node)
    {
        return RenderNodeAction.Proceed;
    }

    public void OnViewEnd(ElementId elementId)
    {
    }

    public bool Start()
    {
        _doc = ExportManager.GetMainDocument();
        _stage = ExportManager.MainStage;
        return true;
    }

    // Corrects the transform for mirrored family instances.
    private Transform CorrectMirroredTransform(Transform t, FamilyInstance fi)
    {
        // Check if the family instance is mirrored
        bool isHandFlipped = fi.HandFlipped;
        bool isFacingFlipped = fi.FacingFlipped;

        if (!isHandFlipped && !isFacingFlipped)
        {
            // Not mirrored, return original transform
            return t;
        }

        // Create corrected transform by negating the appropriate basis vector
        // This introduces the necessary negative scale (reflection)
        Transform corrected = Transform.Identity;
        corrected.Origin = t.Origin;
        corrected.BasisX = t.BasisX;
        corrected.BasisY = t.BasisY;
        corrected.BasisZ = t.BasisZ;

        // If isFacingFlipped is true, we check FacingOrientation to find what basis vector to negate
        if (isFacingFlipped)
        {
            if (fi.FacingOrientation.X == -1)
            {
                corrected.BasisX = -corrected.BasisX;
            }
            if (fi.FacingOrientation.Y == -1)
            {
                corrected.BasisY = -corrected.BasisY;
            }
            if (fi.FacingOrientation.Z == -1)
            {
                corrected.BasisZ = -corrected.BasisZ;
            }
        }
        // If isHandFlipped is true, we check HandOrientation to find what basis vector to negate
        if (isHandFlipped)
        {
            if (fi.HandOrientation.X == -1)
            {
                corrected.BasisX = -corrected.BasisX;
            }
            if (fi.HandOrientation.Y == -1)
            {
                corrected.BasisY = -corrected.BasisY;
            }
            if (fi.HandOrientation.Z == -1)
            {
                corrected.BasisZ = -corrected.BasisZ;
            }
        }

        return corrected;
    }
}
}

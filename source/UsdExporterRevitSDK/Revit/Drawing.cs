// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.IO;
using System.Runtime.InteropServices;

namespace UsdExporterRevitSdk
{
internal class Drawing
{
    private readonly static Color _red = new Color(255, 0, 0);
    private const double _shift = 0.01;
    private const int _size = 25;
    private const string _regMark = "_registration_marks";
    public Drawing(Prim parent, View view)
    {
        if (view.CanBePrinted && view.CropBoxActive && view.CropBox != null)
        {
            Xform xform = new Xform(parent.StageId, view.Name, PrimKind.Component, parent);
            List<ElementId> viewSet = new List<ElementId>() { view.Id };
            double z = (view.CropBox.Min.Z + view.CropBox.Max.Z) / 2;
            if (view is ViewPlan)
            {
                z = 0;
                ViewPlan viewPlan = (ViewPlan)view;
                PlanViewRange range = viewPlan.GetViewRange();
                z += range.GetOffset(PlanViewPlane.CutPlane);

                // Obtain the position (Z) from the level of the drawing.
                ElementId bottomClipPlane = range.GetLevelId(PlanViewPlane.BottomClipPlane);
                if (bottomClipPlane != ElementId.InvalidElementId)
                {
                    Element element = view.Document.GetElement(bottomClipPlane);
                    if (element is Level)
                    {
                        Level level = (Level)element;
                        z = level.Elevation;
                    }
                }
            }
            string tempPath = $@"{ExportManager.OvTempFolder}\{view.Id.GetValue()}";
            ImageExportOptions ieo = new ImageExportOptions() {
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = 4000,
                FilePath = tempPath,
                FitDirection = FitDirectionType.Horizontal,
                HLRandWFViewsFileType = ImageFileType.JPEGLossless,
                ShadowViewsFileType = ImageFileType.JPEGLossless,
                ImageResolution = ImageResolution.DPI_600,
                ExportRange = ExportRange.SetOfViews,
            };
            ieo.SetViewsAndSheets(viewSet);
            view.Document.ExportImage(ieo);

            XYZ q1, q2, q3, q4, xVec, yVec;
            List<ElementId> _registrationMarkIds = new List<ElementId>();

            using (Transaction t = new Transaction(view.Document))
            {
                if (t.Start("adding temporary line") == TransactionStatus.Started)
                {
                    q1 = view.CropBox.Transform.OfPoint(view.Document.Application.Create.NewXYZ(view.CropBox.Min.X, view.CropBox.Min.Y, z));
                    q2 = view.CropBox.Transform.OfPoint(view.Document.Application.Create.NewXYZ(view.CropBox.Max.X, view.CropBox.Min.Y, z));
                    q3 = view.CropBox.Transform.OfPoint(view.Document.Application.Create.NewXYZ(view.CropBox.Max.X, view.CropBox.Max.Y, z));
                    q4 = view.CropBox.Transform.OfPoint(view.Document.Application.Create.NewXYZ(view.CropBox.Min.X, view.CropBox.Max.Y, z));

                    xVec = (q2 - q1).Normalize() * _shift;
                    yVec = (q3 - q2).Normalize() * _size * _shift;

                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(_red);

                    for (int i = 0; i < _size; i++)
                    {
                        try
                        {
                            XYZ q1_1 = Transform.CreateTranslation(xVec * i).OfPoint(q1);
                            XYZ q2_1 = Transform.CreateTranslation(-xVec * i).OfPoint(q2);
                            XYZ q3_1 = Transform.CreateTranslation(-xVec * i).OfPoint(q3);
                            XYZ q4_1 = Transform.CreateTranslation(xVec * i).OfPoint(q4);

                            XYZ q1_2 = Transform.CreateTranslation(yVec).OfPoint(q1_1);
                            XYZ q2_2 = Transform.CreateTranslation(yVec).OfPoint(q2_1);
                            XYZ q3_2 = Transform.CreateTranslation(-yVec).OfPoint(q3_1);
                            XYZ q4_2 = Transform.CreateTranslation(-yVec).OfPoint(q4_1);

                            Line l1 = Line.CreateBound(q1_1, q1_2);
                            Line l2 = Line.CreateBound(q2_1, q2_2);
                            Line l3 = Line.CreateBound(q3_1, q3_2);
                            Line l4 = Line.CreateBound(q4_1, q4_2);

                            DetailCurve dc1 = view.Document.Create.NewDetailCurve(view, l1);
                            DetailCurve dc2 = view.Document.Create.NewDetailCurve(view, l2);
                            DetailCurve dc3 = view.Document.Create.NewDetailCurve(view, l3);
                            DetailCurve dc4 = view.Document.Create.NewDetailCurve(view, l4);

                            view.SetElementOverrides(dc1.Id, ogs);
                            view.SetElementOverrides(dc2.Id, ogs);
                            view.SetElementOverrides(dc3.Id, ogs);
                            view.SetElementOverrides(dc4.Id, ogs);

                            _registrationMarkIds.AddRange(new List<ElementId>() { dc1.Id, dc2.Id, dc3.Id, dc4.Id });
                        }
                        catch (Exception e)
                        {
                            usd.exporter.revit.log.error($"[{view.Name}] {e.Message}");
                        }
                    }
                }
                t.Commit();
            }
            ieo.FilePath = ieo.FilePath + _regMark;
            view.Document.ExportImage(ieo);

            using (Transaction t = new Transaction(view.Document))
            {
                if (t.Start("cleaning up registration marks") == TransactionStatus.Started)
                {
                    view.Document.Delete(_registrationMarkIds);
                }
                t.Commit();
                _registrationMarkIds.Clear();
            }

            XYZ[] pixelLocations = getRegistrationMarks(ieo.FilePath, out string filePathWithExt, out XYZ bitmapSize);
            if (pixelLocations != null && !string.IsNullOrEmpty(filePathWithExt))
            {
                File.Delete(filePathWithExt);
                if (pixelLocations[0] != null && pixelLocations[2] != null)
                {
                    // We can only place the image if we have min and max points
                    if (pixelLocations[3] == null)
                    {
                        pixelLocations[3] = new XYZ(pixelLocations[2].Y, pixelLocations[0].X, 0);
                    }
                    if (pixelLocations[1] == null)
                    {
                        pixelLocations[1] = new XYZ(pixelLocations[2].X, pixelLocations[0].Y, 0);
                    }

                    // Convert min/max pixel location to world location
                    XYZ pixelLocationMin = pixelLocations[3];
                    XYZ pixelLocationMax = pixelLocations[1];

                    XYZ worldLocationMin = new XYZ(remap(0, pixelLocationMin.X, pixelLocationMax.X, view.CropBox.Min.X, view.CropBox.Max.X), remap(0, pixelLocationMin.Y, pixelLocationMax.Y, view.CropBox.Min.Y, view.CropBox.Max.Y), z);
                    XYZ worldLocationMax = new XYZ(remap(bitmapSize.X, pixelLocationMin.X, pixelLocationMax.X, view.CropBox.Min.X, view.CropBox.Max.X), remap(bitmapSize.Y, pixelLocationMin.Y, pixelLocationMax.Y, view.CropBox.Min.Y, view.CropBox.Max.Y), z);

                    XYZ[] corners = new XYZ[] { view.CropBox.Transform.OfPoint(worldLocationMin),
                                                view.CropBox.Transform.OfPoint(new XYZ(worldLocationMax.X, worldLocationMin.Y, z)),
                                                view.CropBox.Transform.OfPoint(worldLocationMax),
                                                view.CropBox.Transform.OfPoint(new XYZ(worldLocationMin.X, worldLocationMax.Y, z)) };
                    XYZ translation = new XYZ((corners[0].X + corners[1].X + corners[2].X + corners[3].X) / 4.0, (corners[0].Y + corners[1].Y + corners[2].Y + corners[3].Y) / 4.0, (corners[0].Z + corners[1].Z + corners[2].Z + corners[3].Z) / 4.0);
                    Transform transform = Transform.CreateTranslation(translation);
                    xform.SetTransform(transform);
                    XYZ[] points = new XYZ[] { corners[0] - translation, corners[1] - translation, corners[2] - translation, corners[3] - translation };

                    List<int> faceVertexIndices = new List<int>() { 0, 1, 2, 3 };
                    List<int> faceVertexCount = new List<int>() { 4 };
                    List<UV> uvs = new List<UV>() { new UV(0, 0), new UV(1, 0), new UV(1, 1), new UV(0, 1) };

                    DirectoryInfo dir = new DirectoryInfo(ExportManager.OvTempFolder);
                    FileInfo[] files = dir.GetFiles();
                    if (files.Any(f => f.FullName.Contains(tempPath)))
                    {
                        FileInfo file = files.Where(f => f.FullName.Contains(tempPath)).First();

                        Scope looks = null;
                        Prim matDefault = ExportManager.MaterialStage.Default;
                        if (matDefault.Children.Any(p => p.Name == ExportManager.Settings.Options.MaterialFolderName))
                        {
                            looks = (Scope)matDefault.Children.Where(p => p.Name == ExportManager.Settings.Options.MaterialFolderName).First();
                        }
                        else
                        {
                            looks = new Scope(matDefault.StageId, ExportManager.Settings.Options.MaterialFolderName, matDefault);
                        }
                        string imagePath = "./Drawings/" + file.Name;
                        string materialName = $"{view.Name}_{view.Id}";
                        Material mat = new Material(looks.StageId, looks, view, materialName, imagePath);
                        MaterialManager.CacheMaterial(looks.StageId, view.Id.GetValue(), mat);
                        string toPath = ExportManager.Settings.File.OutputFolder + "/Drawings/" + file.Name;
                        MaterialManager.AddTexture(toPath, file.FullName);
                        MeshData data = new MeshData(points.ToList(), new List<XYZ>(), uvs, faceVertexIndices, faceVertexCount, view.Id.GetValue(), 20.0);
                        Mesh mesh = new Mesh(xform.StageId, view.Name, xform, data);
                        MaterialManager.UseMaterial(ExportManager.MaterialStage.Id, view.Id.GetValue());
                    }
                }
            }
            xform.Write(parent.StageId);
        }
        else
        {
            usd.exporter.revit.log.warning($"skipping drawing export of View {view.Name} {view.GetType().Name}");
            usd.exporter.revit.log.warning($"views must be 2D and have an active crop box to be exported");
        }
    }
    public static void ExportDrawings(Prim parent)
    {
        Document doc = ExportManager.GetMainDocument();
        var viewSheetSets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>().Where(s => s != null).ToList();
        if (viewSheetSets == null || viewSheetSets.Count == 0)
            return;
        string publishSetName = ExportManager.Settings.GetStringMatch(UsdExporterRevitSettingType.PublishSet, viewSheetSets.Select(v => v.Name).ToList());
        if (!string.IsNullOrEmpty(publishSetName))
        {
            List<ViewSheetSet> matches = viewSheetSets.Where(s => s.Name == publishSetName).ToList();
            if (matches.Count > 0)
            {
                Scope drawingsScope = new Scope(parent.StageId, "Drawings", parent);
                ViewSheetSet set = matches[0];
                Scope setScope = new Scope(drawingsScope.StageId, set.Name, drawingsScope);
                foreach (var v in set.Views)
                {
                    if (v is View)
                    {
                        View view = (View)v;
                        string viewTypeName = string.Empty;
                        switch (view.ViewType)
                        {
                            case ViewType.AreaPlan:
                                viewTypeName = "Area Plans";
                                break;
                            case ViewType.CeilingPlan:
                                viewTypeName = "Ceiling Plans";
                                break;
                            case ViewType.EngineeringPlan:
                                viewTypeName = "Structural Plans";
                                break;
                            case ViewType.FloorPlan:
                                viewTypeName = "Floor Plans";
                                break;
                            default:
                                viewTypeName = Enum.GetName(typeof(ViewType), view.ViewType) + "s";
                                break;
                        }
                        Scope viewTypeScope = null;
                        if (setScope.HasChild(viewTypeName))
                        {
                            viewTypeScope = setScope.GetChild(viewTypeName) as Scope;
                        }
                        else
                        {
                            viewTypeScope = new Scope(setScope.StageId, viewTypeName, setScope);
                        }
                        Drawing drawing = new Drawing(viewTypeScope, view);
                    }
                }
            }
        }
    }

    private static double remap(double from, double fromMin, double fromMax, double toMin, double toMax)
    {
        var fromAbs = from - fromMin;
        var fromMaxAbs = fromMax - fromMin;

        var normal = fromAbs / fromMaxAbs;

        var toMaxAbs = toMax - toMin;
        var toAbs = toMaxAbs * normal;

        var to = toAbs + toMin;

        return to;
    }

    private static XYZ[] getRegistrationMarks(string path, out string filePathWithExtension, out XYZ bitmapSize)
    {
        XYZ[] locations = new XYZ[4];
        filePathWithExtension = string.Empty;
        int quadrantSize = 2;
        int pixelSkip = 2;

        DirectoryInfo dir = new DirectoryInfo(ExportManager.OvTempFolder);
        FileInfo[] files = dir.GetFiles();

        // Use case-insensitive path matching and normalize path separators
        // This fixes issues where file systems or path formats differ between machines
        string normalizedPath = path.Replace('/', '\\').Replace('\\', Path.DirectorySeparatorChar);

        FileInfo file = files.FirstOrDefault(f => f.FullName.IndexOf(normalizedPath, StringComparison.OrdinalIgnoreCase) >= 0);

        if (file != null && file.Exists)
        {
            filePathWithExtension = file.FullName;

            try
            {
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(file.FullName))
                {
                    bitmapSize = new XYZ(bmp.Width, bmp.Height, 0);
                    int maxY = bmp.Height - 1;

                    // Use LockBits for better performance and more reliable color reading
                    System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                    try
                    {
                        // Copy bitmap data to managed array for safe access (no unsafe code required)
                        int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
                        byte[] rgbValues = new byte[bytes];
                        Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
                        int stride = bmpData.Stride;

                        // Helper function for robust red detection that handles JPEG compression artifacts
                        // Strategy 1: Strict criteria (R > 100 && G < 60 && B < 60) - works on machines with minimal compression
                        // Strategy 2: Ratio-based (R > 100 && R > G*2 && R > B*2) - handles compression artifacts
                        // Strategy 3: Dominant red (R > 150 && R > G + 50 && R > B + 50) - more lenient for heavily compressed images
                        Func<byte, byte, byte, bool> isRedMark = (r, g, b) =>
                        {
                            // Strategy 1: Strict criteria
                            if (r > 100 && g < 60 && b < 60)
                                return true;

                            // Strategy 2: Ratio-based (red must be at least 2x green and blue)
                            if (r > 100 && r > g * 2 && r > b * 2)
                                return true;

                            // Strategy 3: Dominant red (red significantly higher than green/blue)
                            if (r > 150 && r > g + 50 && r > b + 50)
                                return true;

                            return false;
                        };

                        // Search quadrant 1 (top-left)
                        for (int x = 0; x < bmp.Width / quadrantSize; x += pixelSkip)
                        {
                            for (int y = 0; y < bmp.Height / quadrantSize; y += pixelSkip)
                            {
                                int offset = y * stride + x * 3;
                                if (offset + 2 < rgbValues.Length)
                                {
                                    byte b = rgbValues[offset];
                                    byte g = rgbValues[offset + 1];
                                    byte r = rgbValues[offset + 2];

                                    if (isRedMark(r, g, b))
                                    {
                                        locations[0] = new XYZ(x, maxY - y, 0);
                                        goto next1;
                                    }
                                }
                            }
                        }
                    next1:
                        // Search quadrant 2 (top-right)
                        for (int x = bmp.Width - 1; x > bmp.Width / quadrantSize; x -= pixelSkip)
                        {
                            for (int y = 0; y < bmp.Height / quadrantSize; y += pixelSkip)
                            {
                                int offset = y * stride + x * 3;
                                if (offset + 2 < rgbValues.Length)
                                {
                                    byte b = rgbValues[offset];
                                    byte g = rgbValues[offset + 1];
                                    byte r = rgbValues[offset + 2];

                                    if (isRedMark(r, g, b))
                                    {
                                        locations[1] = new XYZ(x, maxY - y, 0);
                                        goto next2;
                                    }
                                }
                            }
                        }
                    next2:
                        // Search quadrant 3 (bottom-right)
                        for (int x = bmp.Width - 1; x > bmp.Width / quadrantSize; x -= pixelSkip)
                        {
                            for (int y = bmp.Height - 1; y > bmp.Height / quadrantSize; y -= pixelSkip)
                            {
                                int offset = y * stride + x * 3;
                                if (offset + 2 < rgbValues.Length)
                                {
                                    byte b = rgbValues[offset];
                                    byte g = rgbValues[offset + 1];
                                    byte r = rgbValues[offset + 2];

                                    if (isRedMark(r, g, b))
                                    {
                                        locations[2] = new XYZ(x, maxY - y, 0);
                                        goto next3;
                                    }
                                }
                            }
                        }
                    next3:
                        // Search quadrant 4 (bottom-left)
                        for (int x = 0; x < bmp.Width / quadrantSize; x += pixelSkip)
                        {
                            for (int y = bmp.Height - 1; y > bmp.Height / quadrantSize; y -= pixelSkip)
                            {
                                int offset = y * stride + x * 3;
                                if (offset + 2 < rgbValues.Length)
                                {
                                    byte b = rgbValues[offset];
                                    byte g = rgbValues[offset + 1];
                                    byte r = rgbValues[offset + 2];

                                    if (isRedMark(r, g, b))
                                    {
                                        locations[3] = new XYZ(x, maxY - y, 0);
                                        goto end;
                                    }
                                }
                            }
                        }
                    end:;
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }
            }
            catch (Exception e)
            {
                usd.exporter.revit.log.error($"Exception while processing bitmap: {e.Message}");
                bitmapSize = XYZ.Zero;
            }
        }
        else
        {
            bitmapSize = XYZ.Zero;
        }

        return locations;
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UsdExporterRevitSdk
{
/// <summary>
/// Creates USD from a revit file.
/// </summary>
public static class Exporter
{
    private static EventHandler<ProgressUpdate> OnProgressUpdate;
    private static ProgressUpdate _progress;

    public static void AddProgressUpdateCallback(EventHandler<ProgressUpdate> callback)
    {
        OnProgressUpdate += callback;
    }

    public static void CancelExport()
    {
        if (_progress != null)
        {
            _progress.State = ProgressUpdateState.Cancelled;
            usd.exporter.revit.log.info("Exporter: Export cancellation requested");
        }
    }

    public static bool IsCancelled()
    {
        if (_progress == null)
        {
            return false;
        }
        return _progress.State == ProgressUpdateState.Cancelled;
    }

    public static bool HandledCancelled()
    {
        if (IsCancelled())
        {
            ExportManager.CleanupStages();
            // Reset section box flag when cancelling
            ExportManager.IsSectionBoxActive = false;
            usd.exporter.revit.log.info("Export cancelled by user");
            return true;
        }
        return false;
    }

    public static Result ExportView(UIApplication app, View3D view, UsdExporterRevitSettings settings)
    {
        var perfTotalTimer = System.Diagnostics.Stopwatch.StartNew();

        // Log export options settings
        LogExportOptions(settings.Options);

        ProgressUpdate update = new ProgressUpdate();
        update.ActiveView = view.Name;
        update.ViewProgress = 5.0;
        update.DisplayMessage = "Gathering Data";
        UpdateProgress(update);

        Document doc = view.Document;
        FilteredElementCollector fec = new FilteredElementCollector(doc, view.Id);
        List<Element> elements = new List<Element>();

        ExportManager.Initialize(doc, settings);

        // Store section box state for use during export
        // This helps prevent duplicate meshes when using Instance Families with Section Box cropping
        ExportManager.IsSectionBoxActive = view.IsSectionBoxActive;

        if (ExportManager.Settings.AnyViewManipulations())
        {
            ExportManager.SetTemporaryViewSettings(view, app.ActiveUIDocument);
        }

        update.ViewProgress = 10.0;
        update.DisplayMessage = "Process Materials";
        UpdateProgress(update);

        // Check cancellation before processing materials
        if (HandledCancelled())
        {
            return Result.Cancelled;
        }

        var perfMaterialsTimer = System.Diagnostics.Stopwatch.StartNew();
        MaterialManager.Initialize();
        MaterialManager.ProcessMaterials(doc, ExportManager.MaterialStage.Default);
        perfMaterialsTimer.Stop();
        usd.exporter.revit.log.info($"[PERF] Exporter.MaterialProcessing | Duration: {perfMaterialsTimer.ElapsedMilliseconds} ms | DocumentTitle: {doc.Title}");

        Camera.ExportViewAsCamera(view, ExportManager.GetGeometryRoot(ExportManager.MainStage), true);
        if (ExportManager.Settings.Options.IncludeCameras)
        {
            List<View3D> cameras = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().Where(v => !v.IsTemplate).ToList();
            foreach (View3D camera in cameras)
            {
                // Check cancellation before exporting each camera
                if (HandledCancelled())
                {
                    return Result.Cancelled;
                }
                Camera.ExportViewAsCamera(camera, ExportManager.GetGeometryRoot(ExportManager.MainStage));
            }
        }

        // Check cancellation before setting up links
        if (HandledCancelled())
        {
            return Result.Cancelled;
        }

        List<RevitLinkInstance> linkInstances = new List<RevitLinkInstance>();
        if (ExportManager.Settings.Options.IncludeLinks)
        {
            update.ViewProgress = 15.0;
            update.DisplayMessage = "Setup Links";
            UpdateProgress(update);

            linkInstances = fec.OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
            Link.SetUpLinks(linkInstances, settings.File.OutputFolder + "/Links");
            fec = new FilteredElementCollector(doc, view.Id); // reset the fec
        }

        elements = fec.WhereElementIsNotElementType().ToList();
        int i = 0;
        foreach (Element element in elements)
        {
            // Check cancellation before processing each element
            if (HandledCancelled())
            {
                return Result.Cancelled;
            }

            i++;
            update.ViewProgress = 20 + (i * 10.0 / elements.Count);
            update.DisplayMessage = $"Collect Element Data {element.Name}";
            UpdateProgress(update);

            ExportManager.CreateXformBranch(element, ExportManager.GetGeometryRoot(ExportManager.MainStage));
            if (ExportManager.Settings.Options.InstanceFamilies && element is FamilyInstance)
            {
                ExportManager.CreatePrototypeBranch(element, ExportManager.GetGeometryRoot(ExportManager.MainStage));
            }
        }

        update.ViewProgress = 35.0;
        update.DisplayMessage = "Process Spatial Elements";
        UpdateProgress(update);

        if (ExportManager.Settings.Options.IncludeRooms)
        {
            SpatialElements.SetSchemeValues<Autodesk.Revit.DB.Architecture.Room>(doc);
            SpatialElements.ExportSpatialElements<Autodesk.Revit.DB.Architecture.Room>(doc, ExportManager.MainStage.Default, view);
            foreach (RevitLinkInstance linkInstance in linkInstances)
            {
                // Check cancellation before processing each link instance
                if (HandledCancelled())
                {
                    return Result.Cancelled;
                }
                Document linkDoc = linkInstance.GetLinkDocument();
                Link link = ExportManager.TryGetLink(linkInstance.Id.GetValue());
                Stage linkStage = ExportManager.TryGetStage(link.StageId);
                SpatialElements.SetSchemeValues<Autodesk.Revit.DB.Architecture.Room>(linkDoc);
                SpatialElements.ExportSpatialElements<Autodesk.Revit.DB.Architecture.Room>(linkDoc, linkStage.Default, view, linkInstance);
            }
        }

        if (ExportManager.Settings.Options.IncludeSpaces)
        {
            SpatialElements.SetSchemeValues<Autodesk.Revit.DB.Mechanical.Space>(doc);
            SpatialElements.ExportSpatialElements<Autodesk.Revit.DB.Mechanical.Space>(doc, ExportManager.MainStage.Default, view);
            foreach (RevitLinkInstance linkInstance in linkInstances)
            {
                // Check cancellation before processing each link instance
                if (HandledCancelled())
                {
                    return Result.Cancelled;
                }
                Document linkDoc = linkInstance.GetLinkDocument();
                Link link = ExportManager.TryGetLink(linkInstance.Id.GetValue());
                Stage linkStage = ExportManager.TryGetStage(link.StageId);
                SpatialElements.SetSchemeValues<Autodesk.Revit.DB.Mechanical.Space>(linkDoc);
                SpatialElements.ExportSpatialElements<Autodesk.Revit.DB.Mechanical.Space>(linkDoc, linkStage.Default, view, linkInstance);
            }
        }

        // call revit export api
        update.ViewProgress = 40.0;
        update.DisplayMessage = "Export Element Geometry";
        UpdateProgress(update);

        // Check cancellation before main geometry export
        if (HandledCancelled())
        {
            return Result.Cancelled;
        }

        var perfGeometryTimer = System.Diagnostics.Stopwatch.StartNew();
        using (CustomExporter ce = new CustomExporter(doc, new ExportContext()))
        {
            try
            {
                ce.IncludeGeometricObjects = false;
                ce.ShouldStopOnError = true;
                ce.Export(view);
            }
            catch (Exception ex)
            {
                usd.exporter.revit.log.warning($"Exception thrown during export of {view.Name}: {ex.Message}");
                usd.exporter.revit.log.warning($"Stacktrace: {ex.StackTrace}");
            }
        }
        perfGeometryTimer.Stop();
        usd.exporter.revit.log.info($"[PERF] Exporter.GeometryExport | Duration: {perfGeometryTimer.ElapsedMilliseconds} ms | ViewName: {view.Name}, ElementCount: {elements.Count}");

        if (ExportManager.Settings.AnyViewManipulations())
        {
            ExportManager.RemoveTemporaryViewSettings(view, app.ActiveUIDocument);
        }

        // Check cancellation before drawings export
        if (HandledCancelled())
        {
            return Result.Cancelled;
        }

        if (ExportManager.Settings.Options.IncludeDrawings)
        {
            update.ViewProgress = 75.0;
            update.DisplayMessage = "Export Drawings";
            UpdateProgress(update);

            Drawing.ExportDrawings(ExportManager.MainStage.Default);
        }

        update.ViewProgress = 80.0;
        update.DisplayMessage = "Copy Textures";
        UpdateProgress(update);

        // Check cancellation before copying textures
        if (HandledCancelled())
        {
            return Result.Cancelled;
        }

        MaterialManager.CopyTextures();

        update.ViewProgress = 85.0;
        update.DisplayMessage = "Write to Usd";
        UpdateProgress(update);

        // Check cancellation before writing to USD
        if (HandledCancelled())
        {
            return Result.Cancelled;
        }

        // write it all
        var perfWriteTimer = System.Diagnostics.Stopwatch.StartNew();
        ExportManager.MainStage.Write();
        perfWriteTimer.Stop();
        usd.exporter.revit.log.info($"[PERF] Exporter.WriteToUsd | Duration: {perfWriteTimer.ElapsedMilliseconds} ms | DocumentTitle: {doc.Title}");

        update.ViewProgress = 100.0;
        update.DisplayMessage = "Complete";
        UpdateProgress(update);

        ExportManager.CleanupStages();

        // Reset section box flag for next export
        ExportManager.IsSectionBoxActive = false;

        perfTotalTimer.Stop();
        usd.exporter.revit.log.info($"[PERF] Exporter.TotalExportTime | Duration: {perfTotalTimer.ElapsedMilliseconds} ms | ViewName: {view.Name}, DocumentTitle: {doc.Title}, ElementCount: {elements.Count}");

        return Result.Succeeded;
    }
    public static Result ExportBatch(UIApplication app, UsdExporterRevitSettings settings)
    {
        Document doc = app.ActiveUIDocument.Document;
        string startingDoc = doc.PathName;

        List<string> filePaths = settings.Batch.InputFolder.Replace("/", "\\").getFilesAtDepth(settings.Batch.FolderDepth, ".rvt");
        if (filePaths.Count <= 0)
        {
            usd.exporter.revit.log.warning($"No revit files found at {settings.Batch.InputFolder}");
            return Result.Cancelled;
        }
        if (string.IsNullOrEmpty(settings.Batch.OutputFolder))
        {
            usd.exporter.revit.log.warning($"Invalid output folder for batch.. canceling");
            return Result.Cancelled;
        }

        List<string> failures = new List<string>();
        string outputFolder = settings.Batch.OutputFolder;

#region setup random family so we can close docs freely
        string programdata = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string ovTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ov\temp");
#if REV2026
        string familyTemplates = Path.Combine(programdata, $@"Autodesk\RVT 2026\Family Templates");
        string randomFamily = Path.Combine(ovTemp, "fam2026.rfa");
#endif
#if REV2025
        string familyTemplates = Path.Combine(programdata, $@"Autodesk\RVT 2025\Family Templates");
        string randomFamily = Path.Combine(ovTemp, "fam2025.rfa");
#endif
#if REV2024
        string familyTemplates = Path.Combine(programdata, $@"Autodesk\RVT 2024\Family Templates");
        string randomFamily = Path.Combine(ovTemp, "fam2024.rfa");
#endif
        if (!Directory.Exists(ovTemp))
        {
            Directory.CreateDirectory(ovTemp);
        }
        if (File.Exists(randomFamily))
        {
            UIDocument uid = app.OpenAndActivateDocument(randomFamily);
            if (uid != null)
            {
#if DEBUG
                usd.exporter.revit.log.info($"opened and activated {uid.Document.PathName}");
#endif
                startingDoc = uid.Document.PathName;
            }
        }
        else
        {
            string firstTemplate = string.Empty;
            if (Directory.Exists(familyTemplates))
            {
                DirectoryInfo ftDir = new DirectoryInfo(familyTemplates);
                foreach (DirectoryInfo d in ftDir.GetDirectories())
                {
                    FileInfo[] files = d.GetFiles();
                    if (files.Length > 0)
                    {
                        firstTemplate = files[0].FullName;
                    }
                    if (firstTemplate != string.Empty)
                    {
                        break;
                    }
                }
            }
            if (firstTemplate != string.Empty)
            {
                Document templateDoc = app.Application.NewFamilyDocument(firstTemplate);
                templateDoc.SaveAs(randomFamily);
                UIDocument uid = app.OpenAndActivateDocument(randomFamily);
                if (uid != null)
                {
#if DEBUG
                    usd.exporter.revit.log.info($"opened and activated {uid.Document.PathName}");
#endif
                    startingDoc = uid.Document.PathName;
                }
            }
        }
#endregion

        for (int i = 0; i < filePaths.Count; i++)
        {
            // Check if batch export was cancelled
            if (IsCancelled())
            {
                usd.exporter.revit.log.info("Batch export cancelled by user");
                return Result.Cancelled;
            }

            string rFile = filePaths[i];
#if DEBUG
            usd.exporter.revit.log.info($"attemp to open revit model at {rFile}");
#endif
            try
            {
#if DEBUG
                if (!string.IsNullOrEmpty(rFile) && !rFile.Contains("Snowdon") && !rFile.Contains("Projekt"))
#else
                if (rFile != null)
#endif
                {
                    UIDocument rDoc = null;

                    OpenOptions openOptions = new OpenOptions();
                    openOptions.Audit = false;
                    openOptions.AllowOpeningLocalByWrongUser = true;
                    openOptions.IgnoreExtensibleStorageSchemaConflict = true;
                    rDoc = app.OpenAndActivateDocument(rFile);

#if DEBUG
                    if (rDoc == null)
                    {
                        usd.exporter.revit.log.info("could not open and activate model");
                    }
                    else
                    {
                        usd.exporter.revit.log.info($"document opened: {rDoc.Document.Title}");
                        usd.exporter.revit.log.info($"app active document: {app.ActiveUIDocument.Document.Title}");
                    }
#endif

                    if (rDoc != null)
                    {
                        List<View3D> views = new FilteredElementCollector(app.ActiveUIDocument.Document).OfClass(typeof(View3D)).OfType<View3D>().Where(v => !v.IsTemplate).ToList();
                        List<string> viewNames = settings.GetStringMatches(UsdExporterRevitSettingType.ViewToFind, views.Select(v => v.Name).ToList());
                        List<View3D> viewsToExport = views.Where(v => viewNames.Contains(v.Name)).ToList();

                        string cleanTitle = app.ActiveUIDocument.Document.Title.removeBadWindowsFilePathChars();
                        string modelFolder = outputFolder + "/" + cleanTitle;

#if DEBUG
                        usd.exporter.revit.log.info($"{viewsToExport.Count} views found in {app.ActiveUIDocument.Document.Title}");
#endif
                        ProgressUpdate update = new ProgressUpdate();
                        update.TotalModelNumber = filePaths.Count;
                        update.ActiveModelNumber = i + 1;
                        update.ActiveModel = app.ActiveUIDocument.Document.Title;
                        UpdateProgress(update);

                        int c = 0;
                        foreach (View3D view in viewsToExport)
                        {
                            // Check if batch export was cancelled
                            if (IsCancelled())
                            {
                                usd.exporter.revit.log.info("Batch export cancelled by user");
                                return Result.Cancelled;
                            }

                            c++;
#if DEBUG
                            usd.exporter.revit.log.info($"begin work on {view.Name}");
#endif
                            app.ActiveUIDocument.ActiveView = view;
#if DEBUG
                            usd.exporter.revit.log.info($"active view: {app.ActiveUIDocument.ActiveView.Name}");
#endif
                            string cleanView = view.Name.removeBadWindowsFilePathChars();
                            settings.File.OutputFolder = modelFolder + "/" + cleanView;
                            settings.File.FileName = cleanTitle + "_" + cleanView;
#if DEBUG
                            usd.exporter.revit.log.info($"attempting to export {view.Name}");
#endif

                            update.ViewProgress = 0.1;
                            update.TotalViewNumber = viewsToExport.Count;
                            update.ActiveViewNumber = c;
                            update.DisplayMessage = "Begin Export View";
                            UpdateProgress(update);

                            Exporter.ExportView(app, view, settings);
                        }

                        Document docToClose = app.ActiveUIDocument.Document;
#if DEBUG
                        usd.exporter.revit.log.info($"attempt to open and activate {startingDoc}");
#endif
                        app.OpenAndActivateDocument(startingDoc);
#if DEBUG
                        usd.exporter.revit.log.info($"attempt to close {docToClose.Title}");
#endif
                        docToClose.Close(false);
                    }
                    else
                    {
                        usd.exporter.revit.log.error($"Unable to open file {rFile}," + $" if exporting linked models, open a new model, " + $"revit sample model or any model not linked to successfully batch export");
                    }
                }
            }
            catch (Exception e)
            {
                usd.exporter.revit.log.error(e.Message);
                failures.Add(filePaths[i]);
                // Usually wrong revit version. Skip this file.
                continue;
            }
        }
        return Result.Succeeded;
    }

    private static List<string> getFilesAtDepth(this string root, int depth, string extension)
    {
        var list = new List<string>();

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (depth > 0)
                {
                    list.AddRange(getFilesAtDepth(directory, depth - 1, extension));
                }
            }
            // This is because of a directory access issue.
            catch (Exception ex)
            {
                usd.exporter.revit.log.warning(ex.Message);
            }
        }
        try
        {
            list.AddRange(Directory.EnumerateFiles(root).Where(f => Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase)));
        }
        // This is because of a directory access issue.
        catch (Exception ex)
        {
            usd.exporter.revit.log.warning(ex.Message);
        }

        return list;
    }

    public static void InitiateProgressContext(ProgressUpdate context)
    {
        _progress = context;
    }

    public static void UpdateProgress(ProgressUpdate update)
    {
        if (_progress != null)
        {
            if (update.ActiveViewNumber > 0)
            {
                _progress.ActiveViewNumber = update.ActiveViewNumber;
                _progress.ActiveView = update.ActiveView;
            }
            if (update.TotalViewNumber > 1)
            {
                _progress.TotalViewNumber = update.TotalViewNumber;
            }
            if (update.ActiveModelNumber > 0)
            {
                _progress.ActiveModelNumber = update.ActiveModelNumber;
                _progress.ActiveModel = update.ActiveModel;
                _progress.TotalModelNumber = update.TotalModelNumber;
            }
            if (update.ViewProgress > 0)
            {
                _progress.ViewProgress = update.ViewProgress;
            }
            if (!string.IsNullOrEmpty(update.DisplayMessage))
            {
                _progress.DisplayMessage = update.DisplayMessage;
            }
            _progress.State = update.State;
            if (OnProgressUpdate != null)
            {
                OnProgressUpdate(update.DisplayMessage, _progress);
            }
        }
    }

    private static void LogExportOptions(IncludeOptions options)
    {
        usd.exporter.revit.log.info("========== Export Options ==========");
        usd.exporter.revit.log.info($"Include Cameras: {options.IncludeCameras}");
        usd.exporter.revit.log.info($"Include Lights: {options.IncludeLights}");
        usd.exporter.revit.log.info($"Include Links: {options.IncludeLinks}");
        usd.exporter.revit.log.info($"Include BIM Data: {options.IncludeBimData}");
        usd.exporter.revit.log.info($"Include Rooms: {options.IncludeRooms}" + (options.IncludeRooms ? $" (Scheme: {options.RoomColorScheme})" : ""));
        usd.exporter.revit.log.info($"Include Spaces: {options.IncludeSpaces}" + (options.IncludeSpaces ? $" (Scheme: {options.SpaceColorScheme})" : ""));
        usd.exporter.revit.log.info($"Include Drawings: {options.IncludeDrawings}" + (options.IncludeDrawings ? $" (Publish Set: {options.DrawingPublishSet})" : ""));
        usd.exporter.revit.log.info($"Instance Families: {options.InstanceFamilies}" + (options.InstanceFamilies ? $" (Style: {options.FamilyInstanceStyle})" : ""));
        string coordinateSystemName;
        switch (options.CoordinateSystem)
        {
            case 0:
                coordinateSystemName = "Internal Origin";
                break;
            case 1:
                coordinateSystemName = "Project Base Point";
                break;
            case 2:
                coordinateSystemName = "Survey Point";
                break;
            case 3:
                coordinateSystemName = "Shared Coordinates";
                break;
            default:
                coordinateSystemName = "Unknown";
                break;
        }
        usd.exporter.revit.log.info($"Coordinate System: {coordinateSystemName}");
        usd.exporter.revit.log.info($"Material Style: {options.MaterialStyle}");
        usd.exporter.revit.log.info($"Material Folder Name: {options.MaterialFolderName}");
        usd.exporter.revit.log.info($"Unit Type: {options.UnitType}");
        usd.exporter.revit.log.info("====================================");
    }

    private static List<char> theBaddies = new List<char>() { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#' };

    private static string removeBadWindowsFilePathChars(this string value)
    {
        foreach (char c in theBaddies)
        {
            value = value.Replace(c, '_');
        }
        return value;
    }
}

public class ProgressUpdate : EventArgs
{
    public string ActiveModel = string.Empty;
    public int ActiveModelNumber = 0;
    public int TotalModelNumber = 1;

    public string ActiveView = string.Empty;
    public int ActiveViewNumber = 0;
    public int TotalViewNumber = 1;
    public string DisplayMessage = string.Empty;
    public double ViewProgress = 0.0;

    public ProgressUpdateState State = ProgressUpdateState.Standard;
}

public enum ProgressUpdateState
{
    Standard,
    Batch,
    Cancelled,
    Complete
}

}

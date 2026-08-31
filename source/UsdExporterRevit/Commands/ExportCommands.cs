// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using UsdExporterRevitSdk;
using Autodesk.Revit.Attributes;
using System.Linq;
using Newtonsoft.Json;

namespace UsdExporterRevit
{
[Transaction(TransactionMode.Manual)]
public class ExportCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var handle = commandData.Application.MainWindowHandle;
        message = string.Empty;
        Document doc = commandData.Application.ActiveUIDocument.Document;

        bool oneClick = false;
        UsdExporterRevitSettings settings = Storage.GetSettings(doc, out oneClick);
        List<View3D> views = new List<View3D>();
        Dictionary<string, bool> viewMap = GetInput.ThreeDViews(doc, out views);

        if (!(oneClick && settings.IsOutputFolderValid()))
        {
            List<ColorFillScheme> roomSchemes = GetInput.RoomSchemes(doc);
            List<ColorFillScheme> spaceSchemes = GetInput.SpaceSchemes(doc);
            List<ViewSheetSet> publishSets = GetInput.PublishSets(doc);
            List<PhaseFilter> phaseFilters = GetInput.PhaseFilters(doc);
            List<View3D> viewTemplates = GetInput.ViewTemplates(doc);
            List<MaterialData> materials = GetInput.Materials(doc, settings);
            List<FamilyData> families = GetInput.Families(doc, settings);

            // reconcile saved views vs whats in the model now
            List<string> viewsToCheck = viewMap.Select(v => v.Key).ToList();
            foreach (string view in viewsToCheck)
            {
                if (settings.ViewsToExport.Contains(view))
                {
                    viewMap[view] = true;
                }
            }
            SettingsDialogResult result = Dialogs.Settings(
                handle,
                SettingsContext.FileExport,
                Newtonsoft.Json.JsonConvert.SerializeObject(settings),
                oneClick,
                roomSchemes.Select(s => s.Name).ToList(),
                spaceSchemes.Select(s => s.Name).ToList(),
                publishSets.Select(s => s.Name).ToList(),
                phaseFilters.Select(p => p.Name).ToList(),
                viewTemplates.Select(v => v.Name).ToList(),
                viewMap,
                materials,
                families
            );

            if (result.Canceled)
            {
                message = "Export Canceled";
                return Result.Cancelled;
            }

            settings = Newtonsoft.Json.JsonConvert.DeserializeObject<UsdExporterRevitSettings>(result.SettingsString);
            Storage.SaveSettings(doc, settings, result.OneClick);
        }

        Result r = Result.Failed;

        ProgressContext context = null;
        if (settings.ViewsToExport.Count > 0)
        {
            context = new ProgressContext();
            context.State = ProgressContextState.Standard;
            context.ActiveViewNumber = 1;
            context.TotalViewNumber = settings.ViewsToExport.Count;
            context.ActiveView = settings.ViewsToExport[0];
            context.DisplayMessage = "Starting Export";

            Dialogs.Progress(handle, context);
            Exporter.InitiateProgressContext(context.ToProgressUpdate());
            Exporter.AddProgressUpdateCallback(App.Update_Progress);

            // Subscribe to dialog close event
            Dialogs.ProgressDialogStatus += OnProgressDialogClosed;
        }

        if (settings.ViewsToExport.Count == 1)
        {
            View3D view = views.Where(v => v.Name == settings.ViewsToExport[0]).FirstOrDefault();
            if (view != null)
            {
                try
                {
                    r = Exporter.ExportView(commandData.Application, view, settings);
                }
                catch (Exception ex)
                {
                    message = $"Export failed: {ex.Message}";
                    usd.exporter.revit.log.error(message);
                    Dialogs.ProgressDialogStatus -= OnProgressDialogClosed;
                    return Result.Failed;
                }

                // Check if export was cancelled
                if (Exporter.IsCancelled())
                {
                    Dialogs.ProgressDialogStatus -= OnProgressDialogClosed;
                    return Result.Cancelled;
                }

                App.SetLastExportPath(settings.File.OutputFolder + settings.File.FileName + settings.File.Extension);
                if (!settings.File.CheckExtension())
                {
                    message = $"This is not a usd file: {App.GetLastExportPath()}";
                    usd.exporter.revit.log.warning(message);
                    return Result.Cancelled;
                }
            }
            else
            {
                message = $"Unable to find {settings.ViewsToExport[0]} for export...";
                usd.exporter.revit.log.warning(message);
                return Result.Cancelled;
            }
        }
        else
        {
            string rootOutput = settings.File.OutputFolder;
            string filePath = string.Empty;
            int i = 0;
            foreach (string viewName in settings.ViewsToExport)
            {
                i++;
                if (context == null)
                {
                    Dialogs.ProgressDialogStatus -= OnProgressDialogClosed;
                    usd.exporter.revit.log.warning("View does not exist.");
                    return Result.Cancelled;
                }

                // Check if export was cancelled
                if (Exporter.IsCancelled())
                {
                    Dialogs.ProgressDialogStatus -= OnProgressDialogClosed;
                    return Result.Cancelled;
                }

                context.ActiveView = viewName;
                context.ActiveViewNumber = i;
                context.DisplayMessage = "Opening View";
                Dialogs.UpdateProgress(context);

                View3D view = views.Where(v => v.Name == viewName).FirstOrDefault();
                if (view != null)
                {
                    string cleanViewName = App.RemoveBadWindowsFilePathChars(view.Name);
                    settings.File.OutputFolder = rootOutput + "/" + cleanViewName;
                    settings.File.FileName = cleanViewName;

                    string exportPath = settings.File.OutputFolder + settings.File.FileName + settings.File.Extension;
                    if (!settings.File.CheckExtension())
                    {
                        message = $"This is not a usd file: {exportPath}";
                        usd.exporter.revit.log.warning(message);
                        continue;
                    }
                    try
                    {
                        r = Exporter.ExportView(commandData.Application, view, settings);
                    }
                    catch (Exception ex)
                    {
                        usd.exporter.revit.log.error($"Export of {viewName} failed: {ex.Message}");
                        continue;
                    }

                    // Check if export was cancelled
                    if (Exporter.IsCancelled())
                    {
                        Dialogs.ProgressDialogStatus -= OnProgressDialogClosed;
                        return Result.Cancelled;
                    }

                    if (r == Result.Succeeded && string.IsNullOrEmpty(filePath))
                    {
                        filePath = settings.File.OutputFolder + settings.File.FileName + settings.File.Extension;
                    }
                }
                else
                {
                    usd.exporter.revit.log.warning($"Unable to find {viewName} for export...");
                }
            }
            r = Result.Succeeded;
            App.SetLastExportPath(filePath);
        }
        if (context != null)
        {
            context.State = ProgressContextState.Complete;
            Dialogs.UpdateProgress(context);
        }

        // Unsubscribe from dialog close event
        Dialogs.ProgressDialogStatus -= OnProgressDialogClosed;
        usd.exporter.revit.log.info($"Export Completed: Thread ID: {Thread.CurrentThread.ManagedThreadId}, Timestamp: {DateTime.Now}");

        return r;
    }

    private static void OnProgressDialogClosed(object sender, ProgressContext context)
    {
        // If the dialog was cancelled, notify the Exporter
        if (context.State == ProgressContextState.Cancelled)
        {
            Exporter.CancelExport();
        }
    }
}

[Transaction(TransactionMode.Manual)]
public class ExportBatchCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var handle = commandData.Application.MainWindowHandle;
        message = string.Empty;
        Document doc = commandData.Application.ActiveUIDocument.Document;

        bool oneClick = false;
        UsdExporterRevitSettings settings = new UsdExporterRevitSettings();
        List<View3D> views = new List<View3D>();
        Dictionary<string, bool> viewMap = GetInput.ThreeDViews(doc, out views);
        List<ColorFillScheme> roomSchemes = GetInput.RoomSchemes(doc);
        List<ColorFillScheme> spaceSchemes = GetInput.SpaceSchemes(doc);
        List<ViewSheetSet> publishSets = GetInput.PublishSets(doc);
        List<PhaseFilter> phaseFilters = GetInput.PhaseFilters(doc);
        List<View3D> viewTemplates = GetInput.ViewTemplates(doc);
        List<MaterialData> materials = GetInput.Materials(doc, settings);
        List<FamilyData> families = GetInput.Families(doc, settings);

        // reconcile saved views vs whats in the model now
        List<string> viewsToCheck = viewMap.Select(v => v.Key).ToList();
        foreach (string view in viewsToCheck)
        {
            if (settings.ViewsToExport.Contains(view))
            {
                viewMap[view] = true;
            }
        }
        SettingsDialogResult result = Dialogs.Settings(
            handle,
            SettingsContext.BatchExport,
            Newtonsoft.Json.JsonConvert.SerializeObject(settings),
            oneClick,
            roomSchemes.Select(s => s.Name).ToList(),
            spaceSchemes.Select(s => s.Name).ToList(),
            publishSets.Select(s => s.Name).ToList(),
            phaseFilters.Select(p => p.Name).ToList(),
            viewTemplates.Select(v => v.Name).ToList(),
            viewMap,
            materials,
            families
        );

        if (result.Canceled)
        {
            message = "Batch Export Canceled";
            usd.exporter.revit.log.info(message);
            return Result.Cancelled;
        }

        settings = Newtonsoft.Json.JsonConvert.DeserializeObject<UsdExporterRevitSettings>(result.SettingsString);
        if (!(settings.IsInputFolderValid() && settings.IsBatchOutputFolderValid()))
        {
            message = "Invalid Folders for Batch Export";
            usd.exporter.revit.log.error($"INVALID FOLDERS FOR BATCH \"{settings.Batch.InputFolder}\" or \"{settings.Batch.OutputFolder}\"");
            return Result.Cancelled;
        }

        ProgressContext context = new ProgressContext();
        context.State = ProgressContextState.Batch;

        // Subscribe to dialog close event
        Dialogs.ProgressDialogStatus += OnProgressDialogClosedBatch;

        Dialogs.Progress(handle, context);
        Exporter.InitiateProgressContext(context.ToProgressUpdate());
        Exporter.AddProgressUpdateCallback(App.Update_Progress);

        Result r = Exporter.ExportBatch(commandData.Application, settings);

        context.State = ProgressContextState.Complete;
        Dialogs.UpdateProgress(context);

        // Unsubscribe from dialog close event
        Dialogs.ProgressDialogStatus -= OnProgressDialogClosedBatch;

        return r;
    }

    private static void OnProgressDialogClosedBatch(object sender, ProgressContext context)
    {
        usd.exporter.revit.log.info($"ExportBatchCommand: Progress dialog closed with state: {context.State}");

        // If the dialog was cancelled, notify the Exporter
        if (context.State == ProgressContextState.Cancelled)
        {
            Exporter.CancelExport();
        }
    }
}

[Transaction(TransactionMode.Manual)]
public class SettingsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var handle = commandData.Application.MainWindowHandle;
        message = string.Empty;
        Document doc = commandData.Application.ActiveUIDocument.Document;

        bool oneClick = false;
        UsdExporterRevitSettings settings = Storage.GetSettings(doc, out oneClick);

        List<ColorFillScheme> roomSchemes = GetInput.RoomSchemes(doc);
        List<ColorFillScheme> spaceSchemes = GetInput.SpaceSchemes(doc);
        List<ViewSheetSet> publishSets = GetInput.PublishSets(doc);
        List<PhaseFilter> phaseFilters = GetInput.PhaseFilters(doc);
        List<View3D> viewTemplates = GetInput.ViewTemplates(doc);
        List<View3D> views = new List<View3D>();
        Dictionary<string, bool> viewMap = GetInput.ThreeDViews(doc, out views);
        List<MaterialData> materials = GetInput.Materials(doc, settings);
        List<FamilyData> families = GetInput.Families(doc, settings);

        // reconcile saved views vs whats in the model now
        List<string> viewsToCheck = viewMap.Select(v => v.Key).ToList();
        foreach (string view in viewsToCheck)
        {
            if (settings.ViewsToExport.Contains(view))
            {
                viewMap[view] = true;
            }
        }

        SettingsDialogResult result = Dialogs.Settings(
            handle,
            SettingsContext.RibbonClick,
            Newtonsoft.Json.JsonConvert.SerializeObject(settings),
            oneClick,
            roomSchemes.Select(s => s.Name).ToList(),
            spaceSchemes.Select(s => s.Name).ToList(),
            publishSets.Select(s => s.Name).ToList(),
            phaseFilters.Select(p => p.Name).ToList(),
            viewTemplates.Select(v => v.Name).ToList(),
            viewMap,
            materials,
            families
        );

        if (!result.Canceled)
        {
            if (result.Save)
            {
                settings = Newtonsoft.Json.JsonConvert.DeserializeObject<UsdExporterRevitSettings>(result.SettingsString);
                Storage.SaveSettings(doc, settings, result.OneClick);
            }
        }
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.ReadOnly)]
public class AboutCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var handle = commandData.Application.MainWindowHandle;
        UI.About about = new UI.About(handle);
        about.ShowDialog();
        return Result.Succeeded;
    }
}

internal static class GetInput
{
    public static long GetValue(this ElementId id)
    {
        return id.Value;
    }
    public static List<ColorFillScheme> RoomSchemes(Document doc)
    {
        ElementId roomCategoryId = new ElementId(BuiltInCategory.OST_Rooms);
        return new FilteredElementCollector(doc).OfClass(typeof(ColorFillScheme)).Cast<ColorFillScheme>().Where(c => c.CategoryId.GetValue() == roomCategoryId.GetValue()).ToList();
    }
    public static List<ColorFillScheme> SpaceSchemes(Document doc)
    {
        ElementId roomCategoryId = new ElementId(BuiltInCategory.OST_MEPSpaces);
        return new FilteredElementCollector(doc).OfClass(typeof(ColorFillScheme)).Cast<ColorFillScheme>().Where(c => c.CategoryId.GetValue() == roomCategoryId.GetValue()).ToList();
    }

    public static List<ViewSheetSet> PublishSets(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>().Where(s => s != null).ToList();
    }

    public static List<PhaseFilter> PhaseFilters(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>().ToList();
    }

    public static List<View3D> ViewTemplates(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().Where(v => v.IsTemplate).ToList();
    }

    public static Dictionary<string, bool> ThreeDViews(Document doc, out List<View3D> views)
    {
        views = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().Where(v => !v.IsTemplate).ToList();
        Dictionary<string, bool> output = new Dictionary<string, bool>();
        views = views.OrderBy(v => v.Name).ToList();
        foreach (View3D view in views)
        {
            if (view.Id.GetValue() == doc.ActiveView.Id.GetValue())
            {
                output.Add(view.Name, true); // todo check extensible storage...
            }
            else
            {
                output.Add(view.Name, false);
            }
        }

        return output;
    }

    public static List<MaterialData> Materials(Document doc, UsdExporterRevitSettings settings)
    {
        List<MaterialData> data = new List<MaterialData>();
        List<Material> materials = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().ToList();
        foreach (Material material in materials)
        {
            MaterialData m = new MaterialData();
            m.Id = material.Id.GetValue();
            m.Name = material.Name;
            if (settings.Mappings.Materials.UserMapped.Any(i => i.Id == m.Id))
            {
                UserMaterialMapping mapping = settings.Mappings.Materials.UserMapped.Where(i => i.Id == m.Id).First();
                m.Mapped = true;
                m.MdlPath = mapping.MdlPath;
                m.MdlModule = mapping.MdlModule;
            }
            data.Add(m);
        }
        return data.OrderBy(d => d.Name).ToList();
    }

    public static List<FamilyData> Families(Document doc, UsdExporterRevitSettings settings)
    {
        List<FamilyData> data = new List<FamilyData>();
        List<FamilyInstance> families = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(f => f.Category.CategoryType != CategoryType.Annotation).ToList();
        foreach (FamilyInstance instance in families)
        {
            ElementId typeId = instance.GetTypeId();
            long idValue = typeId.GetValue();
            if (!data.Any(d => d.Id == idValue))
            {
                ElementType family = doc.GetElement(typeId) as ElementType;
                FamilyData f = new FamilyData();
                f.Id = idValue;
                f.Category = family.Category.Name;
                f.TypeName = family.Name;
                f.FamilyName = family.FamilyName;
                if (settings.Mappings.FamilyTypes.UserMapped.Any(m => m.Id == f.Id))
                {
                    UserFamilyTypeMapping mapping = settings.Mappings.FamilyTypes.UserMapped.Where(m => m.Id == f.Id).First();
                    f.Mapped = true;
                    f.AssetPath = mapping.AssetPath;
                }
                data.Add(f);
            }
        }
        return data.OrderBy(d => d.FamilyName + " - " + d.TypeName).ToList();
    }
}
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace UsdExporterRevit
{
public class Dialogs
{
    private static UI.Progress progressDialog = null;
    public static event EventHandler<ProgressContext> ProgressDialogStatus;

    public static SettingsDialogResult Settings(
        IntPtr windowHandle,
        SettingsContext context,
        string settingsString,
        bool oneClickEnabled,
        List<string> roomSchemes,
        List<string> spaceSchemes,
        List<string> publishSets,
        List<string> phaseFilters,
        List<string> viewTemplates,
        Dictionary<string, bool> views,
        List<MaterialData> materials,
        List<FamilyData> families
    )
    {
        UI.Settings settings = new UI.Settings(context, settingsString, oneClickEnabled, roomSchemes, spaceSchemes, publishSets, phaseFilters, viewTemplates, views, materials, families);

        HwndSource hwndSource = HwndSource.FromHwnd(windowHandle);
        Window wnd = hwndSource.RootVisual as Window;
        settings.Owner = wnd;
        settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        settings.ShowDialog();
        SettingsDialogResult result = settings.Result;
        result.SettingsString = settings.GetSettings();
        return result;
    }

    public static void Progress(IntPtr windowHandle, ProgressContext context)
    {
        // Publish the window before export updates can arrive. Never run Revit
        // API work on this STA thread.
        var ready = new System.Threading.Tasks.TaskCompletionSource<bool>();
        {
            Exception startupError = null;
            Thread thread = new Thread(delegate() {
                UI.Progress dialog = null;
                try
                {
                    dialog = new UI.Progress(context.Snapshot());
                    dialog.Closed += ProgressDialog_Closed;
                    dialog.Dispatcher.UnhandledException += (sender, args) =>
                    {
                        usd.exporter.revit.log.error($"Progress update failed: {args.Exception}");
                        args.Handled = true;
                        dialog.Context.State = ProgressContextState.Cancelled;
                        dialog.Close();
                    };
                    Volatile.Write(ref progressDialog, dialog);
                    dialog.Loaded += (sender, args) => ready.TrySetResult(true);
                    // ShowDialog owns a dispatcher frame which exits on Close.
                    dialog.ShowDialog();
                }
                catch (Exception ex)
                {
                    startupError = ex;
                    usd.exporter.revit.log.error($"Progress dialog failed: {ex}");
                }
                finally
                {
                    if (dialog != null)
                    {
                        Interlocked.CompareExchange(ref progressDialog, null, dialog);
                    }
                    ready.TrySetResult(true);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Task.GetAwaiter().GetResult();
            if (startupError != null)
            {
                throw new InvalidOperationException("Could not open export progress.", startupError);
            }
        }
    }

    public static bool ProgressShowing()
    {
        return Volatile.Read(ref progressDialog) != null;
    }

    public static void UpdateProgress(ProgressContext context)
    {
        // Capture both objects: queued delegates must never dereference a
        // replacement window or mutable context belonging to another update.
        var dialog = Volatile.Read(ref progressDialog);
        var snapshot = context.Snapshot();
        if (dialog != null && !dialog.Dispatcher.HasShutdownStarted)
        {
            dialog.Dispatcher.BeginInvoke((Action)(() =>
                                                   {
                                                       if (dialog.IsLoaded && dialog.Context.State != ProgressContextState.Cancelled)
                                                       {
                                                           dialog.Context = snapshot;
                                                       }
                                                   }));
        }
    }

    private static void ProgressDialog_Closed(object sender, EventArgs e)
    {
        var dialog = (UI.Progress)sender;
        Interlocked.CompareExchange(ref progressDialog, null, dialog);
        ProgressDialogStatus?.Invoke(sender, dialog.Context.Snapshot());
    }
}
#region settings
public class SettingsDialogResult
{
    public bool Canceled = false;
    public bool OneClick = false;
    public bool Save = false;
    public string ExportJsonUri = string.Empty;
    public string SettingsString = string.Empty;
}

public enum SettingsContext
{
    RibbonClick,
    FileExport,
    BatchExport
}

public class FamilyData
{
    public long Id = -1;
    public string TypeName = string.Empty;
    public string FamilyName = string.Empty;
    public string Category = string.Empty;
    public bool Mapped = false;
    public string AssetPath;
}

public class MaterialData
{
    public long Id = -1;
    public string Name = string.Empty;
    public bool Mapped = false;
    public string MdlPath = string.Empty;
    public string MdlModule = string.Empty;
}
#endregion

public class ProgressContext : EventArgs
{
    public string ActiveModel = string.Empty;
    public int ActiveModelNumber = 0;
    public int TotalModelNumber = 1;

    public string ActiveView = string.Empty;
    public int ActiveViewNumber = 0;
    public int TotalViewNumber = 1;
    public string DisplayMessage = string.Empty;
    public double ViewProgress = 0.0;

    public ProgressContextState State = ProgressContextState.Standard;

    public ProgressContext Snapshot() => (ProgressContext)MemberwiseClone();
}

public enum ProgressContextState
{
    Standard,
    Batch,
    Cancelled,
    Complete
}
}

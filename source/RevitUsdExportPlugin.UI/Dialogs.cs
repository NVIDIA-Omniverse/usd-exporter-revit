// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace RevitUsdExportPlugin
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
        revit.log.info($"Starting conversion Progress: Thread ID: {Thread.CurrentThread.ManagedThreadId}, Timestamp: {DateTime.Now}");
#pragma warning disable CA1416
        Thread thread = new Thread(delegate() {
            progressDialog = new UI.Progress(context);
            progressDialog.Closed += ProgressDialog_Closed;
            progressDialog.Show();
            System.Windows.Threading.Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA); // needs to be STA or throws exception
        thread.Start();
#pragma warning restore CA1416
    }

    public static bool ProgressShowing()
    {
        return progressDialog != null;
    }

    public static void UpdateProgress(ProgressContext context)
    {
        if (progressDialog != null)
        {
            progressDialog.Dispatcher.BeginInvoke((Action)(() =>
                                                           {
                                                               if (progressDialog != null && progressDialog.IsLoaded)
                                                               {
                                                                   progressDialog.Context = context;
                                                               }
                                                           }));
        }
    }

    private static void ProgressDialog_Closed(object sender, EventArgs e)
    {
        if (ProgressDialogStatus != null)
        {
            ProgressDialogStatus(sender, progressDialog.Context);
        }
        progressDialog = null;
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
}

public enum ProgressContextState
{
    Standard,
    Batch,
    Cancelled,
    Complete
}
}

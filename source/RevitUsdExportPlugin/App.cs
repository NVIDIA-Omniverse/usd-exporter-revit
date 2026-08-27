// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RevitUsdExportSdk;
using System.Runtime.InteropServices;
using RevitUsdExport.Utilities;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Media.Imaging;

namespace RevitUsdExportPlugin
{
public class App : IExternalApplication
{
    private static int closedDocHash = 0;
    private static UIControlledApplication uiapp = null;

    private static EventHandler handler;
    private static ExternalEvent exEvent;

    // Track document titles to detect Save As operations
    private static Dictionary<int, string> documentTitleHistory = new Dictionary<int, string>();
#if REV2026
    private const string payloadPipeName = "RevitUsdExportTestHarness_Payload2026";
    private const string modelPipeName = "RevitUsdExportTestHarness_OpenModel2026";
    private const string messagePipeName = "RevitUsdExportTestHarness_Message2026";
#endif
#if REV2025
    private const string payloadPipeName = "RevitUsdExportTestHarness_Payload2025";
    private const string modelPipeName = "RevitUsdExportTestHarness_OpenModel2025";
    private const string messagePipeName = "RevitUsdExportTestHarness_Message2025";
#endif
#if REV2024
    private const string payloadPipeName = "RevitUsdExportTestHarness_Payload2024";
    private const string modelPipeName = "RevitUsdExportTestHarness_OpenModel2024";
    private const string messagePipeName = "RevitUsdExportTestHarness_Message2024";
#endif
    private static ClientPipe payloadPipe;
    private static ClientPipe modelPipe;
    private static ClientPipe messagePipe;

    private static bool IsRunningBatch = false;

    private static string TestHarnessFileToOpen = string.Empty;
    private static Document TestHarnessFileToClose = null;
    private static bool TestHarnessOpeningModel = false;
    private static RevitUsdExportSettings TestHarnessSettings = null;

    private static bool piped = false;
    private static bool firstPayload = true;

    private static string LastExportPath = string.Empty;

    public Result OnShutdown(UIControlledApplication application)
    {
        closePipes();
        application.DialogBoxShowing -= Application_DialogBoxShowing;
        application.ControlledApplication.DocumentClosing -= ControlledApplication_DocumentClosing;
        application.ControlledApplication.DocumentClosed -= ControlledApplication_DocumentClosed;
        application.ControlledApplication.DocumentOpened -= ControlledApplication_DocumentOpened;
        application.ControlledApplication.DocumentSavedAs -= ControlledApplication_DocumentSavedAs;
        return Result.Succeeded;
    }

    public Result OnStartup(UIControlledApplication application)
    {
        cleanup();
        uiapp = application;
        if (!CodeIntegrity.VerifyBeforeNativeStartup())
        {
            return Result.Failed;
        }
        revit.usd.export.core.startup();
        revit.usd.export.core.startupLog();

#if REV2026
        revit.log.info("OMNIVERSE REVIT ADDIN for REVIT 2026");
#endif
#if REV2025
        revit.log.info("OMNIVERSE REVIT ADDIN for REVIT 2025");
#endif
#if REV2024
        revit.log.info("OMNIVERSE REVIT ADDIN for REVIT 2024");
#endif
        setupRibbon(uiapp);

        // test harness event handler
        handler = new EventHandler();
        exEvent = ExternalEvent.Create(handler);
        openPipes();

        // events
        application.DialogBoxShowing += Application_DialogBoxShowing;
        application.ControlledApplication.DocumentClosing += ControlledApplication_DocumentClosing;
        application.ControlledApplication.DocumentClosed += ControlledApplication_DocumentClosed;
        application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
        application.ControlledApplication.DocumentSavedAs += ControlledApplication_DocumentSavedAs;

        return Result.Succeeded;
    }

    private void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
    {
        // Track the initial title when document is opened
        Document doc = e.Document;
        int docHash = doc.GetHashCode();
        if (!documentTitleHistory.ContainsKey(docHash))
        {
            documentTitleHistory[docHash] = doc.Title;
        }

        if (!IsRunningBatch && piped) // this condition happens on the first model open of the test harness
        {
            ReadyForPayload();
        }
        else if (IsRunningBatch && piped && TestHarnessOpeningModel)
        {
            ReadyForPayload();
        }
    }

    private static void cleanup()
    {
        string logs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".revit_usd_export_plugin",
            "logs");
        try
        {
            DirectoryInfo dir = new DirectoryInfo(logs);
            if (dir.Exists)
            {
                // Layout: logs/Revit-<version>/<ClientName>-<version>/<YYYYMMDD_HHMMSS>.log
                foreach (DirectoryInfo versionDir in dir.GetDirectories())
                {
                    foreach (DirectoryInfo clientDir in versionDir.GetDirectories())
                    {
                        foreach (FileInfo file in clientDir.GetFiles("*.log"))
                        {
                            TimeSpan span = DateTime.UtcNow - file.CreationTimeUtc;
                            if (span.Days > 15)
                            {
                                file.Delete();
                            }
                        }
                        if (clientDir.GetFileSystemInfos().Length == 0)
                        {
                            clientDir.Delete();
                        }
                    }
                    // Only remove the version directory when it is truly empty
                    // (no files AND no subdirectories); otherwise Delete() throws
                    // IOException "The directory is not empty".
                    if (versionDir.GetFileSystemInfos().Length == 0)
                    {
                        versionDir.Delete();
                    }
                }
            }
        }
        catch (System.IO.IOException)
        {
            // Log cleanup is best-effort and must never block addin startup.
        }
    }

    static void setupRibbon(UIControlledApplication app)
    {
        FileInfo assembly = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string resources = Path.Combine(assembly.Directory.FullName, "img");

        app.CreateRibbonTab("Omniverse");
        RibbonPanel panel = app.CreateRibbonPanel("Omniverse", "Export");
        PushButtonData data = new PushButtonData("ov_export", "Export View", Assembly.GetExecutingAssembly().Location, "RevitUsdExportPlugin.ExportCommand");
        PushButton publish = panel.AddItem(data) as PushButton;
        publish.ToolTip = "Publish a view or multiple views to Usd";
        BitmapImage pubimg = new BitmapImage(new Uri(Path.Combine(resources, "publish.png")));
        publish.LargeImage = pubimg;

        PushButtonData batchData = new PushButtonData("ov_batch", "Batch", Assembly.GetExecutingAssembly().Location, "RevitUsdExportPlugin.ExportBatchCommand");
        PushButton batch = panel.AddItem(batchData) as PushButton;
        batch.ToolTip = "Batch publish a folder of Revit models to Usd";
        BitmapImage batchimg = new BitmapImage(new Uri(Path.Combine(resources, "publish_batch.png")));
        batch.LargeImage = batchimg;

        RibbonPanel settingsPanel = app.CreateRibbonPanel("Omniverse", "Info");
        PushButtonData settingsData = new PushButtonData("ov_settings", "Settings", Assembly.GetExecutingAssembly().Location, "RevitUsdExportPlugin.SettingsCommand");
        PushButton settings = settingsPanel.AddItem(settingsData) as PushButton;
        settings.ToolTip = "Settings for Publish and Batch Publish to Usd";
        BitmapImage settingimg = new BitmapImage(new Uri(Path.Combine(resources, "settings.png")));
        settings.LargeImage = settingimg;

        PushButtonData aboutData = new PushButtonData("ov_about", "About", Assembly.GetExecutingAssembly().Location, "RevitUsdExportPlugin.AboutCommand");
        PushButton about = settingsPanel.AddItem(aboutData) as PushButton;
        about.ToolTip = "Information about the Omniverse Revit USD Export Plugin";
        BitmapImage aboutimg = new BitmapImage(new Uri(Path.Combine(resources, "about.png")));
        about.LargeImage = aboutimg;
    }

    public static void Update_Progress(object sender, ProgressUpdate e)
    {
        Dialogs.UpdateProgress(e.ToProgressContext());
    }

    private void ControlledApplication_DocumentClosing(object sender, Autodesk.Revit.DB.Events.DocumentClosingEventArgs e)
    {
        closedDocHash = e.Document.GetHashCode();
    }

    private void ControlledApplication_DocumentClosed(object sender, Autodesk.Revit.DB.Events.DocumentClosedEventArgs e)
    {
        // Clean up tracking when document is closed
        if (documentTitleHistory.ContainsKey(closedDocHash))
        {
            documentTitleHistory.Remove(closedDocHash);
        }
        closedDocHash = 0;
    }

    private void ControlledApplication_DocumentSavedAs(object sender, Autodesk.Revit.DB.Events.DocumentSavedAsEventArgs e)
    {
        Document doc = e.Document;
        int docHash = doc.GetHashCode();

        // Check if we have previous title tracked
        if (documentTitleHistory.TryGetValue(docHash, out string previousTitle))
        {
            // Check if title changed (indicating Save As with new name)
            if (previousTitle != doc.Title)
            {
                bool oneClick = false;
                bool hasInternalSettings = Storage.isInternalSettings(doc, out oneClick);

                // Only migrate for external storage mode
                if (!oneClick)
                {
                    MigrateExternalSettings(previousTitle, doc.Title);
                }

                // Update tracked title
                documentTitleHistory[docHash] = doc.Title;
            }
        }
        else
        {
            // First save, track the title
            documentTitleHistory[docHash] = doc.Title;
        }
    }

    private static void MigrateExternalSettings(string oldTitle, string newTitle)
    {
        try
        {
            string cleanOldTitle = RemoveBadWindowsFilePathChars(oldTitle);
            string cleanNewTitle = RemoveBadWindowsFilePathChars(newTitle);

            string oldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $@"Omniverse/Revit/{cleanOldTitle}");
            string newPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $@"Omniverse/Revit/{cleanNewTitle}");

            string oldSettingFile = Path.Combine(oldPath, "settings.json");
            string newSettingFile = Path.Combine(newPath, "settings.json");

            // Only migrate if old settings exist and new location doesn't have settings yet
            if (File.Exists(oldSettingFile) && !File.Exists(newSettingFile))
            {
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }

                File.Copy(oldSettingFile, newSettingFile, false);
            }
        }
        catch (Exception ex)
        {
            revit.log.error($"Revit USD Export Plugin: Failed to migrate settings: {ex.Message}");
        }
    }

    private void Application_DialogBoxShowing(object sender, Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs e)
    {
        if (IsRunningBatch)
        {
            if (e.DialogId == "TaskDialog_Unresolved_References")
            {
                e.OverrideResult(1002);
            }
            if (e.DialogId == "TaskDialog_Loading_Transmitted_File")
            {
                e.OverrideResult(1002);
            }
            if (e.DialogId == "TaskDialog_Cannot_Find_Central_Model")
            {
                e.OverrideResult(1);
            }
            if (e.DialogId == "TaskDialog_Local_File_Resides_In_Central_Model_Location")
            {
                e.OverrideResult(1);
            }
            if (e.DialogId == "TaskDialog_Copied_Central_Model")
            {
                e.OverrideResult(1);
            }
            if (e.DialogId == "Dialog_Revit_Partitions")
            {
                e.OverrideResult(2);
            }
            if (e.DialogId == "Dialog_Revit_DocWarnDialog")
            {
                e.OverrideResult(1);
            }
            if (e.DialogId == "TaskDialog_Save_File")
            {
                e.OverrideResult(1);
            }
            if (e.DialogId == "TaskDialog_Model_Opened_By_Another_User")
            {
                e.OverrideResult(1001);
            }
            if (piped)
            {
                if (e.DialogId == "TaskDialog_External_Tools_External_Tool_Failure")
                {
                    messagePipe.WriteString("exception thrown");
                    e.OverrideResult(1);
                }
                else if (e is Autodesk.Revit.UI.Events.TaskDialogShowingEventArgs)
                {
                    Autodesk.Revit.UI.Events.TaskDialogShowingEventArgs tde = e as Autodesk.Revit.UI.Events.TaskDialogShowingEventArgs;
                    if (tde.Message == "Autodesk.Revit.Exceptions.ExternalApplicationException")
                    {
                        messagePipe.WriteString("exception thrown");
                        e.OverrideResult(1);
                    }
                    if (tde.Message.StartsWith("Exception: "))
                    {
                        messagePipe.WriteString("exception thrown");
                        e.OverrideResult(1);
                    }
                }
                else
                {
                    e.OverrideResult(1);
                }
            }
        }
        else
        {
            if (e is Autodesk.Revit.UI.Events.TaskDialogShowingEventArgs)
            {
                if (Dialogs.ProgressShowing())
                {
                    ProgressContext cancel = new ProgressContext();
                    cancel.State = ProgressContextState.Cancelled;
                    Dialogs.UpdateProgress(cancel);
                }
            }
        }
    }

    public static void ReadyForPayload()
    {
        revit.log.info("ready for payload");
        messagePipe.WriteString("ready for payload");
        TestHarnessOpeningModel = false;
    }

#region TEST HARNESS
    private static void openPipes()
    {
        // Only connect when launched by the test harness (avoids well-known pipe spoofing).
        string enabled = Environment.GetEnvironmentVariable("REVIT_USD_EXPORT_TEST_HARNESS");
        if (string.IsNullOrWhiteSpace(enabled)
            || !(enabled.Trim() == "1"
                || enabled.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!piped)
        {
            try
            {
                payloadPipe = new ClientPipe(".", payloadPipeName, p => p.StartStringReaderAsync());
                payloadPipe.DataReceived += PayloadPipe_DataReceived;
                payloadPipe.Connect();
                modelPipe = new ClientPipe(".", modelPipeName, p => p.StartStringReaderAsync());
                modelPipe.DataReceived += ModelPipe_DataReceived;
                modelPipe.Connect();
                messagePipe = new ClientPipe(".", messagePipeName, p => p.StartStringReaderAsync());
                messagePipe.DataReceived += MessagePipe_DataReceived;
                messagePipe.Connect();
            }
            catch (Exception ex)
            {
                revit.log.info(ex.Message);
                revit.log.info("pipe connection timed out, expected if not being run by harness");
            }
        }
    }

    private static void closePipes()
    {
        if (piped)
        {
            payloadPipe.Close();
            payloadPipe.DataReceived -= PayloadPipe_DataReceived;
            payloadPipe = null;
            modelPipe.Close();
            modelPipe.DataReceived -= ModelPipe_DataReceived;
            modelPipe = null;
            messagePipe.Close();
            messagePipe.DataReceived -= MessagePipe_DataReceived;
            messagePipe = null;
            piped = false;
        }
    }

    private static void PayloadPipe_DataReceived(object sender, PipeEventArgs e)
    {
        revit.log.info(e.Msg);
        if (!piped)
        {
            piped = true;
        }
        if (!firstPayload) // payload server sends out a generic message when it recieves a connection, we want to ignore that
        {
            RevitUsdExportSettings settings = Newtonsoft.Json.JsonConvert.DeserializeObject<RevitUsdExportSettings>(e.Msg);
            IsRunningBatch = true;
            TestHarnessSettings = settings;
            MakeRequest(ExportEvent.BatchExport);
        }
        else
        {
            firstPayload = false; // got connection to server pipe, ready for payloads
        }
    }
    private static void ModelPipe_DataReceived(object sender, PipeEventArgs e)
    {
        revit.log.info(e.Msg);
        if (!piped)
        {
            piped = true;
        }
        if (e.Msg != "hello")
        {
            if (File.Exists(e.Msg))
            {
                TestHarnessFileToOpen = e.Msg;
                MakeRequest(ExportEvent.OpenModel);
            }
            else
            {
                messagePipe.WriteString($"OPENFAIL {e.Msg}");
            }
        }
    }

    private static void MessagePipe_DataReceived(object sender, PipeEventArgs e)
    {
        revit.log.info(e.Msg);
        if (!piped)
        {
            piped = true;
        }
    }

    public static void TaskFinished()
    {
        if (piped)
        {
            revit.log.info("task completed");
            messagePipe.WriteString("task completed");
            IsRunningBatch = false;
            TestHarnessSettings = null;
            // process is closed by the test harness
        }
    }

    private static void MakeRequest(ExportEvent e)
    {
        handler.Request.Make(e);
        exEvent.Raise();
    }
#endregion

    private static List<char> theBaddies = new List<char>() { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#' };

    public static string RemoveBadWindowsFilePathChars(string value)
    {
        foreach (char c in theBaddies)
        {
            value = value.Replace(c, '_');
        }
        return value;
    }

    public static RevitUsdExportSettings GetTestHarnessSettings()
    {
        return TestHarnessSettings;
    }

    public static string GetTestHarnessFileToOpen()
    {
        return TestHarnessFileToOpen;
    }

    public static void SetTestHarnessFileToOpen(string fileToOpen)
    {
        TestHarnessFileToOpen = fileToOpen;
    }

    public static Document GetTestHarnessFileToClose()
    {
        return TestHarnessFileToClose;
    }

    public static void SetTestHarnessFileToClose(Document fileToClose)
    {
        TestHarnessFileToClose = fileToClose;
    }

    public static void SetTestHarnessOpeningModel(bool openingModel)
    {
        TestHarnessOpeningModel = openingModel;
    }

    public static string GetLastExportPath()
    {
        return LastExportPath;
    }

    public static void SetLastExportPath(string path)
    {
        LastExportPath = path;
    }
}

public static class ProgressInterop
{
    public static ProgressContext ToProgressContext(this ProgressUpdate e)
    {
        ProgressContext context = new ProgressContext();
        context.ViewProgress = e.ViewProgress;
        context.ActiveViewNumber = e.ActiveViewNumber;
        context.ActiveView = e.ActiveView;
        context.TotalViewNumber = e.TotalViewNumber;
        context.ActiveModel = e.ActiveModel;
        context.ActiveModelNumber = e.ActiveModelNumber;
        context.TotalModelNumber = e.TotalModelNumber;
        context.DisplayMessage = e.DisplayMessage;
        context.State = (ProgressContextState)(int)e.State;
        return context;
    }

    public static ProgressUpdate ToProgressUpdate(this ProgressContext e)
    {
        ProgressUpdate context = new ProgressUpdate();
        context.ViewProgress = e.ViewProgress;
        context.ActiveViewNumber = e.ActiveViewNumber;
        context.ActiveView = e.ActiveView;
        context.TotalViewNumber = e.TotalViewNumber;
        context.ActiveModel = e.ActiveModel;
        context.ActiveModelNumber = e.ActiveModelNumber;
        context.TotalModelNumber = e.TotalModelNumber;
        context.DisplayMessage = e.DisplayMessage;
        context.State = (ProgressUpdateState)(int)e.State;
        return context;
    }
}
}

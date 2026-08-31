// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using UsdExporterRevitSdk;
using System.IO;

namespace UsdExporterRevit
{
public class EventHandler : IExternalEventHandler
{
    private string handlerName = "Omniverse Event Handler";
    public ExportRequest Request = new ExportRequest();
    public void Execute(UIApplication app)
    {
        ExportEvent e = this.Request.Take();
        if (e == ExportEvent.None || e == ExportEvent.OpenModel || e == ExportEvent.CloseModel)
        {
            switch (e)
            {
                case ExportEvent.None:
                    return;
                case ExportEvent.OpenModel:
                    usd.exporter.revit.log.info("OpenModel");
                    if (app.ActiveUIDocument.Document.PathName == App.GetTestHarnessFileToOpen())
                    {
                        usd.exporter.revit.log.info("ReadyForPayload");
                        App.SetTestHarnessFileToClose(null);
                        App.SetTestHarnessFileToOpen(string.Empty);
                        App.ReadyForPayload();
                    }
                    else
                    {
                        usd.exporter.revit.log.info("Attempt to Open and Activate " + App.GetTestHarnessFileToOpen());
                        FilePath file = new FilePath(App.GetTestHarnessFileToOpen());
                        OpenOptions openOptions = new OpenOptions();
                        openOptions.Audit = false;
                        openOptions.AllowOpeningLocalByWrongUser = true;
                        openOptions.IgnoreExtensibleStorageSchemaConflict = true;

                        App.SetTestHarnessFileToClose(app.ActiveUIDocument.Document);
                        App.SetTestHarnessOpeningModel(true);
                        app.OpenAndActivateDocument(file, openOptions, false);
                        App.SetTestHarnessFileToOpen(string.Empty);
                        if (App.GetTestHarnessFileToClose() != null)
                        {
                            App.GetTestHarnessFileToClose().Close(false);
                            App.SetTestHarnessFileToClose(null);
                        }
                    }
                    break;
            }
            return;
        }
        Document doc = app.ActiveUIDocument.Document;
        string message = string.Empty;
        switch (e)
        {
            case ExportEvent.BatchExport:
                usd.exporter.revit.log.info("BatchExport");
                Exporter.ExportBatch(app, App.GetTestHarnessSettings());
                break;
        }
        usd.exporter.revit.log.info("TaskFinished");
        App.TaskFinished();
    }

    public string GetName()
    {
        return handlerName;
    }
}

public enum ExportEvent : int
{
    None,
    BatchExport,
    OpenModel,
    CloseModel
}

public class ExportRequest
{
    private int request = (int)ExportEvent.None;

    public ExportEvent Take()
    {
        return (ExportEvent)Interlocked.Exchange(ref this.request, (int)ExportEvent.None);
    }

    public void Make(ExportEvent e)
    {
        Interlocked.Exchange(ref this.request, (int)e);
    }
}
}

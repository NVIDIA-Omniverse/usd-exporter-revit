// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Xml.Linq;

namespace TestSetup
{
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
#if REVIT2024
    private int version = 2024;
#elif REVIT2025
    private int version = 2025;
#elif REVIT2026
    private int version = 2026;
#endif

    private string ovRevitPackage = "";

    private void App_Startup(object sender, StartupEventArgs e)
    {
        int exitCode = 0;

        // uninstall
        if (e.Args.Length > 0 && e.Args[0].ToLower().Contains("uninstall"))
        {
            RevitInstall result = uninstall();
            Console.WriteLine($"Revit {result.Version} : {result.Result} : {result.Message}");
            exitCode = Succeeded(result) ? 0 : 1;
        }
        // install
        else
        {
            // Best-effort remove of a prior .addin; failure here must not block install.
            RevitInstall cleanup = uninstall();
            Console.WriteLine($"Revit {cleanup.Version} : {cleanup.Result} : {cleanup.Message}");
            RevitInstall result = install();
            Console.WriteLine($"Revit {result.Version} : {result.Result} : {result.Message}");
            exitCode = Succeeded(result) ? 0 : 1;
        }

        Shutdown(exitCode);
    }

    private static bool Succeeded(RevitInstall result)
    {
        return result.Result == "Success" || result.Result == "Uninstalled";
    }

    private RevitInstall uninstall()
    {
        RevitInstall result = new RevitInstall(version);
        string addins = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $@"Autodesk\Revit\Addins\{version}");
        if (!Directory.Exists(addins))
        {
            // Idempotent: nothing to remove is success (fresh install / already cleaned).
            result.Result = "Uninstalled";
            result.Message = $"No add-ins folder at {addins}";
            return result;
        }

        DirectoryInfo dir = new DirectoryInfo(addins);
        bool removedAny = false;
        foreach (FileInfo file in dir.GetFiles("*.addin"))
        {
            if (!(file.Name.StartsWith("UsdExporterRevit")
                || file.Name.StartsWith("RevitUsdExport")
                || file.Name.StartsWith("RevitOmni")))
            {
                continue;
            }

            try
            {
                File.Delete(file.FullName);
                removedAny = true;
                result.Result = "Uninstalled";
                result.Message = $"Removed {file.FullName}";
            }
            catch (Exception ex)
            {
                result.Result = "Uninstall Failure";
                result.Message = file.FullName + " : " + ex.Message;
                return result;
            }
        }

        if (!removedAny)
        {
            result.Result = "Uninstalled";
            result.Message = "No matching UsdExporterRevit/RevitUsdExport/RevitOmni .addin to remove";
        }

        return result;
    }

    private RevitInstall install()
    {
        RevitInstall result = new RevitInstall(version);
        ovRevitPackage = getPackageLocation();
        if (!string.IsNullOrEmpty(ovRevitPackage))
        {
            string addinPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $@"Autodesk\Revit\Addins\{version}\UsdExporterRevit.addin");
            string addinResult = writeAddin(version, addinPath);
            if (addinResult == "success")
            {
                result.Result = "Success";
                result.Message = $"Created .addin for Revti {version} at {addinPath}";
            }
            else
            {
                result.Result = "Failed";
                result.Message = $"Unable to write .addin file for Revit {version} at {addinPath} : {addinResult}";
            }
        }
        else
        {
            result.Result = "Failed";
            result.Message = $"Unable to find package location for Revit {version} plugin.";
        }
        return result;
    }
    private string writeAddin(int version, string path)
    {
        try
        {
            XDocument doc = new XDocument();
            XElement revitAddins = new XElement("RevitAddIns");

            XElement addin = new XElement("AddIn");
            addin.Add(new XAttribute("Type", "Application"));

            XElement name = new XElement("Name");
            name.Value = "USD Exporter for Revit";
            XElement assembly = new XElement("Assembly");
            assembly.Value = Path.Combine(ovRevitPackage, $@"lib\UsdExporterRevit{version}.dll");
            XElement addinId = new XElement("AddInId");
            addinId.Value = "804b1052-f742-4951-8576-c261d19a3108";
            XElement fullClassName = new XElement("FullClassName");
            fullClassName.Value = "UsdExporterRevit.App";
            XElement vendorId = new XElement("VendorId");
            vendorId.Value = "NVIDIA Corporation";
            XElement vendorDescription = new XElement("VendorDescription");
            vendorDescription.Value = "NVIDIA Corporation";

            addin.Add(name);
            addin.Add(assembly);
            addin.Add(addinId);
            addin.Add(fullClassName);
            addin.Add(vendorId);
            addin.Add(vendorDescription);

            revitAddins.Add(addin);

            doc.Add(revitAddins);

            doc.Save(path);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return "success";
    }

    private string getPackageLocation()
    {
        FileInfo assembly = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string location = (assembly.Directory != null) ? assembly.Directory.FullName : string.Empty;
        return location;
    }
}

internal class RevitInstall
{
    public int Version = 0;
    public string Result = "";
    public string Message = "";

    public RevitInstall(int version)
    {
        Version = version;
    }
}
}

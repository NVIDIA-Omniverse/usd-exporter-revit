// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.IO;
using Microsoft.Win32;

namespace UsdExporterRevitBrowser
{
public class usd_export_browser
{
    public static string BrowseDlg(bool folderOnly, bool openFile, string title, string buttonLabel, string defaultUri, bool enableUriEdit)
    {
        if (folderOnly)
        {
            return showFolderDialog(title, defaultUri);
        }

        return openFile ? showOpenFileDialog(title, defaultUri) : showSaveFileDialog(title, defaultUri);
    }

    private static string showFolderDialog(string title, string defaultUri)
    {
#if NET8_0_OR_GREATER
        OpenFolderDialog dialog = new OpenFolderDialog {
            Title = title,
            InitialDirectory = getInitialDirectory(defaultUri),
        };

        return dialog.ShowDialog() == true ? normalizePath(dialog.FolderName) : string.Empty;
#else
        using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
        {
            dialog.Description = title;
            dialog.SelectedPath = getInitialDirectory(defaultUri);
            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? normalizePath(dialog.SelectedPath) : string.Empty;
        }
#endif
    }

    private static string showOpenFileDialog(string title, string defaultUri)
    {
        OpenFileDialog dialog = new OpenFileDialog {
            Title = title,
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = Path.GetFileName(defaultUri),
            InitialDirectory = getInitialDirectory(defaultUri),
        };

        return dialog.ShowDialog() == true ? normalizePath(dialog.FileName) : string.Empty;
    }

    private static string showSaveFileDialog(string title, string defaultUri)
    {
        SaveFileDialog dialog = new SaveFileDialog {
            Title = title, Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", FileName = Path.GetFileName(defaultUri), InitialDirectory = getInitialDirectory(defaultUri), DefaultExt = ".json", AddExtension = true, OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? normalizePath(dialog.FileName) : string.Empty;
    }

    private static string getInitialDirectory(string uri)
    {
        if (string.IsNullOrEmpty(uri) || uri.StartsWith("omniverse:/", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("omni:/", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        string localPath = uri.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        if (Directory.Exists(localPath))
        {
            return localPath;
        }

        string directory = Path.GetDirectoryName(localPath);
        return !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static string normalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/").Replace("%20", " ");
    }
}
}

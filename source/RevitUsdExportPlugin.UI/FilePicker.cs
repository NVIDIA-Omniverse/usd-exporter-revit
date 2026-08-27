// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using RevitUsdExportBrowser;

namespace RevitUsdExportPlugin
{
public static class FilePicker
{
    public static string GetFolderUri(string title, string buttonLabel, string defaultUri = "")
    {
        string returnFolder = RevitUsdExportBrowser.usd_export_browser.BrowseDlg(true, false, title, buttonLabel, defaultUri, false);
        if (!string.IsNullOrEmpty(returnFolder))
        {
            returnFolder = returnFolder.Replace("\\", "/");
            if (!returnFolder.EndsWith("/")) // NOSONAR
            {
                returnFolder += "/";
            }
        }
        return returnFolder.Replace("%20", " ");
    }

    public static string GetJsonUri(string title, string buttonLabel, string defaultUri = "")
    {
        string uri = RevitUsdExportBrowser.usd_export_browser.BrowseDlg(false, true, title, buttonLabel, defaultUri, false);
        return uri.Replace("%20", " ");
    }

    public static string SaveJsonUri(string title, string buttonLabel, string defaultUri = "")
    {
        string uri = RevitUsdExportBrowser.usd_export_browser.BrowseDlg(false, false, title, buttonLabel, defaultUri, true);
        return uri.Replace("%20", " ");
    }
}
}

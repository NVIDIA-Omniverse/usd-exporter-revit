// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.IO;

namespace RevitUsdExportSdk
{
public class TemporaryDirectory : System.IDisposable
{
    public string TempDirectoryPath { get; set; } = "";

    public TemporaryDirectory()
    {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), $"omniverse_revit_usd_export_{Guid.NewGuid()}");
        Directory.CreateDirectory(TempDirectoryPath);
    }

    ~TemporaryDirectory()
    {
        Destroy();
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }

    public void Destroy()
    {
        if (Directory.Exists(TempDirectoryPath))
        {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Create temporary directory.
    /// </summary>
    /// <param name="directoryName">directory name</param>
    /// <returns>Full path of temporary directory</returns>
    public string GetTemporaryDirectory(string directoryName)
    {
        string dirName = (string.IsNullOrEmpty(directoryName)) ? "temp" : directoryName;
        string newPath = $"{TempDirectoryPath}/{dirName}";
        if (!Directory.Exists(newPath))
        {
            Directory.CreateDirectory(newPath);
        }
        return newPath;
    }

    /// <summary>
    /// Delete temporary directory.
    /// </summary>
    /// <param name="directoryName">directory name</param>
    public void DeleteTemporaryDirectory(string directoryName)
    {
        string tempDirectory = GetTemporaryDirectory(directoryName);
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, true);
        }
    }
}
}

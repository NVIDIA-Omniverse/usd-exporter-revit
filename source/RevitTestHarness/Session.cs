// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RevitUsdExport.Settings;

namespace RevitTestHarness.Session
{
[Serializable]
public class SessionData
{
    public string Session;
    public string Version;
    public List<TestData> Tests;
    public double TotalExportTime;
    public double Duration;

    [JsonConstructor]
    public SessionData()
    {
    }

    public SessionData(string session, string version)
    {
        Session = session;
        Version = version;
        Tests = new List<TestData>();
        TotalExportTime = 0;
    }
}
[Serializable]
public class TestData
{
    public string Test;
    public string Model;
    public double Time;
    public int ExitCode;
    public double Size;
    public int UsdaCount;

    public bool Cameras;
    public bool Lights;
    public bool Vegetation;
    public bool Rooms;
    public bool Spaces;
    public bool Drawings;
    public bool Instancing;
    public bool Autodesk;
    public bool Mapping;
    public bool BIM;
    public bool Links;

    [Newtonsoft.Json.JsonConstructor]
    public TestData()
    {
    }

    public TestData(string test, string model, RevitUsdExportSettings settings)
    {
        Test = test;
        Model = model;
        Cameras = settings.Options.IncludeCameras;
        Lights = settings.Options.IncludeLights;
        Rooms = settings.Options.IncludeRooms;
        Spaces = settings.Options.IncludeSpaces;
        Drawings = settings.Options.IncludeDrawings;
        Instancing = settings.Options.InstanceFamilies;
        Autodesk = true;
        Mapping = !Autodesk;
        BIM = settings.Options.IncludeBimData;
        Links = settings.Options.IncludeLinks;
    }
    private class SizeCount
    {
        public double Size;
        public int Count;
        public SizeCount()
        {
            Size = 0.0;
            Count = 0;
        }
    }

    public void SetSizeAndCount(string path)
    {
        if (Directory.Exists(path))
        {
            DirectoryInfo dir = new DirectoryInfo(path);

            SizeCount sizeCount = new SizeCount();
            sizeCount = GetSizeAndCount(dir, sizeCount);
            Size = sizeCount.Size;
            UsdaCount = sizeCount.Count;
        }
    }
    private SizeCount GetSizeAndCount(DirectoryInfo dir, SizeCount sizeCount)
    {
        foreach (DirectoryInfo d in dir.GetDirectories())
        {
            sizeCount = GetSizeAndCount(d, sizeCount);
        }
        foreach (FileInfo f in dir.GetFiles())
        {
            if (f.Name.EndsWith(".usda"))
            {
                sizeCount.Count++;
                sizeCount.Size += f.Length / 1000000.0; // MB
            }
        }
        return sizeCount;
    }
}
}

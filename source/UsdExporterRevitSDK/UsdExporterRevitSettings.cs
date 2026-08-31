// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Drawing;

#if REV2026 || REV2025 || REV2024
using Autodesk.Revit.DB;

namespace UsdExporterRevitSdk
#else
namespace UsdExporterRevit.Settings
#endif
{
[Serializable]
public class UsdExporterRevitSettings
{
    private List<string> fileNameParts = new List<string>();
#region File
    // output folder
    // file name
    // extension
    // app path
    public SingleFileExport File = new SingleFileExport();
#endregion

#region Views to Export
    public List<string> ViewsToExport = new List<string>();
#endregion

#region View Adjustments
    // detail level
    // phase filter
    // view template
    public ViewModifications View = new ViewModifications();
#endregion

#region Options
    // cameras
    // lights
    // rooms
    // spaces
    // drawings
    // family instances
    // bim data
    // links
    // coordinate system
    // sky
    public IncludeOptions Options = new IncludeOptions();
#endregion

#region Mapping
    // materials
    // vegetation
    // family types
    public AssetMappings Mappings = new AssetMappings();
#endregion

#region Batch
    // input folder
    // output folder
    // depth
    // file name pattern
    // view name
    public BatchExport Batch = new BatchExport();
#endregion

    public string OverrideJsonPath = string.Empty;

    [Newtonsoft.Json.JsonConstructor]
    public UsdExporterRevitSettings()
    {
    }

    public UsdExporterRevitSettings(string fileName)
    {
        File.FileName = fileName;
    }

    public bool Write(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

#if REV2026 || REV2025 || REV2024
        string settingsString = JsonConvert.SerializeObject(this, Formatting.Indented);
        if (!usd.exporter.revit.file.client.isLocalUri(filePath))
        {
            usd.exporter.revit.log.warning($"Writing settings to non-local paths is not supported: \"{filePath}\"");
            return false;
        }
        else
        {
            System.IO.File.WriteAllText(filePath, settingsString);
        }
#else
        string settingsString = JsonConvert.SerializeObject(this, Formatting.Indented);
        System.IO.File.WriteAllText(filePath, settingsString);
#endif
        return true;
    }

    public static UsdExporterRevitSettings Read(JObject settings, string filePath)
    {
        UsdExporterRevitSettings output = new UsdExporterRevitSettings();
        if (settings != null)
        {
            if (settings.ContainsKey("standard_export_settings")) // old json format
            {
                UsdExporterRevitSettings updated = updateFromOld(settings);
                if (string.IsNullOrEmpty(filePath))
                {
                    return updated;
                }
                else
                {
                    updated.Write(filePath);
                    return Read(filePath);
                }
            }
            if (settings.ContainsKey(nameof(output.File)))
            {
                JObject fileObj = settings.SelectToken(nameof(output.File)) as JObject;
                JToken token = null;
                if (fileObj.ContainsKey(nameof(output.File.FileName)))
                {
                    token = fileObj.SelectToken(nameof(output.File.FileName));
                    if (token != null)
                    {
                        output.File.FileName = (string)token;
                    }
                }
                if (fileObj.ContainsKey(nameof(output.File.Extension)))
                {
                    token = fileObj.SelectToken(nameof(output.File.Extension));
                    if (token != null)
                    {
                        output.File.Extension = (string)token;
                    }
                }
                if (fileObj.ContainsKey(nameof(output.File.OutputFolder)))
                {
                    token = fileObj.SelectToken(nameof(output.File.OutputFolder));
                    if (token != null)
                    {
                        output.File.OutputFolder = (string)token;
                    }
                }
            }
            if (settings.ContainsKey(nameof(output.ViewsToExport)))
            {
                JArray views = settings.SelectToken(nameof(output.ViewsToExport)) as JArray;
                if (views != null)
                {
                    foreach (var view in views)
                    {
                        output.ViewsToExport.Add((string)view);
                    }
                }
            }
            if (settings.ContainsKey(nameof(output.View)))
            {
                JObject viewObj = settings.SelectToken(nameof(output.View)) as JObject;
                if (viewObj != null)
                {
                    JToken token = null;
                    if (viewObj.ContainsKey(nameof(output.View.DetailLevel)))
                    {
                        token = viewObj.SelectToken(nameof(output.View.DetailLevel));
                        if (token != null)
                        {
                            output.View.DetailLevel = (string)token;
                        }
                    }
                    if (viewObj.ContainsKey(nameof(output.View.PhaseFilter)))
                    {
                        token = viewObj.SelectToken(nameof(output.View.PhaseFilter));
                        if (token != null)
                        {
                            output.View.PhaseFilter = (string)token;
                        }
                    }
                    if (viewObj.ContainsKey(nameof(output.View.ViewTemplate)))
                    {
                        token = viewObj.SelectToken(nameof(output.View.ViewTemplate));
                        if (token != null)
                        {
                            output.View.ViewTemplate = (string)token;
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(output.Options)))
            {
                JObject optionsObj = settings.SelectToken(nameof(output.Options)) as JObject;
                if (optionsObj != null)
                {
                    JToken token = null;
                    if (optionsObj.ContainsKey(nameof(output.Options.CoordinateSystem)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.CoordinateSystem));
                        if (token != null)
                        {
                            output.Options.CoordinateSystem = (int)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.DrawingPublishSet)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.DrawingPublishSet));
                        if (token != null)
                        {
                            output.Options.DrawingPublishSet = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeBimData)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeBimData));
                        if (token != null)
                        {
                            output.Options.IncludeBimData = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeCameras)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeCameras));
                        if (token != null)
                        {
                            output.Options.IncludeCameras = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeDrawings)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeDrawings));
                        if (token != null)
                        {
                            output.Options.IncludeDrawings = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeLights)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeLights));
                        if (token != null)
                        {
                            output.Options.IncludeLights = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeLinks)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeLinks));
                        if (token != null)
                        {
                            output.Options.IncludeLinks = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.InstanceFamilies)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.InstanceFamilies));
                        if (token != null)
                        {
                            output.Options.InstanceFamilies = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.FamilyInstanceStyle)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.FamilyInstanceStyle));
                        if (token != null)
                        {
                            output.Options.FamilyInstanceStyle = (FamilyInstancingStyle)(int)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeRooms)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeRooms));
                        if (token != null)
                        {
                            output.Options.IncludeRooms = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.RoomColorScheme)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.RoomColorScheme));
                        if (token != null)
                        {
                            output.Options.RoomColorScheme = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.IncludeSpaces)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.IncludeSpaces));
                        if (token != null)
                        {
                            output.Options.IncludeSpaces = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.SpaceColorScheme)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.SpaceColorScheme));
                        if (token != null)
                        {
                            output.Options.SpaceColorScheme = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.MaterialFolderName)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.MaterialFolderName));
                        if (token != null)
                        {
                            output.Options.MaterialFolderName = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.MaterialStyle)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.MaterialStyle));
                        if (token != null)
                        {
                            output.Options.MaterialStyle = (MaterialStyle)(int)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(output.Options.UnitType)))
                    {
                        token = optionsObj.SelectToken(nameof(output.Options.UnitType));
                        if (token != null)
                        {
                            output.Options.UnitType = (UnitType)(int)token;
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(output.Mappings)))
            {
                JObject mappingObj = settings.SelectToken(nameof(output.Mappings)) as JObject;
                if (mappingObj != null)
                {
                    if (mappingObj.ContainsKey(nameof(output.Mappings.Libraries)))
                    {
                        JObject libs = mappingObj.SelectToken(nameof(output.Mappings.Libraries)) as JObject;
                        if (libs != null)
                        {
                            JToken token = null;
                            if (libs.ContainsKey(nameof(output.Mappings.Libraries.MaterialFolders)))
                            {
                                token = libs.SelectToken(nameof(output.Mappings.Libraries.MaterialFolders));
                                if (token != null)
                                {
                                    output.Mappings.Libraries.MaterialFolders = ((JArray)token).ToObject<List<string>>();
                                }
                            }
                            if (libs.ContainsKey(nameof(output.Mappings.Libraries.AssetFolders)))
                            {
                                token = libs.SelectToken(nameof(output.Mappings.Libraries.AssetFolders));
                                if (token != null)
                                {
                                    output.Mappings.Libraries.AssetFolders = ((JArray)token).ToObject<List<string>>();
                                }
                            }
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(output.Mappings.Materials)))
                    {
                        JObject mats = mappingObj.SelectToken(nameof(output.Mappings.Materials)) as JObject;
                        if (mats != null)
                        {
                            JToken token = null;
                            if (mats.ContainsKey(nameof(output.Mappings.Materials.DefaultLibraryUri)))
                            {
                                token = mats.SelectToken(nameof(output.Mappings.Materials.DefaultLibraryUri));
                                if (token != null)
                                {
                                    output.Mappings.Materials.DefaultLibraryUri = (string)token;
                                }
                            }
                            if (mats.ContainsKey(nameof(output.Mappings.Materials.UserMapped)))
                            {
                                token = mats.SelectToken(nameof(output.Mappings.Materials.UserMapped));
                                if (token != null)
                                {
                                    output.Mappings.Materials.UserMapped = ((JArray)token).ToObject<List<UserMaterialMapping>>();
                                }
                            }
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(output.Mappings.FamilyTypes)))
                    {
                        JObject ft = mappingObj.SelectToken(nameof(output.Mappings.FamilyTypes)) as JObject;
                        if (ft != null)
                        {
                            if (ft.ContainsKey(nameof(output.Mappings.FamilyTypes.DefaultLibraryUri)))
                            {
                                JToken token = ft.SelectToken(nameof(output.Mappings.FamilyTypes.DefaultLibraryUri));
                                if (token != null)
                                {
                                    output.Mappings.FamilyTypes.DefaultLibraryUri = (string)token;
                                }
                            }
                            if (ft.ContainsKey(nameof(output.Mappings.FamilyTypes.UserMapped)))
                            {
                                JToken token = ft.SelectToken(nameof(output.Mappings.FamilyTypes.UserMapped));
                                if (token != null)
                                {
                                    output.Mappings.FamilyTypes.UserMapped = ((JArray)token).ToObject<List<UserFamilyTypeMapping>>();
                                }
                            }
                        }
                    }
                    // Read UI state properties
                    JToken uiToken = null;
                    if (mappingObj.ContainsKey(nameof(output.Mappings.IsMaterialsMode)))
                    {
                        uiToken = mappingObj.SelectToken(nameof(output.Mappings.IsMaterialsMode));
                        if (uiToken != null)
                        {
                            output.Mappings.IsMaterialsMode = (bool)uiToken;
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(output.Mappings.MaterialsUIState)))
                    {
                        JObject matUIObj = mappingObj.SelectToken(nameof(output.Mappings.MaterialsUIState)) as JObject;
                        if (matUIObj != null)
                        {
                            if (matUIObj.ContainsKey(nameof(output.Mappings.MaterialsUIState.SelectedLibrary)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(output.Mappings.MaterialsUIState.SelectedLibrary));
                                if (uiToken != null)
                                    output.Mappings.MaterialsUIState.SelectedLibrary = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(output.Mappings.MaterialsUIState.SelectedGroup)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(output.Mappings.MaterialsUIState.SelectedGroup));
                                if (uiToken != null)
                                    output.Mappings.MaterialsUIState.SelectedGroup = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(output.Mappings.MaterialsUIState.SelectedFile)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(output.Mappings.MaterialsUIState.SelectedFile));
                                if (uiToken != null)
                                    output.Mappings.MaterialsUIState.SelectedFile = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(output.Mappings.MaterialsUIState.SelectedModule)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(output.Mappings.MaterialsUIState.SelectedModule));
                                if (uiToken != null)
                                    output.Mappings.MaterialsUIState.SelectedModule = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(output.Mappings.MaterialsUIState.SelectedCategory)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(output.Mappings.MaterialsUIState.SelectedCategory));
                                if (uiToken != null)
                                    output.Mappings.MaterialsUIState.SelectedCategory = (string)uiToken;
                            }
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(output.Mappings.FamiliesUIState)))
                    {
                        JObject famUIObj = mappingObj.SelectToken(nameof(output.Mappings.FamiliesUIState)) as JObject;
                        if (famUIObj != null)
                        {
                            if (famUIObj.ContainsKey(nameof(output.Mappings.FamiliesUIState.SelectedLibrary)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(output.Mappings.FamiliesUIState.SelectedLibrary));
                                if (uiToken != null)
                                    output.Mappings.FamiliesUIState.SelectedLibrary = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(output.Mappings.FamiliesUIState.SelectedGroup)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(output.Mappings.FamiliesUIState.SelectedGroup));
                                if (uiToken != null)
                                    output.Mappings.FamiliesUIState.SelectedGroup = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(output.Mappings.FamiliesUIState.SelectedFile)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(output.Mappings.FamiliesUIState.SelectedFile));
                                if (uiToken != null)
                                    output.Mappings.FamiliesUIState.SelectedFile = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(output.Mappings.FamiliesUIState.SelectedModule)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(output.Mappings.FamiliesUIState.SelectedModule));
                                if (uiToken != null)
                                    output.Mappings.FamiliesUIState.SelectedModule = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(output.Mappings.FamiliesUIState.SelectedCategory)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(output.Mappings.FamiliesUIState.SelectedCategory));
                                if (uiToken != null)
                                    output.Mappings.FamiliesUIState.SelectedCategory = (string)uiToken;
                            }
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(output.Batch)))
            {
                JObject batchObj = settings.SelectToken(nameof(output.Batch)) as JObject;
                if (batchObj != null)
                {
                    JToken token = null;
                    if (batchObj.ContainsKey(nameof(output.Batch.InputFolder)))
                    {
                        token = batchObj.SelectToken(nameof(output.Batch.InputFolder));
                        if (token != null)
                        {
                            output.Batch.InputFolder = (string)token;
                        }
                    }
                    if (batchObj.ContainsKey(nameof(output.Batch.OutputFolder)))
                    {
                        token = batchObj.SelectToken(nameof(output.Batch.OutputFolder));
                        if (token != null)
                        {
                            output.Batch.OutputFolder = (string)token;
                        }
                    }
                    if (batchObj.ContainsKey(nameof(output.Batch.ViewToFind)))
                    {
                        token = batchObj.SelectToken(nameof(output.Batch.ViewToFind));
                        if (token != null)
                        {
                            output.Batch.ViewToFind = (string)token;
                        }
                    }
                    if (batchObj.ContainsKey(nameof(output.Batch.FolderDepth)))
                    {
                        token = batchObj.SelectToken(nameof(output.Batch.FolderDepth));
                        if (token != null)
                        {
                            output.Batch.FolderDepth = (int)token;
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(output.OverrideJsonPath)))
            {
                JToken overPath = settings.SelectToken(nameof(output.OverrideJsonPath));
                if (overPath != null)
                {
                    output.OverrideJsonPath = (string)overPath;
                }
            }
        }
        return output;
    }

    public static UsdExporterRevitSettings Read(string filePath)
    {
        UsdExporterRevitSettings output = new UsdExporterRevitSettings();
        if (System.IO.File.Exists(filePath))
        {
            string settingsString = System.IO.File.ReadAllText(filePath);
            JObject settings = JObject.Parse(settingsString);
            return Read(settings, filePath);
        }
        return output;
    }

    public void OverrideWithJson(string jsonPath)
    {
        if (System.IO.File.Exists(jsonPath))
        {
            string settingsString = System.IO.File.ReadAllText(jsonPath);
            JObject settings = JObject.Parse(settingsString);
            if (settings.ContainsKey("standard_export_settings")) // old settings format
            {
                UsdExporterRevitSettings updated = updateFromOld(settings);
                updated.Write(jsonPath);
                this.OverrideWithJson(jsonPath);
            }
            if (settings.ContainsKey(nameof(this.File)))
            {
                JObject fileObj = settings.SelectToken(nameof(this.File)) as JObject;
                JToken token = null;
                if (fileObj.ContainsKey(nameof(this.File.FileName)))
                {
                    token = fileObj.SelectToken(nameof(this.File.FileName));
                    if (token != null)
                    {
                        this.File.FileName = (string)token;
                    }
                }
                if (fileObj.ContainsKey(nameof(this.File.Extension)))
                {
                    token = fileObj.SelectToken(nameof(this.File.Extension));
                    if (token != null)
                    {
                        this.File.Extension = (string)token;
                    }
                }
                if (fileObj.ContainsKey(nameof(this.File.OutputFolder)))
                {
                    token = fileObj.SelectToken(nameof(this.File.OutputFolder));
                    if (token != null)
                    {
                        this.File.OutputFolder = (string)token;
                    }
                }
            }
            if (settings.ContainsKey(nameof(this.ViewsToExport)))
            {
                JArray views = settings.SelectToken(nameof(this.ViewsToExport)) as JArray;
                if (views != null)
                {
                    foreach (var view in views)
                    {
                        if (!this.ViewsToExport.Contains((string)view))
                        {
                            this.ViewsToExport.Add((string)view);
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(this.View)))
            {
                JObject viewObj = settings.SelectToken(nameof(this.View)) as JObject;
                if (viewObj != null)
                {
                    JToken token = null;
                    if (viewObj.ContainsKey(nameof(this.View.DetailLevel)))
                    {
                        token = viewObj.SelectToken(nameof(this.View.DetailLevel));
                        if (token != null)
                        {
                            this.View.DetailLevel = (string)token;
                        }
                    }
                    if (viewObj.ContainsKey(nameof(this.View.PhaseFilter)))
                    {
                        token = viewObj.SelectToken(nameof(this.View.PhaseFilter));
                        if (token != null)
                        {
                            this.View.PhaseFilter = (string)token;
                        }
                    }
                    if (viewObj.ContainsKey(nameof(this.View.ViewTemplate)))
                    {
                        token = viewObj.SelectToken(nameof(this.View.ViewTemplate));
                        if (token != null)
                        {
                            this.View.ViewTemplate = (string)token;
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(this.Options)))
            {
                JObject optionsObj = settings.SelectToken(nameof(this.Options)) as JObject;
                if (optionsObj != null)
                {
                    JToken token = null;
                    if (optionsObj.ContainsKey(nameof(this.Options.CoordinateSystem)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.CoordinateSystem));
                        if (token != null)
                        {
                            this.Options.CoordinateSystem = (int)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.DrawingPublishSet)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.DrawingPublishSet));
                        if (token != null)
                        {
                            this.Options.DrawingPublishSet = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeBimData)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeBimData));
                        if (token != null)
                        {
                            this.Options.IncludeBimData = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeCameras)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeCameras));
                        if (token != null)
                        {
                            this.Options.IncludeCameras = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeDrawings)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeDrawings));
                        if (token != null)
                        {
                            this.Options.IncludeDrawings = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeLights)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeLights));
                        if (token != null)
                        {
                            this.Options.IncludeLights = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeLinks)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeLinks));
                        if (token != null)
                        {
                            this.Options.IncludeLinks = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.InstanceFamilies)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.InstanceFamilies));
                        if (token != null)
                        {
                            this.Options.InstanceFamilies = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.FamilyInstanceStyle)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.FamilyInstanceStyle));
                        if (token != null)
                        {
                            this.Options.FamilyInstanceStyle = (FamilyInstancingStyle)(int)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeRooms)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeRooms));
                        if (token != null)
                        {
                            this.Options.IncludeRooms = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.RoomColorScheme)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.RoomColorScheme));
                        if (token != null)
                        {
                            this.Options.RoomColorScheme = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.IncludeSpaces)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.IncludeSpaces));
                        if (token != null)
                        {
                            this.Options.IncludeSpaces = (bool)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.SpaceColorScheme)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.SpaceColorScheme));
                        if (token != null)
                        {
                            this.Options.SpaceColorScheme = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.MaterialFolderName)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.MaterialFolderName));
                        if (token != null)
                        {
                            this.Options.MaterialFolderName = (string)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.MaterialStyle)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.MaterialStyle));
                        if (token != null)
                        {
                            this.Options.MaterialStyle = (MaterialStyle)(int)token;
                        }
                    }
                    if (optionsObj.ContainsKey(nameof(this.Options.UnitType)))
                    {
                        token = optionsObj.SelectToken(nameof(this.Options.UnitType));
                        if (token != null)
                        {
                            this.Options.UnitType = (UnitType)(int)token;
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(this.Mappings)))
            {
                JObject mappingObj = settings.SelectToken(nameof(this.Mappings)) as JObject;
                if (mappingObj != null)
                {
                    if (mappingObj.ContainsKey(nameof(this.Mappings.Libraries)))
                    {
                        JObject libs = mappingObj.SelectToken(nameof(this.Mappings.Libraries)) as JObject;
                        if (libs != null)
                        {
                            JToken token = null;
                            if (libs.ContainsKey(nameof(this.Mappings.Libraries.MaterialFolders)))
                            {
                                token = libs.SelectToken(nameof(this.Mappings.Libraries.MaterialFolders));
                                if (token != null)
                                {
                                    this.Mappings.Libraries.MaterialFolders = ((JArray)token).ToObject<List<string>>();
                                }
                            }
                            if (libs.ContainsKey(nameof(this.Mappings.Libraries.AssetFolders)))
                            {
                                token = libs.SelectToken(nameof(this.Mappings.Libraries.AssetFolders));
                                if (token != null)
                                {
                                    this.Mappings.Libraries.AssetFolders = ((JArray)token).ToObject<List<string>>();
                                }
                            }
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(this.Mappings.Materials)))
                    {
                        JObject mats = mappingObj.SelectToken(nameof(this.Mappings.Materials)) as JObject;
                        if (mats != null)
                        {
                            JToken token = null;
                            if (mats.ContainsKey(nameof(this.Mappings.Materials.DefaultLibraryUri)))
                            {
                                token = mats.SelectToken(nameof(this.Mappings.Materials.DefaultLibraryUri));
                                if (token != null)
                                {
                                    this.Mappings.Materials.DefaultLibraryUri = (string)token;
                                }
                            }
                            if (mats.ContainsKey(nameof(this.Mappings.Materials.UserMapped)))
                            {
                                token = mats.SelectToken(nameof(this.Mappings.Materials.UserMapped));
                                if (token != null)
                                {
                                    this.Mappings.Materials.UserMapped = ((JArray)token).ToObject<List<UserMaterialMapping>>();
                                }
                            }
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(this.Mappings.FamilyTypes)))
                    {
                        JObject ft = mappingObj.SelectToken(nameof(this.Mappings.FamilyTypes)) as JObject;
                        if (ft != null)
                        {
                            if (ft.ContainsKey(nameof(this.Mappings.FamilyTypes.DefaultLibraryUri)))
                            {
                                JToken token = ft.SelectToken(nameof(this.Mappings.FamilyTypes.DefaultLibraryUri));
                                if (token != null)
                                {
                                    this.Mappings.FamilyTypes.DefaultLibraryUri = (string)token;
                                }
                            }
                            if (ft.ContainsKey(nameof(this.Mappings.FamilyTypes.UserMapped)))
                            {
                                JToken token = ft.SelectToken(nameof(this.Mappings.FamilyTypes.UserMapped));
                                if (token != null)
                                {
                                    this.Mappings.FamilyTypes.UserMapped = ((JArray)token).ToObject<List<UserFamilyTypeMapping>>();
                                }
                            }
                        }
                    }
                    // Read UI state properties
                    JToken uiToken = null;
                    if (mappingObj.ContainsKey(nameof(this.Mappings.IsMaterialsMode)))
                    {
                        uiToken = mappingObj.SelectToken(nameof(this.Mappings.IsMaterialsMode));
                        if (uiToken != null)
                        {
                            this.Mappings.IsMaterialsMode = (bool)uiToken;
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(this.Mappings.MaterialsUIState)))
                    {
                        JObject matUIObj = mappingObj.SelectToken(nameof(this.Mappings.MaterialsUIState)) as JObject;
                        if (matUIObj != null)
                        {
                            if (matUIObj.ContainsKey(nameof(this.Mappings.MaterialsUIState.SelectedLibrary)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(this.Mappings.MaterialsUIState.SelectedLibrary));
                                if (uiToken != null)
                                    this.Mappings.MaterialsUIState.SelectedLibrary = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(this.Mappings.MaterialsUIState.SelectedGroup)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(this.Mappings.MaterialsUIState.SelectedGroup));
                                if (uiToken != null)
                                    this.Mappings.MaterialsUIState.SelectedGroup = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(this.Mappings.MaterialsUIState.SelectedFile)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(this.Mappings.MaterialsUIState.SelectedFile));
                                if (uiToken != null)
                                    this.Mappings.MaterialsUIState.SelectedFile = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(this.Mappings.MaterialsUIState.SelectedModule)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(this.Mappings.MaterialsUIState.SelectedModule));
                                if (uiToken != null)
                                    this.Mappings.MaterialsUIState.SelectedModule = (string)uiToken;
                            }
                            if (matUIObj.ContainsKey(nameof(this.Mappings.MaterialsUIState.SelectedCategory)))
                            {
                                uiToken = matUIObj.SelectToken(nameof(this.Mappings.MaterialsUIState.SelectedCategory));
                                if (uiToken != null)
                                    this.Mappings.MaterialsUIState.SelectedCategory = (string)uiToken;
                            }
                        }
                    }
                    if (mappingObj.ContainsKey(nameof(this.Mappings.FamiliesUIState)))
                    {
                        JObject famUIObj = mappingObj.SelectToken(nameof(this.Mappings.FamiliesUIState)) as JObject;
                        if (famUIObj != null)
                        {
                            if (famUIObj.ContainsKey(nameof(this.Mappings.FamiliesUIState.SelectedLibrary)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(this.Mappings.FamiliesUIState.SelectedLibrary));
                                if (uiToken != null)
                                    this.Mappings.FamiliesUIState.SelectedLibrary = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(this.Mappings.FamiliesUIState.SelectedGroup)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(this.Mappings.FamiliesUIState.SelectedGroup));
                                if (uiToken != null)
                                    this.Mappings.FamiliesUIState.SelectedGroup = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(this.Mappings.FamiliesUIState.SelectedFile)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(this.Mappings.FamiliesUIState.SelectedFile));
                                if (uiToken != null)
                                    this.Mappings.FamiliesUIState.SelectedFile = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(this.Mappings.FamiliesUIState.SelectedModule)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(this.Mappings.FamiliesUIState.SelectedModule));
                                if (uiToken != null)
                                    this.Mappings.FamiliesUIState.SelectedModule = (string)uiToken;
                            }
                            if (famUIObj.ContainsKey(nameof(this.Mappings.FamiliesUIState.SelectedCategory)))
                            {
                                uiToken = famUIObj.SelectToken(nameof(this.Mappings.FamiliesUIState.SelectedCategory));
                                if (uiToken != null)
                                    this.Mappings.FamiliesUIState.SelectedCategory = (string)uiToken;
                            }
                        }
                    }
                }
            }
            if (settings.ContainsKey(nameof(this.Batch)))
            {
                JObject batchObj = settings.SelectToken(nameof(this.Batch)) as JObject;
                if (batchObj != null)
                {
                    JToken token = null;
                    if (batchObj.ContainsKey(nameof(this.Batch.InputFolder)))
                    {
                        token = batchObj.SelectToken(nameof(this.Batch.InputFolder));
                        if (token != null)
                        {
                            this.Batch.InputFolder = (string)token;
                        }
                    }
                    if (batchObj.ContainsKey(nameof(this.Batch.OutputFolder)))
                    {
                        token = batchObj.SelectToken(nameof(this.Batch.OutputFolder));
                        if (token != null)
                        {
                            this.Batch.OutputFolder = (string)token;
                        }
                    }
                    if (batchObj.ContainsKey(nameof(this.Batch.ViewToFind)))
                    {
                        token = batchObj.SelectToken(nameof(this.Batch.ViewToFind));
                        if (token != null)
                        {
                            this.Batch.ViewToFind = (string)token;
                        }
                    }
                    if (batchObj.ContainsKey(nameof(this.Batch.FolderDepth)))
                    {
                        token = batchObj.SelectToken(nameof(this.Batch.FolderDepth));
                        if (token != null)
                        {
                            this.Batch.FolderDepth = (int)token;
                        }
                    }
                }
            }
        }
    }

    private static UsdExporterRevitSettings updateFromOld(JObject old)
    {
        UsdExporterRevitSettings current = new UsdExporterRevitSettings();
        JToken token = null;
        if (old.ContainsKey("standard_export_settings"))
        {
            token = old.SelectToken("standard_export_settings.include_cameras.value");
            if (token != null)
            {
                current.Options.IncludeCameras = (bool)token;
            }
            token = old.SelectToken("standard_export_settings.include_lights.value");
            if (token != null)
            {
                current.Options.IncludeLights = (bool)token;
            }
            token = old.SelectToken("standard_export_settings.include_rooms.value");
            if (token != null)
            {
                current.Options.IncludeRooms = (bool)token;
            }
            token = old.SelectToken("standard_export_settings.include_spaces.value");
            if (token != null)
            {
                current.Options.IncludeSpaces = (bool)token;
            }
            token = old.SelectToken("standard_export_settings.room_color_scheme.value");
            if (token != null)
            {
                current.Options.RoomColorScheme = (string)token;
            }
            token = old.SelectToken("standard_export_settings.space_color_scheme.value");
            if (token != null)
            {
                current.Options.SpaceColorScheme = (string)token;
            }
            token = old.SelectToken("standard_export_settings.include_drawings.value");
            if (token != null)
            {
                current.Options.IncludeDrawings = (bool)token;
            }
            token = old.SelectToken("standard_export_settings.publish_set_name.value");
            if (token != null)
            {
                current.Options.DrawingPublishSet = (string)token;
            }
            token = old.SelectToken("standard_export_settings.enable_family_data_instancing.value");
            if (token != null)
            {
                current.Options.InstanceFamilies = (bool)token;
                current.Options.FamilyInstanceStyle = FamilyInstancingStyle.InternalClasses;
            }
            token = old.SelectToken("standard_export_settings.include_bim_data.value");
            if (token != null)
            {
                current.Options.IncludeBimData = (bool)token;
            }
        }
        if (old.ContainsKey("view_export_settings"))
        {
            token = old.SelectToken("view_export_settings.text_to_find.value");
            if (token != null)
            {
                current.Batch.ViewToFind = (string)token;
            }
            token = old.SelectToken("view_export_settings.detail_level.value");
            if (token != null)
            {
                current.View.DetailLevel = (string)token;
            }
            token = old.SelectToken("view_export_settings.phase_filter.value");
            if (token != null)
            {
                current.View.PhaseFilter = (string)token;
            }
            token = old.SelectToken("view_export_settings.view_template.value");
            if (token != null)
            {
                current.View.ViewTemplate = (string)token;
            }
            token = old.SelectToken("view_export_settings.include_links.value");
            if (token != null)
            {
                current.Options.IncludeLinks = (bool)token;
            }
        }
        if (old.ContainsKey("batch_export_settings"))
        {
            token = old.SelectToken("batch_export_settings.input_folder.value");
            if (token != null)
            {
                current.Batch.InputFolder = (string)token;
            }
            token = old.SelectToken("batch_export_settings.output_folder.value");
            if (token != null)
            {
                current.Batch.OutputFolder = (string)token;
            }
            token = old.SelectToken("batch_export_settings.coordinate_system_type.value");
            if (token != null)
            {
                current.Options.CoordinateSystem = (int)token;
            }
            token = old.SelectToken("batch_export_settings.folder_depth.value");
            if (token != null)
            {
                current.Batch.FolderDepth = (int)token;
            }
        }
        return current;
    }
    public bool IsInputFolderValid()
    {
        if (Directory.Exists(Batch.InputFolder))
        {
            return true;
        }
        return false;
    }
#if REV2026 || REV2025 || REV2024
    public bool IsBatchOutputFolderValid()
    {
        string value = Batch.OutputFolder;
        if (Directory.Exists(value) || usd.exporter.revit.file.client.isLocalUri(value))
        {
            return true;
        }
        return false;
    }

    public bool IsOutputFolderValid()
    {
        string value = File.OutputFolder;
        if (Directory.Exists(value) || usd.exporter.revit.file.client.isLocalUri(value))
        {
            return true;
        }
        return false;
    }
#endif

    public bool AnyViewManipulations()
    {
        if (string.IsNullOrEmpty(this.View.DetailLevel) && string.IsNullOrEmpty(this.View.ViewTemplate) && string.IsNullOrEmpty(this.View.PhaseFilter))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public string GetStringMatch(UsdExporterRevitSettingType setting, List<string> names)
    {
        string output = string.Empty;
        List<string> matches = GetStringMatches(setting, names);
        if (matches.Count > 0)
        {
            output = matches[0];
        }
        return output;
    }
    public List<string> GetStringMatches(UsdExporterRevitSettingType setting, List<string> names)
    {
        List<string> matches = new List<string>();
        string testValue = string.Empty;
        switch (setting)
        {
            case UsdExporterRevitSettingType.DetailLevel:
                testValue = this.View.DetailLevel;
                break;
            case UsdExporterRevitSettingType.PhaseFilter:
                testValue = this.View.PhaseFilter;
                break;
            case UsdExporterRevitSettingType.ViewToFind:
                testValue = this.Batch.ViewToFind;
                break;
            case UsdExporterRevitSettingType.ViewTemplate:
                testValue = this.View.ViewTemplate;
                break;
            case UsdExporterRevitSettingType.PublishSet:
                testValue = this.Options.DrawingPublishSet;
                break;
            case UsdExporterRevitSettingType.RoomColorScheme:
                testValue = this.Options.RoomColorScheme;
                break;
            case UsdExporterRevitSettingType.SpaceColorScheme:
                testValue = this.Options.SpaceColorScheme;
                break;
        }
        if (testValue != string.Empty)
        {
            matches = names.Where(x => x.Equals(testValue, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0)
            {
                return matches;
            }
            matches = names.Where(x => x.StartsWith(testValue, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0)
            {
                return matches;
            }
            matches = names.Where(x => x.EndsWith(testValue, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0)
            {
                return matches;
            }
            matches = names.Where(x => x.ToLower().Contains(testValue.ToLower())).ToList(); // NOSONAR
        }
        return matches;
    }
}

[Serializable]
public class SingleFileExport
{
    public string OutputFolder = string.Empty;
    public string FileName = "Default";
    public string Extension = ".usdc";

    [Newtonsoft.Json.JsonConstructor]
    public SingleFileExport()
    {
        OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Omniverse\UsdExporterRevit").Replace('\\', '/');
    }

    // Check if it is a usd file extension.
    public bool CheckExtension()
    {
        string extensionLower = Extension.ToLower();
        return (extensionLower == ".usd" || extensionLower == ".usdc" || extensionLower == ".usda");
    }
}

[Serializable]
public class ViewModifications
{
    public string DetailLevel = string.Empty;
    public string PhaseFilter = string.Empty;
    public string ViewTemplate = string.Empty;

    [Newtonsoft.Json.JsonConstructor]
    public ViewModifications()
    {
    }
}

[Serializable]
public class IncludeOptions
{
    public bool IncludeCameras = false;
    public bool IncludeLights = false;
    public bool IncludeRooms = false;
    public string RoomColorScheme = string.Empty;
    public bool IncludeSpaces = false;
    public string SpaceColorScheme = string.Empty;
    public bool IncludeDrawings = false;
    public string DrawingPublishSet = string.Empty;
    public bool InstanceFamilies = false;
    public FamilyInstancingStyle FamilyInstanceStyle = FamilyInstancingStyle.None;
    public bool IncludeBimData = false;
    public bool IncludeLinks = false; // always as payloads, dont give them a choice
    public int CoordinateSystem = 0; // todo define enumeration of options
    public string MaterialFolderName = "Looks";
    public MaterialStyle MaterialStyle = MaterialStyle.ExternalLibraryAsReference;
    public UnitType UnitType = UnitType.Feet;

    [Newtonsoft.Json.JsonConstructor]
    public IncludeOptions()
    {
    }
}

public enum FamilyInstancingStyle
{
    None,
    InternalClasses,
    ExternalAssetAsReference,
    ExternalAssetAsPayload
}

public enum MaterialStyle
{
    InternalLibrary,
    ExternalLibraryAsReference,
    ExternalLibraryAsPayload
}

public enum UnitType
{
    Feet,
    Inches,
    Meters,
    Centimeters,
    Millimeters,
    Micrometers,
    Nanometers
}

[Serializable]
public class AssetMappings
{
    public AssetLibraries Libraries = new AssetLibraries();
    public MaterialMappings Materials = new MaterialMappings();
    public FamilyTypeMappings FamilyTypes = new FamilyTypeMappings();

    // Track which mapping mode is active
    public bool IsMaterialsMode = true; // true = Materials, false = Families

    // UI state for Materials mode
    public MappingUIState MaterialsUIState = new MappingUIState();

    // UI state for Families mode
    public MappingUIState FamiliesUIState = new MappingUIState();

    [Newtonsoft.Json.JsonConstructor]
    public AssetMappings()
    {
    }
}

[Serializable]
public class MappingUIState
{
    public string SelectedLibrary = string.Empty;
    public string SelectedGroup = string.Empty;
    public string SelectedFile = string.Empty;
    public string SelectedModule = string.Empty;
    public string SelectedCategory = string.Empty;

    [Newtonsoft.Json.JsonConstructor]
    public MappingUIState()
    {
    }
}

[Serializable]
public class AssetLibraries
{
    public List<string> MaterialFolders = new List<string>();
    public List<string> AssetFolders = new List<string>();

    [Newtonsoft.Json.JsonConstructor]
    public AssetLibraries()
    {
    }
}

[Serializable]
public class MaterialMappings
{
    public string DefaultLibraryUri = "https://omniverse-content-production.s3-us-west-2.amazonaws.com/Materials";
    public List<UserMaterialMapping> UserMapped = new List<UserMaterialMapping>();

    [Newtonsoft.Json.JsonConstructor]
    public MaterialMappings()
    {
    }
#if REV2026 || REV2025 || REV2024
    public void AddMaterialMapping(Autodesk.Revit.DB.Material material, string mdlPath, string mdlModule)
    {
        if (material != null)
        {
            UserMapped.Add(new UserMaterialMapping(material, mdlPath, mdlModule));
        }
    }
#endif
}

[Serializable]
public class UserMaterialMapping
{
    public long Id;
    public string Name;
    public string MdlPath;
    public string MdlModule;

    [Newtonsoft.Json.JsonConstructor]
    public UserMaterialMapping()
    {
    }
#if REV2026 || REV2025 || REV2024
    public UserMaterialMapping(Autodesk.Revit.DB.Material material, string mdlPath, string mdlModule) // NOSONAR
    {
        if (material != null)
        {
            Id = material.Id.GetValue();
            Name = material.Name;
            MdlPath = mdlPath;
            MdlModule = mdlModule;
        }
    }
#endif
}

[Serializable]
public class FamilyTypeMappings
{
    public string DefaultLibraryUri = "https://omniverse-content-production.s3-us-west-2.amazonaws.com/Assets";
    public List<UserFamilyTypeMapping> UserMapped = new List<UserFamilyTypeMapping>();

    [Newtonsoft.Json.JsonConstructor]
    public FamilyTypeMappings()
    {
    }

#if REV2026 || REV2025 || REV2024
    public void AddFamilyTypeMapping(ElementType familyType, string assetPath)
    {
        if (familyType != null)
        {
            UserMapped.Add(new UserFamilyTypeMapping(familyType, assetPath));
        }
    }
#endif
}

[Serializable]
public class UserFamilyTypeMapping
{
    public long Id;
    public string FamilyName;
    public string TypeName;
    public string AssetPath;

    [Newtonsoft.Json.JsonConstructor]
    public UserFamilyTypeMapping()
    {
    }
#if REV2026 || REV2025 || REV2024
    // is FamilySymbol the right type?
    public UserFamilyTypeMapping(ElementType familyType, string assetPath) // NOSONAR
    {
        if (familyType != null)
        {
            Id = familyType.Id.GetValue();
            FamilyName = familyType.FamilyName;
            TypeName = familyType.Name;
            AssetPath = assetPath;
        }
    }
#endif
}

[Serializable]
public class BatchExport
{
    public string InputFolder = string.Empty;
    public string OutputFolder = string.Empty;
    public int FolderDepth = 0;
    public string ViewToFind = string.Empty;

    [Newtonsoft.Json.JsonConstructor]
    public BatchExport()
    {
        ViewToFind = "3D";
        OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Omniverse\UsdExporterRevit\Batch").Replace('\\', '/');
#if REV2026
        InputFolder = "C:/Program Files/Autodesk/Revit 2026/Samples";
#endif
#if REV2025
        InputFolder = "C:/Program Files/Autodesk/Revit 2025/Samples";
#endif
#if REV2024
        InputFolder = "C:/Program Files/Autodesk/Revit 2024/Samples";
#endif
    }
}
public enum UsdExporterRevitSettingType
{
    DetailLevel,
    PhaseFilter,
    ViewToFind,
    ViewTemplate,
    FolderDepth,
    ExportLinks,
    PublishSet,
    RoomColorScheme,
    SpaceColorScheme
}
}

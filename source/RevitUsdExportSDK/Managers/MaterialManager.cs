// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Revit = Autodesk.Revit.DB;

namespace RevitUsdExportSdk
{
internal static class MaterialManager
{
    public static string ADSKmaterials;
    public static string ADSKtextures;

    public static bool ADSKexists = false;

    private static List<MaterialSource> sourceData = new List<MaterialSource>();

    // Cache for constant time material lookups by stage id and material id
    // Structure: stageId -> materialId -> Material
    private static Dictionary<long, Dictionary<long, Material>> stageMaterials = new Dictionary<long, Dictionary<long, Material>>();

    // Lightweight cache for hot-path GetMaterialPath lookups (called for every mesh/cylinder when binding materials)
    // Structure: stageId -> materialId -> materialPath
    private static Dictionary<long, Dictionary<long, string>> stageMaterialPaths = new Dictionary<long, Dictionary<long, string>>();

    // texture image files to copy: key = copy to path, value = copy from path
    private static Dictionary<string, string> textureFiles = new Dictionary<string, string>();

    public static void Initialize()
    {
        ADSKmaterials = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)}\Autodesk Shared\Materials\";

        // check if ADSKmaterials exists, if not we then check CommonProgramFiles
        if (!Directory.Exists(ADSKmaterials))
        {
            ADSKmaterials = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles)}\Autodesk Shared\Materials\";
        }

        ADSKtextures = ADSKmaterials + @"Textures\";
        DirectoryInfo dir = new DirectoryInfo(ADSKtextures);
        ADSKexists = dir.Exists;
        if (!ADSKexists)
        {
            revit.log.warning($"Could not find Revit materials at {ADSKmaterials}");
        }
        sourceData = new List<MaterialSource>();
        stageMaterials = new Dictionary<long, Dictionary<long, Material>>();
        stageMaterialPaths = new Dictionary<long, Dictionary<long, string>>();
        textureFiles = new Dictionary<string, string>();
    }

    public static void ProcessMaterials(Document doc, Prim rootPrim)
    {
        List<Autodesk.Revit.DB.Material> materials = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Material)).Cast<Autodesk.Revit.DB.Material>().ToList();
        prepSourceData(materials);
        processSourceData(rootPrim);
    }

    // Helper to cache material in both dictionaries for material info lookup performance
    public static void CacheMaterial(long stageId, long materialId, Material material)
    {
        if (!stageMaterials.TryGetValue(stageId, out Dictionary<long, Material> matCache))
        {
            matCache = new Dictionary<long, Material>();
            stageMaterials.Add(stageId, matCache);
        }
        matCache[materialId] = material;

        if (!stageMaterialPaths.TryGetValue(stageId, out Dictionary<long, string> pathCache))
        {
            pathCache = new Dictionary<long, string>();
            stageMaterialPaths.Add(stageId, pathCache);
        }
        pathCache[materialId] = material.Path;
    }

    // todo material mappings should come from settings
    private static void prepSourceData(List<Revit.Material> materials, bool clearTextureFileData = false)
    {
        // todo different paths for mapped and native revit materials
        sourceData.Clear();
        if (clearTextureFileData)
        {
            textureFiles.Clear();
        }

        foreach (Revit.Material m in materials)
        {
            ElementId aId = m.AppearanceAssetId;
            if (aId.GetValue() != ElementId.InvalidElementId.GetValue())
            {
                AppearanceAssetElement aElement = m.Document.GetElement(aId) as AppearanceAssetElement;
                Asset a = aElement.GetRenderingAsset();
                sourceData.Add(new MaterialSource(m, a));
            }
            else
            {
                sourceData.Add(new MaterialSource(m, null));
            }
        }
    }
    private static void processSourceData(Prim parent)
    {
        Scope looks = getLooks(parent) as Scope;
        List<Material> materials = new List<Material>();

        Material defaultMat = new Material(looks.StageId, looks, "default_mat");
        materials.Add(defaultMat);
        defaultMat.InUse = true;
        defaultMat.ActivateBranch();

        foreach (MaterialSource source in sourceData)
        {
            Material m;
            if (ExportManager.Settings.Mappings.Materials.UserMapped.Any(mat => mat.Id == source.Material.Id.GetValue()))
            {
                UserMaterialMapping map = ExportManager.Settings.Mappings.Materials.UserMapped.Where(mat => mat.Id == source.Material.Id.GetValue()).First();
                m = new Material(looks.StageId, looks, map.MdlPath, map.MdlModule, source.Material);
            }
            else
            {
                m = new Material(looks.StageId, looks, source);
            }
            materials.Add(m);
        }

        looks.Children.AddRange(materials);

        foreach (Material mat in materials)
        {
            CacheMaterial(parent.StageId, mat.Id, mat);
            // Also add default material with special IDs -1 and 0
            if (mat.Name == "default_mat")
            {
                CacheMaterial(parent.StageId, -1, mat);
                CacheMaterial(parent.StageId, 0, mat);
            }
        }
    }

    public static Dictionary<string, Material> ProcessColorScheme(Prim parent, ColorFillScheme scheme, string prefix)
    {
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Scope looks = getLooks(parent) as Scope;
        foreach (ColorFillSchemeEntry entry in scheme.GetEntries())
        {
            string value = entry.GetStringValue();
            if (materials.ContainsKey(value))
            {
                revit.log.warning($"Color fill scheme \"{scheme.Name}\" contains duplicate entry \"{value}\". The first entry will be used.");
                continue;
            }

            Color color = entry.Color;

            // materials are organized by revit id (long) so for these generated color materials, we need to construct a unique long
            // revit material ids are positive, so we *-1 to avoid collisions
            string colorString = color.Red.ToString() + color.Green.ToString() + color.Blue.ToString();
            long colorId = long.Parse(colorString) * -1;
            Material mat = new Material(looks.StageId, looks, prefix + value, color, colorId);

            CacheMaterial(parent.StageId, colorId, mat);
            materials.Add(value, mat);
        }
        return materials;
    }

    public static Dictionary<string, Material> CopySpatialElementMaterials(long fromStageId, long toStageId)
    {
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Stage fromStage = ExportManager.TryGetStage(fromStageId);
        Scope fromLooks = getLooks(fromStage.Default) as Scope;

        foreach (Prim p in fromLooks.Children)
        {
            if (p is Material mat && mat.Id < 0 && mat.Name != "default_mat")
            {
                Material copy = CopyMaterial(mat.Id, fromStageId, toStageId);
                materials.Add(mat.Name, copy);
            }
        }
        return materials;
    }

    public static Material GetMaterial(long stageId, long materialId)
    {
        Material material = null;
        if (stageMaterials.TryGetValue(stageId, out Dictionary<long, Material> matCache))
        {
            matCache.TryGetValue(materialId, out material);
        }
        return material;
    }
    public static Material CopyMaterial(long materialId, long fromStageId, long toStageId)
    {
        Stage fromStage = ExportManager.TryGetStage(fromStageId);
        Stage toStage = ExportManager.TryGetStage(toStageId);

        Material alreadyCopied = GetMaterial(toStageId, materialId);

        if (alreadyCopied != null)
        {
            return alreadyCopied;
        }

        Prim fromLooks = getLooks(fromStage.Default);
        Material source = fromLooks.Children.Cast<Material>().Where(m => m.Id == materialId).First(); // assumes material exists...
        string materialFolderName = (string.IsNullOrEmpty(source.DisplayName)) ? source.Name : source.DisplayName;
        materialFolderName = materialFolderName.RemoveBadWindowsFilePathChars(); // do SUBSTRING!
        materialFolderName = (materialFolderName.Length > 25) ? materialFolderName.Substring(0, 25) : materialFolderName;
        materialFolderName = materialFolderName.TrimEnd();

        string fromStageFolder = fromStage.FolderPath + "/" + ExportManager.Settings.Options.MaterialFolderName + "/";
        string toStageFolder = toStage.FolderPath + "/" + ExportManager.Settings.Options.MaterialFolderName + "/";
        fromStageFolder = NormalizePath(fromStageFolder);
        toStageFolder = NormalizePath(toStageFolder);

        Dictionary<string, string> files = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> file in textureFiles)
        {
            // Populate 'files' with required textures that we want to copyto the target folder
            string folderPath = Path.GetDirectoryName(file.Key);
            if (folderPath.Contains(materialFolderName))
            {
                files.Add(file.Key.Replace(fromStageFolder, toStageFolder), file.Value);
            }
        }
        foreach (KeyValuePair<string, string> file in files)
        {
            AddTexture(file.Key, file.Value);
        }

        Prim toLooks = getLooks(toStage.Default);
        Material mat = source.CopyTo(toLooks);

        CacheMaterial(toStageId, materialId, mat);

        return mat;
    }
    private static Prim getLooks(Prim parent)
    {
        string materialFolderName = ExportManager.Settings.Options.MaterialFolderName;
        foreach (Prim child in parent.Children)
        {
            if (child.Name == materialFolderName)
            {
                return child;
            }
        }
        // Not found, create new
        Prim looks = new Scope(parent.StageId, materialFolderName, parent);
        return looks;
    }
    public static void AddTexture(string toPath, string fromPath)
    {
        // Normalize paths: replace backslashes with forward slashes
        toPath = toPath.Replace("\\", "/");
        fromPath = fromPath.Replace("\\", "/");

        // Only collapse duplicate slashes that are NOT part of a URI scheme separator (://)
        toPath = NormalizePath(toPath);
        fromPath = NormalizePath(fromPath);

        if (textureFiles.TryGetValue(toPath, out string existingPath))
        {
            textureFiles[toPath] = fromPath;
        }
        else
        {
            textureFiles.Add(toPath, fromPath);
        }
    }

    private static string NormalizePath(string path)
    {
        // preserve the scheme separator (://) if present
        int schemeEnd = path.IndexOf("://");
        if (schemeEnd > 0)
        {
            // Has a URI scheme, normalize only the path portion
            string scheme = path.Substring(0, schemeEnd + 3);
            string pathPortion = path.Substring(schemeEnd + 3);
            pathPortion = pathPortion.Replace("//", "/");
            return scheme + pathPortion;
        }
        else
        {
            path = path.Replace("//", "/");
            return path;
        }
    }
    public static void CopyTextures()
    {
        foreach (KeyValuePair<string, string> kvp in textureFiles)
        {
            copy(kvp.Value, kvp.Key);
        }
    }

    private static bool copy(string fromPath, string toPath)
    {
        if (!File.Exists(fromPath))
        {
            revit.log.warning($"Source file does not exist: \"{fromPath}\"");
            return false;
        }

        if (!revit.file.client.isLocalUri(toPath))
        {
            revit.log.warning($"Copying textures to non-local paths is not supported: \"{toPath}\"");
            return false;
        }

        string directoryPath = Path.GetDirectoryName(toPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        try
        {
            File.Copy(fromPath, toPath, true);
            return true;
        }
        catch (Exception ex)
        {
            revit.log.warning($"Failed to copy texture \"{fromPath}\" to \"{toPath}\": {ex.Message}");
            return false;
        }
    }

    public static string GetMaterialPath(long stageId, long materialId)
    {
        // Use lightweight path cache for performance (hot path - calle for each cylinder and mesh when binding materials)
        if (stageMaterialPaths.TryGetValue(stageId, out Dictionary<long, string> pathCache))
        {
            if (pathCache.TryGetValue(materialId, out string materialPath))
            {
                return materialPath;
            }
        }
        return string.Empty;
    }
    public static void UseMaterial(long stageId, long materialId)
    {
        if (stageMaterials.TryGetValue(stageId, out Dictionary<long, Material> matCache))
        {
            if (matCache.TryGetValue(materialId, out Material material))
            {
                material.InUse = true;
                material.ActivateBranch();
                foreach (var kvp in material.Textures)
                {
                    AddTexture(kvp.Key, kvp.Value);
                }
            }
        }
    }

    public static bool IsGlass(long stageId, long materialId)
    {
        if (stageMaterials.TryGetValue(stageId, out Dictionary<long, Material> matCache))
        {
            if (matCache.TryGetValue(materialId, out Material material))
            {
                return material.Module == OmniGlass.Module;
            }
        }
        return false;
    }
}
}

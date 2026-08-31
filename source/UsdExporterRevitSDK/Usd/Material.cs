// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Revit = Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using System.IO;
using Autodesk.Revit.DB;

namespace UsdExporterRevitSdk
{
internal class MaterialSource
{
    public Revit.Material Material;
    public Asset Asset;
    public MaterialSource(Revit.Material material, Asset asset)
    {
        Material = material;
        Asset = asset;
    }
}
internal class Material : Prim
{
    public MaterialAttributes Attributes;
    public string AssetPath;
    public string Module;

    public string TextureFolderAbsolute;
    public string TextureFolderRelative;

    public Dictionary<string, string> Textures = new Dictionary<string, string>();

    public bool InUse = false;

    // constructor for autodesk materials
    public Material(long stageId, Prim parent, MaterialSource source) : base(stageId, source.Material.Name, parent)
    {
#if DEBUG && MATERIALS
        usd.exporter.revit.log.info(source.Material.Name);
#endif
        PrimType = PrimType.Material;
        Kind = PrimKind.None;
        Id = source.Material.Id.GetValue();

        // these will be set during asset processing
        Attributes = new MaterialAttributes();
        AssetPath = string.Empty;
        Module = string.Empty;

        Stage stage = ExportManager.TryGetStage(stageId);

        string folderName = (string.IsNullOrEmpty(DisplayName)) ? Name : DisplayName;
        folderName = folderName.RemoveBadWindowsFilePathChars();
        folderName = (folderName.Length > 25) ? folderName.Substring(0, 25) : folderName;
        folderName = folderName.TrimEnd();
        TextureFolderAbsolute = stage.FolderPath.Replace("\\", "/");
        if (!TextureFolderAbsolute.EndsWith("/")) // NOSONAR
            TextureFolderAbsolute += "/";
        TextureFolderAbsolute += $"{ExportManager.Settings.Options.MaterialFolderName}/" + folderName + "/";
        TextureFolderRelative = $"./{ExportManager.Settings.Options.MaterialFolderName}/" + folderName + "/";

        processSource(source);
    }

    // constructor for mapped materials
    public Material(long stageId, Prim parent, string assetPath, string module, Revit.Material material) : base(stageId, material.Name, parent)
    {
        PrimType = PrimType.Material;
        Kind = PrimKind.None;
        Id = material.Id.GetValue();
        Attributes = new MaterialAttributes();
        AssetPath = assetPath;
        Module = module;
    }

    // constructor for generic color blob (rooms and spaces)
    public Material(long stageId, Prim parent, string name, Color color, long materialId) : base(stageId, name, parent)
    {
        PrimType = PrimType.Material;
        Kind = PrimKind.None;
        Id = materialId;
        Attributes = new MaterialAttributes();
        setOmniGlass();
        Attributes.GfVec3fs.Add(OmniGlass.GlassColor, usd.exporter.revit.core.sRgbToLinear(new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f)));
        Attributes.Floats.Add(OmniGlass.Ior, 1.0f);
        Attributes.Floats.Add(OmniGlass.OpacityAmount, 0.0f);
    }

    // constructor for drawings
    public Material(long stageId, Prim parent, Autodesk.Revit.DB.View view, string name, string imagePath) : base(stageId, name, parent)
    {
        PrimType = PrimType.Material;
        Kind = PrimKind.None;
        Id = view.Id.GetValue();
        Attributes = new MaterialAttributes();
        setOmniPbr();
        Attributes.Assets.Add(OmniPBR.AlbedoMap, imagePath);
        InUse = true;
    }

    public Material(long stageId, Prim parent, string name) : base(stageId, name, parent)
    {
        PrimType = PrimType.Material;
        Kind = PrimKind.None;
        Id = -1;
        Attributes = new MaterialAttributes();
        setOmniPbr();
        InUse = true;
    }

    // private copy constructor
    private Material(long stageId, Prim parent, Material material) : base(stageId, material.Name, parent)
    {
        PrimType = PrimType.Material;
        Kind = PrimKind.None;
        Path = Parent.Path + "/" + Name;
        Id = material.Id;
        Attributes = new MaterialAttributes();
        foreach (KeyValuePair<string, string> pair in material.Attributes.Assets)
        {
            Attributes.Assets.Add(pair.Key, pair.Value); // assets are relative so shouldnt need adjustment
        }
        foreach (KeyValuePair<string, bool> pair in material.Attributes.Booleans)
        {
            Attributes.Booleans.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<string, int> pair in material.Attributes.Integers)
        {
            Attributes.Integers.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<string, float> pair in material.Attributes.Floats)
        {
            Attributes.Floats.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<string, usd.exporter.revit.GfVec2f> pair in material.Attributes.GfVec2fs)
        {
            usd.exporter.revit.GfVec2f v2fs = new usd.exporter.revit.GfVec2f(pair.Value.x, pair.Value.y);
            Attributes.GfVec2fs.Add(pair.Key, v2fs);
        }
        foreach (KeyValuePair<string, usd.exporter.revit.GfVec3f> pair in material.Attributes.GfVec3fs)
        {
            usd.exporter.revit.GfVec3f v3fs = new usd.exporter.revit.GfVec3f(pair.Value.x, pair.Value.y, pair.Value.z);
            Attributes.GfVec3fs.Add(pair.Key, v3fs);
        }
        AssetPath = material.AssetPath;
        Module = material.Module;
    }

    public override void Write(long stageId)
    {
        if (InUse)
        {
            bool exists = false;
            if (this.AssetPath == OmniPBR.Path)
            {
                usd.exporter.revit.GfVec3f color =
                    Attributes.GfVec3fs.TryGetValue(OmniPBR.AlbedoColor, out usd.exporter.revit.GfVec3f albedoColor) ? albedoColor : usd.exporter.revit.core.sRgbToLinear(new usd.exporter.revit.GfVec3f(225f / 255f, 225f / 255f, 225f / 255f));
                float opacity = Attributes.Floats.TryGetValue(OmniPBR.OpacityAmount, out float opacityAmount) ? opacityAmount : OmniPBR.DefaultOpacity;
                opacity = clamp(opacity);
                float roughness = Attributes.Floats.TryGetValue(OmniPBR.RoughnessAmount, out float roughnessAmount) ? roughnessAmount : OmniPBR.DefaultRoughness;
                roughness = clamp(roughness);
                float metallic = Attributes.Floats.TryGetValue(OmniPBR.MetallicAmount, out float metallicAmount) ? metallicAmount : OmniPBR.DefaultMetallic;
                metallic = clamp(metallic);
                usd.exporter.revit.core.defineOmniPbrMaterial(stageId, Path, color, opacity, roughness, metallic);
            }
            else if (this.AssetPath == OmniGlass.Path)
            {
                usd.exporter.revit.GfVec3f color = Attributes.GfVec3fs.TryGetValue(OmniGlass.GlassColor, out usd.exporter.revit.GfVec3f glassColor) ? glassColor : new usd.exporter.revit.GfVec3f(1.0f, 1.0f, 1.0f);
                float ior = Attributes.Floats.TryGetValue(OmniGlass.Ior, out float iorValue) ? iorValue : OmniGlass.DefaultIor;
                ior = clamp(ior, 1.0f, 4.0f);
                float roughness = Attributes.Floats.TryGetValue(OmniGlass.RoughnessAmount, out float roughnessAmount) ? roughnessAmount : OmniGlass.DefaultRoughness;
                roughness = clamp(roughness);
                usd.exporter.revit.core.defineOmniGlassMaterial(stageId, Path, color, ior, roughness);
            }
            else
            {
                usd.exporter.revit.core.createMaterial(stageId, Parent.Path, Name);
                usd.exporter.revit.core.createMdlShader(stageId, Path, "MdlShader", AssetPath, Module, true);
            }
            if (exists)
            {
                pxr.usd.prim.setPrimToOver(stageId, Path);
            }
            Attributes.Write(stageId, Path);
            base.Write(stageId);
        }
    }

    private static float clamp(float value, float min = 0.0f, float max = 1.0f)
    {
        return (value > max) ? max : (value < min) ? min : value;
    }

    public Material CopyTo(Prim prim)
    {
        Material material = null;
        if (!prim.HasChild(this.Name))
        {
            material = new Material(prim.StageId, prim, this);
        }
        else
        {
            material = prim.GetChild(this.Name) as Material;
        }
        return material;
    }

    private bool isGlass()
    {
        string materialName = this.Name;
        bool isGlass = false;
        if (materialName.ToLower().Contains("glas") || materialName.ToLower().Contains("glaz")) // NOSONAR
        {
            isGlass = true;
        }
        return isGlass;
    }

    private void processSource(MaterialSource source)
    {
        if (source.Asset != null)
        {
            AssetProperty ap = source.Asset.FindByName("BaseSchema");
            if (ap != null)
            {
                string baseScheme = (ap as AssetPropertyString).Value;
#if DEBUG && MATERIALS
                usd.exporter.revit.log.info(baseScheme);
#endif
                switch (baseScheme)
                {
                    case "GenericSchema":
                        getGenericAttributes(source.Asset);
                        break;
                    case "PrismMetalSchema":
                        getPrismMetalAttributes(source.Asset);
                        break;
                    case "PrismLayeredSchema":
                        getPrismLayeredAttributes(source.Asset);
                        break;
                    case "PrismOpaqueSchema":
                        getPrismOpaqueAttributes(source.Asset);
                        break;
                    case "PrismTransparentSchema":
                        getPrismTransparentAttributes(source.Asset);
                        break;
                    case "PrismGlazingSchema":
                        getPrismGlazingAttributes(source.Asset);
                        break;
                    case "MasonryCMUSchema":
                        getMasonryCmuAttributes(source.Asset);
                        break;
                    case "MetalSchema":
                        getMetalAttributes(source.Asset);
                        break;
                    case "MirrorSchema":
                        getMirrorAttributes(source.Asset);
                        break;
                    case "ConcreteSchema":
                        getConcreteAttributes(source.Asset);
                        break;
                    case "PlasticVinylSchema":
                        getPlasticVinylAttributes(source.Asset);
                        break;
                    case "StoneSchema":
                        getStoneAttributes(source.Asset);
                        break;
                    case "WallPaintSchema":
                        getWallPaintAttributes(source.Asset);
                        break;
                    case "HardwoodSchema":
                        getHardwoodAttributes(source.Asset);
                        break;
                    case "SolidGlassSchema":
                        getSolidGlassAttributes(source.Asset);
                        break;
                    case "GlazingSchema":
                        getGlazingAttributes(source.Asset);
                        break;
                    case "CeramicSchema":
                        getCeramicAttributes(source.Asset);
                        break;
                    case "WaterSchema":
                        getWaterAttributes(source.Asset);
                        break;
                    case "MetallicPaintSchema":
                        getMetallicPaintAttributes(source.Asset);
                        break;
                    default:
                        break;
                }
                return;
            }
        }
        Revit.Color color = source.Material.Color;
        if (isGlass() || source.Material.Transparency > 10)
        {
            setOmniGlass();
            try
            {
                this.Attributes.GfVec3fs.Add(OmniGlass.GlassColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
            }
            catch (Exception e)
            {
                usd.exporter.revit.log.warning(e.Message);
            }
        }
        else
        {
            setOmniPbr();
            try
            {
                this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
            }
            catch (Exception e)
            {
                usd.exporter.revit.log.warning(e.Message);
            }
        }
    }
    private void setOmniPbr()
    {
        AssetPath = OmniPBR.Path;
        Module = OmniPBR.Module;
    }
    private void setOmniGlass()
    {
        AssetPath = OmniGlass.Path;
        Module = OmniGlass.Module;
    }
    private void setUV(Asset asset)
    {
        AssetPropertyDistance prop = (asset.FindByName("texture_RealWorldOffsetX") as AssetPropertyDistance);
        double transX = (prop == null) ? 0 : UnitUtils.Convert(prop.Value, prop.GetUnitTypeId(), UnitTypeId.Feet);
        prop = (asset.FindByName("texture_RealWorldOffsetY") as AssetPropertyDistance);
        double transY = (prop == null) ? 0 : UnitUtils.Convert(prop.Value, prop.GetUnitTypeId(), UnitTypeId.Feet);
        prop = (asset.FindByName("texture_RealWorldScaleX") as AssetPropertyDistance);
        double scaleX = (prop == null) ? 1 : UnitUtils.Convert(prop.Value, prop.GetUnitTypeId(), UnitTypeId.Feet);
        prop = (asset.FindByName("texture_RealWorldScaleY") as AssetPropertyDistance);
        double scaleY = (prop == null) ? 1 : UnitUtils.Convert(prop.Value, prop.GetUnitTypeId(), UnitTypeId.Feet);

        transX = (transX.Equals(0)) ? 0 : (scaleX.Equals(0)) ? transX : 1 - (transX / scaleX);
        transY = (transY.Equals(0)) ? 0 : (scaleY.Equals(0)) ? transY : 1 - (transY / scaleY);
        scaleX = (scaleX.Equals(0)) ? 0 : 1 / scaleX;
        scaleY = (scaleY.Equals(0)) ? 0 : 1 / scaleY;

        if (this.Module == OmniGlass.Module)
        {
            this.Attributes.GfVec2fs.Add(OmniGlass.TextureTranslate, new usd.exporter.revit.GfVec2f((float)transX, (float)transY));
            this.Attributes.GfVec2fs.Add(OmniGlass.TextureScale, new usd.exporter.revit.GfVec2f((float)scaleX, (float)scaleY));
        }
        else
        {
            this.Attributes.GfVec2fs.Add(OmniPBR.TextureTranslate, new usd.exporter.revit.GfVec2f((float)transX, (float)transY));
            this.Attributes.GfVec2fs.Add(OmniPBR.TextureScale, new usd.exporter.revit.GfVec2f((float)scaleX, (float)scaleY));
        }
    }
    private void setAlbedoMap(Asset asset)
    {
        string assetPath = getTexturePath(asset, "albedo");
        if (assetPath != string.Empty)
        {
            this.Attributes.Assets.Add(OmniPBR.AlbedoMap, assetPath);
            setUV(asset);
        }
    }
    private void setGlassMap(Asset asset)
    {
        string assetPath = getTexturePath(asset, "glass");
        if (assetPath != string.Empty)
        {
            this.Attributes.Assets.Add(OmniGlass.GlassMap, assetPath);
            setUV(asset);
        }
    }
    private void setNormalMap(Asset asset, bool setAmount, bool setUVs = false)
    {
        double normalAmount = 1.0;
        string assetPath = getTexturePath(asset, "normal");
        if (asset.Name == "UnifiedBitmapSchema")
        {
            normalAmount = (asset.FindByName("unifiedbitmap_RGBAmount") as AssetPropertyDouble).Value;
        }
        else if (asset.Name == "BumpMapSchema")
        {
            normalAmount = (asset.FindByName("bumpmap_Depth") as AssetPropertyDistance).Value;
        }
        if (setAmount)
        {
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
        }
        if (assetPath != string.Empty)
        {
            this.Attributes.Assets.Add(OmniPBR.NormalMap, assetPath);
        }
        if (setUVs)
        {
            setUV(asset);
        }
    }
    private void setOpacityMap(Asset asset, bool setUVs = false)
    {
        string assetPath = getTexturePath(asset, "opacity");
        if (assetPath != string.Empty)
        {
            this.Attributes.Assets.Add(OmniPBR.OpacityMap, assetPath);
        }
        if (setUVs)
        {
            setUV(asset);
        }
    }
    private void setRoughnessMap(Asset asset, bool setUVs = false)
    {
        string assetPath = getTexturePath(asset, "roughness");
        if (assetPath != string.Empty)
        {
            if (this.Module == OmniGlass.Module)
            {
                this.Attributes.Assets.Add(OmniGlass.RoughnessMap, assetPath);
            }
            else
            {
                this.Attributes.Assets.Add(OmniPBR.RoughnessMap, assetPath);
            }
        }
        if (setUVs)
        {
            setUV(asset);
        }
    }

    private void setEmissiveMap(Asset asset, bool setUVs = false)
    {
        string assetPath = getTexturePath(asset, "emissive");
        if (assetPath != string.Empty)
        {
            this.Attributes.Assets.Add(OmniPBR.EmissiveMaskMap, assetPath);
        }
        if (setUVs)
        {
            setUV(asset);
        }
    }
    private string getTexturePath(string autodeskFileName, string propName)
    {
        string relativePath = string.Empty;
        string path = MaterialManager.ADSKtextures + autodeskFileName;
        if (File.Exists(path))
        {
            FileInfo file = new FileInfo(path);
            relativePath = this.TextureFolderRelative + propName + file.Extension;
            string fullPath = this.TextureFolderAbsolute + propName + file.Extension;
            if (!Textures.TryGetValue(fullPath, out string existingTexture))
            {
                Textures.Add(fullPath, path.Replace("\\", "/"));
            }
        }
        return relativePath;
    }
    private string getTexturePath(Asset asset, string propName)
    {
        string relativePath = string.Empty;
        string path = string.Empty;
        if (asset.Name == "UnifiedBitmapSchema" || asset.Name == "UnifiedBitmap")
        {
            path = (asset.FindByName("unifiedbitmap_Bitmap") as AssetPropertyString).Value;
        }
        else if (asset.Name == "BumpMapSchema" || asset.Name == "BumpMap")
        {
            path = (asset.FindByName("bumpmap_Bitmap") as AssetPropertyString).Value;
        }
        if (File.Exists(path))
        {
            FileInfo file = new FileInfo(path);
            relativePath = this.TextureFolderRelative + propName + file.Extension;
            string fullPath = this.TextureFolderAbsolute + propName + file.Extension;
            if (!Textures.TryGetValue(fullPath, out string existingTexture))
            {
                Textures.Add(fullPath, path.Replace("\\", "/"));
            }
        }
        else
        {
            path = path.Replace("/\\", "\\");
            bool foundTexture = false;
            string[] splits = path.Split('|');
            for (int j = splits.Length - 1; j >= 0; j--)
            {
                if (splits[j].StartsWith("3/") || splits[j].StartsWith("2/") || splits[j].StartsWith("1/"))
                {
                    string testPath = $"{MaterialManager.ADSKtextures}{splits[j].Replace('/', '\\')}";
                    if (File.Exists(testPath))
                    {
                        path = testPath;
                        foundTexture = true;
                        break;
                    }
                }
                else
                {
                    for (int i = 3; i > 0; i--)
                    {
                        string testPath = $@"{MaterialManager.ADSKtextures}{i}\Mats\{splits[j]}";
                        if (File.Exists(testPath))
                        {
                            path = testPath;
                            foundTexture = true;
                            break;
                        }
                    }
                    if (foundTexture)
                    {
                        break;
                    }
                }
            }
            if (!foundTexture && splits.Length > 0)
            {
                if (splits[0].Length >= 3 && splits[0].Substring(1, 2) == ":/")
                {
                    string testPath = splits[0].Replace('/', '\\');
                    if (File.Exists(testPath))
                    {
                        path = testPath;
                        foundTexture = true;
                    }
                }
                else if (splits[0].StartsWith("3\\") || splits[0].StartsWith("2\\") || splits[0].StartsWith("1\\"))
                {
                    string testPath = $"{MaterialManager.ADSKtextures}{splits[0]}";
                    if (File.Exists(testPath))
                    {
                        path = testPath;
                        foundTexture = true;
                    }
                }
                else
                {
                    for (int i = 3; i > 0; i--)
                    {
                        string testPath = $@"{MaterialManager.ADSKtextures}{i}\Mats\{splits[0]}";
                        if (File.Exists(testPath))
                        {
                            path = testPath;
                            foundTexture = true;
                            break;
                        }
                    }
                }
            }
            if (foundTexture)
            {
                FileInfo file = new FileInfo(path);
                relativePath = this.TextureFolderRelative + propName + file.Extension;
                string fullPath = this.TextureFolderAbsolute + propName + file.Extension;
                if (!Textures.TryGetValue(fullPath, out string existingTexture))
                {
                    Textures.Add(fullPath, path.Replace("\\", "/"));
                }
            }
        }
        return relativePath;
    }

    private static Asset getConnectedAsset(AssetProperty prop)
    {
        Asset asset = null;
        if (prop.Type == AssetPropertyType.Reference)
        {
            AssetPropertyReference r = prop as AssetPropertyReference;
            if (r.NumberOfConnectedProperties > 1)
            {
                var assets = r.GetAllConnectedProperties();
                asset = assets[0] as Asset;
            }
            else
            {
                asset = r.GetSingleConnectedAsset();
            }
        }
        else if (prop.Type == AssetPropertyType.Double4)
        {
            AssetPropertyDoubleArray4d d4 = prop as AssetPropertyDoubleArray4d;
            Color color = d4.GetValueAsColor();
            if (d4.NumberOfConnectedProperties > 1)
            {
                var assets = d4.GetAllConnectedProperties();
                asset = assets[0] as Asset;
            }
            else
            {
                asset = d4.GetSingleConnectedAsset();
            }
        }
        else
        {
            List<AssetProperty> props = prop.GetAllConnectedProperties().ToList();
            if (props.Count > 0)
            {
                asset = props[0] as Asset;
            }
        }

        return asset;
    }
    private void getGenericAttributes(Asset asset)
    {
        double transparency = (asset.FindByName(Generic.GenericTransparency) as AssetPropertyDouble).Value;
        bool tintOn = (asset.FindByName(Generic.CommonTintToggle) as AssetPropertyBoolean).Value;
        Revit.Color tint = (asset.FindByName(Generic.CommonTintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        if (isGlass() || transparency > 0)
        {
            setOmniGlass();
            this.Attributes.Floats.Add(OmniGlass.Absorbtion, (float)transparency);
            AssetProperty p = asset.FindByName(Generic.GenericTransparency);
            Asset a = getConnectedAsset(p);
            if (a != null)
            {
                setGlassMap(a);
            }
            double ior = (asset.FindByName(Generic.GenericRefractionIndex) as AssetPropertyDouble).Value;
            this.Attributes.Floats.Add(OmniGlass.Ior, (float)ior);
            if (tintOn)
            {
                this.Attributes.GfVec3fs.Add(OmniGlass.GlassColor, new usd.exporter.revit.GfVec3f(tint.Red / 255f, tint.Green / 255f, tint.Blue / 255f));
            }
        }
        else
        {
            setOmniPbr();
            bool setUVs = true;
            AssetProperty p = asset.FindByName(Generic.GenericDiffuse);
            Revit.Color color = (p as AssetPropertyDoubleArray4d).GetValueAsColor();
            this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
            Asset a = getConnectedAsset(p);
            if (a != null)
            {
                setAlbedoMap(a);
                setUVs = false;
            }
            if (tintOn)
            {
                this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoTint, new usd.exporter.revit.GfVec3f(tint.Red / 255f, tint.Green / 255f, tint.Blue / 255f));
            }
            double normalAmount = (asset.FindByName(Generic.GenericBumpAmount) as AssetPropertyDouble).Value;
            p = asset.FindByName(Generic.GenericBumpMap);
            a = getConnectedAsset(p);
            if (a != null)
            {
                setNormalMap(a, false, setUVs);
                this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
                setUVs = false;
            }
            bool setRoughness = true;
            p = asset.FindByName(Generic.GenericGlossiness);
            a = getConnectedAsset(p);
            if (a != null)
            {
                setRoughnessMap(a, setUVs);
                setRoughness = false;
                setUVs = false;
            }
            p = asset.FindByName(Generic.GenericReflectivityAt90deg);
            a = getConnectedAsset(p);
            if (a != null && setRoughness)
            {
                setRoughnessMap(a, setUVs);
                setRoughness = false;
                setUVs = false;
            }
            p = asset.FindByName(Generic.GenericReflectivityAt0deg);
            a = getConnectedAsset(p);
            if (a != null && setRoughness)
            {
                setRoughnessMap(a, setUVs);
                setRoughness = false;
                setUVs = false;
            }
            p = asset.FindByName(Generic.GenericCutoutOpacity);
            double opacity = (p as AssetPropertyDouble).Value;
            a = getConnectedAsset(p);
            if (a != null)
            {
                this.Attributes.Booleans.Add(OmniPBR.OpacityOn, true);
                this.Attributes.Floats.Add(OmniPBR.OpacityAmount, (float)opacity);
                setOpacityMap(a, setUVs);
                setUVs = false;
            }
            double emissiveIntensity = (asset.FindByName(Generic.GenericSelfIllumLuminance) as AssetPropertyDouble).Value;
            if (emissiveIntensity > 0)
            {
                this.Attributes.Booleans.Add(OmniPBR.EmissiveOn, true);
                this.Attributes.Floats.Add(OmniPBR.EmissiveIntensity, (float)emissiveIntensity);
                p = asset.FindByName(Generic.GenericSelfIllumFilterMap);
                a = getConnectedAsset(p);
                if (a != null)
                {
                    setEmissiveMap(a, setUVs);
                    setUVs = false;
                }
                double emissiveTemp = (asset.FindByName(Generic.GenericSelfIllumColorTemperature) as AssetPropertyDouble).Value;
                this.Attributes.GfVec3fs.Add(OmniPBR.EmissiveColor, getColorFromTemp(emissiveTemp));
            }
        }
    }
    private void getPrismMetalAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        Revit.Color color = (asset.FindByName(AdvancedMetal.MetalF0) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        double roughness = (asset.FindByName(AdvancedMetal.SurfaceRoughness) as AssetPropertyDouble).Value;
        this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, (float)roughness);
        AssetProperty p = asset.FindByName(AdvancedMetal.SurfaceCutout);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            this.Attributes.Booleans.Add(OmniPBR.OpacityOn, true);
            setOpacityMap(a, setUVs);
            setUVs = false;
        }
        p = asset.FindByName(AdvancedMetal.SurfaceNormal);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, true, setUVs);
            setUVs = false;
        }
        this.Attributes.Floats.Add(OmniPBR.MetallicAmount, 1.0f);
        this.Attributes.Floats.Add(OmniPBR.SpecularAmount, 0.5f);
    }
    private void getPrismLayeredAttributes(Asset asset)
    {
        setOmniPbr();
        AssetProperty p = asset.FindByName(AdvancedLayered.LayeredNormal);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, true, true);
        }
        Revit.Color color = (asset.FindByName(AdvancedLayered.LayeredDiffuse) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        double roughness = (asset.FindByName(AdvancedLayered.LayeredRoughness) as AssetPropertyDouble).Value;
        this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, (float)roughness);
    }
    private void getPrismOpaqueAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        AssetProperty p = asset.FindByName(AdvancedOpaque.OpaqueAlbedo);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        p = asset.FindByName(AdvancedOpaque.SurfaceAlbedo);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setRoughnessMap(a, setUVs);
            setUVs = false;
        }
        p = asset.FindByName(AdvancedOpaque.SurfaceNormal);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, true, setUVs);
            setUVs = false;
        }
        p = asset.FindByName(AdvancedOpaque.SurfaceCutout);
        a = getConnectedAsset(p);
        if (a != null)
        {
            this.Attributes.Booleans.Add(OmniPBR.OpacityOn, true);
            setOpacityMap(a, setUVs);
            setUVs = false;
        }
        Revit.Color color = (asset.FindByName(AdvancedOpaque.OpaqueAlbedo) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Revit.Color reflectiveColor = (asset.FindByName(AdvancedOpaque.SurfaceAlbedo) as AssetPropertyDoubleArray4d).GetValueAsColor();

        bool emissive = (asset.FindByName(AdvancedOpaque.OpaqueEmission) as AssetPropertyBoolean).Value;
        if (emissive)
        {
            this.Attributes.Booleans.Add(OmniPBR.EmissiveOn, true);
            double luminance = (asset.FindByName(AdvancedOpaque.OpaqueLuminance) as AssetPropertyDouble).Value;
            this.Attributes.Floats.Add(OmniPBR.EmissiveIntensity, (float)luminance);
            Revit.Color emissiveColor = (asset.FindByName(AdvancedOpaque.OpaqueLuminanceModifier) as AssetPropertyDoubleArray4d).GetValueAsColor();
            this.Attributes.GfVec3fs.Add(OmniPBR.EmissiveColor, new usd.exporter.revit.GfVec3f(emissiveColor.Red / 255f, emissiveColor.Green / 255f, emissiveColor.Blue / 255f));
        }
    }
    private void getPrismTransparentAttributes(Asset asset)
    {
        setOmniGlass();
        double ior = (asset.FindByName(AdvancedTransparent.TransparentIor) as AssetPropertyDouble).Value;
        this.Attributes.Floats.Add(OmniGlass.Ior, (float)ior);
        double depth = (asset.FindByName(AdvancedTransparent.TransparentDistance) as AssetPropertyDistance).Value;
        this.Attributes.Floats.Add(OmniGlass.Absorbtion, (float)depth);
        Revit.Color color = (asset.FindByName(AdvancedTransparent.TransparentColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniGlass.GlassColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
    }
    private void getPrismGlazingAttributes(Asset asset)
    {
        setOmniGlass();
        Revit.Color color = (asset.FindByName(AdvancedGlazing.GlazingTransmissionColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniGlass.GlassColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Revit.Color reflectionColor = (asset.FindByName(AdvancedGlazing.GlazingF0) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniGlass.ReflectionColor, new usd.exporter.revit.GfVec3f(reflectionColor.Red / 255f, reflectionColor.Green / 255f, reflectionColor.Blue / 255f));
        Revit.Color highlightColor = (asset.FindByName(AdvancedGlazing.SurfaceAlbedo) as AssetPropertyDoubleArray4d).GetValueAsColor();

        double roughness = (asset.FindByName(AdvancedGlazing.GlazingTransmissionRoughness) as AssetPropertyDouble).Value;
        this.Attributes.Floats.Add(OmniGlass.RoughnessAmount, (float)roughness);
    }
    private void getMasonryCmuAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        AssetProperty p = asset.FindByName(MasonryCMU.MasonryCMUColor);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        Revit.Color color = (p as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        p = asset.FindByName(MasonryCMU.MasonryCMUPatternMap);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, true, setUVs);
            setUVs = false;
        }
    }
    private void getMetalAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;

        int metalPattern = getEnumOrIntValue(asset, Metal.MetalPattern);
        double normalAmount = (asset.FindByName(Metal.MetalPatternHeight) as AssetPropertyDouble).Value;
        Revit.Color color = (asset.FindByName(Metal.MetalColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        AssetProperty p = asset.FindByName(Metal.MetalPerforationsShader);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setOpacityMap(a, setUVs);
            setUVs = false;
        }
        if (metalPattern > -1)
        {
            if (metalPattern == 1)
            {
                AssetPropertyString prop = asset.FindByName("brush_def_map") as AssetPropertyString;
                string relativePath = getTexturePath(prop.Value, "normal");
                this.Attributes.Assets.Add(OmniPBR.NormalMap, relativePath);
            }
            else if (metalPattern == 2)
            {
                AssetPropertyString prop = asset.FindByName("bump_diamond_map") as AssetPropertyString;
                string relativePath = getTexturePath(prop.Value, "normal");
                this.Attributes.Assets.Add(OmniPBR.NormalMap, relativePath);
            }
            else if (metalPattern == 3)
            {
                AssetPropertyString prop = asset.FindByName("bump_checker_map") as AssetPropertyString;
                string relativePath = getTexturePath(prop.Value, "normal");
                this.Attributes.Assets.Add(OmniPBR.NormalMap, relativePath);
            }
            else if (metalPattern == 4)
            {
                p = asset.FindByName(Metal.MetalPatternShader);
                a = getConnectedAsset(p);
                if (a != null)
                {
                    setNormalMap(a, false);
                    this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
                }
            }
        }
        this.Attributes.Floats.Add(OmniPBR.MetallicAmount, 0.76f);
        this.Attributes.Floats.Add(OmniPBR.SpecularAmount, 0.5f);
    }
    private void getMirrorAttributes(Asset asset)
    {
        setOmniGlass();
        this.Attributes.Floats.Add(OmniGlass.Ior, 0.0f);
        Revit.Color color = (asset.FindByName(Mirror.MirrorTintcolor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniGlass.ReflectionColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
    }
    private void getConcreteAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        double normalAmount = (asset.FindByName(Concrete.ConcreteBumpAmount) as AssetPropertyDouble).Value;
        AssetProperty p = asset.FindByName(Concrete.ConcreteColor);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        // curveball it could also be this
        p = asset.FindByName(Concrete.ConcreteBmMap);
        a = getConnectedAsset(p);
        if (a != null && !this.Attributes.Assets.TryGetValue(OmniPBR.AlbedoMap, out string existingAsset))
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        Revit.Color color = (asset.FindByName(Concrete.ConcreteColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Revit.Color tint = (asset.FindByName(Concrete.CommonTintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        bool useTint = (asset.FindByName(Concrete.CommonTintToggle) as AssetPropertyBoolean).Value;
        if (useTint)
        {
            this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoTint, new usd.exporter.revit.GfVec3f(tint.Red / 255f, tint.Green / 255f, tint.Blue / 255f));
        }
        int finish = getEnumOrIntValue(asset, Concrete.ConcreteFinish);
        if (finish == 0)
        {
            AssetPropertyString prop = asset.FindByName("broom_straight") as AssetPropertyString;
            string relativePath = getTexturePath(prop.Value, "normal");
            this.Attributes.Assets.Add(OmniPBR.NormalMap, relativePath);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
        }
        else if (finish == 1)
        {
            AssetPropertyString prop = asset.FindByName("broom_curved") as AssetPropertyString;
            string relativePath = getTexturePath(prop.Value, "normal");
            this.Attributes.Assets.Add(OmniPBR.NormalMap, relativePath);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
        } // no 2 no 3??
        else if (finish == 4)
        {
            p = asset.FindByName(Concrete.ConcreteBumpMap);
            a = getConnectedAsset(p);
            if (a != null)
            {
                setNormalMap(a, false, setUVs);
                this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
            }
        }
    }
    private void getPlasticVinylAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        int pvType = getEnumOrIntValue(asset, PlasticVinyl.PlasticvinylType);
        int finish = getEnumOrIntValue(asset, PlasticVinyl.PlasticvinylApplication);
        AssetProperty p = asset.FindByName(PlasticVinyl.PlasticvinylColor);
        Revit.Color color = (p as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        double normalAmount = (asset.FindByName(PlasticVinyl.PlasticvinylBumpAmount) as AssetPropertyDouble).Value;
        p = asset.FindByName(PlasticVinyl.PlasticvinylBumpMap);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, false, setUVs);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
            setUVs = false;
        }

        if (pvType == 1) // transparent
        {
            this.Attributes.Booleans.Add(OmniPBR.OpacityOn, true);
            this.Attributes.Floats.Add(OmniPBR.OpacityAmount, 0.5f);
        }

        if (finish == 1) // glossy
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.1f);
        }
        else if (finish == 2) // matte
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.9f);
        }
    }
    private void getStoneAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        double normalAmount = (asset.FindByName(Stone.StonePatternAmount) as AssetPropertyDouble).Value;
        AssetProperty p = asset.FindByName(Stone.StoneColor);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }

        p = asset.FindByName(Stone.StonePatternMap);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, true, setUVs);
            setUVs = false;
        }
    }
    private void getWallPaintAttributes(Asset asset)
    {
        setOmniPbr();
        Revit.Color color = (asset.FindByName(WallPaint.WallpaintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Revit.Color tint = (asset.FindByName(WallPaint.CommonTintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        bool tintOn = (asset.FindByName(WallPaint.CommonTintToggle) as AssetPropertyBoolean).Value;
        if (tintOn)
        {
            this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoTint, new usd.exporter.revit.GfVec3f(tint.Red / 255f, tint.Green / 255f, tint.Blue / 255f));
        }
        int wpFinish = getEnumOrIntValue(asset, WallPaint.WallpaintFinish);
        if (wpFinish == 0) // flat matte
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.9f);
        }
        else if (wpFinish == 1) // egg shell
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.8f);
        }
        else if (wpFinish == 2) // platinum?
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.7f);
        }
        else if (wpFinish == 3) // pearl
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.5f);
        }
        else if (wpFinish == 4) // semi-gloss
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.3f);
        }
        else if (wpFinish == 5) // gloss
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.1f);
        }
    }
    private void getHardwoodAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        AssetProperty p = asset.FindByName(Hardwood.HardwoodColor);
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        Revit.Color tint = (asset.FindByName(Hardwood.CommonTintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoTint, new usd.exporter.revit.GfVec3f(tint.Red / 255f, tint.Green / 255f, tint.Blue / 255f));
        double normalAmount = (asset.FindByName(Hardwood.HardwoodImperfectionsAmount) as AssetPropertyDouble).Value;

        p = asset.FindByName(Hardwood.HardwoodImperfectionsShader);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, false, setUVs);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
            setUVs = false;
        }
        int hw_finish = getEnumOrIntValue(asset, Hardwood.HardwoodFinish);
        // int hw_application = "hardwood_application";
        if (hw_finish == 0) // glossy varnish
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.2f);
        }
        else if (hw_finish == 1) // semi gloss
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.4f);
        }
        else if (hw_finish == 2) // satin
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.6f);
        }
        else if (hw_finish == 3) // unfininshed
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.8f);
        }
    }
    private void getSolidGlassAttributes(Asset asset)
    {
        setOmniGlass();
        double roughnessAmount = (asset.FindByName(SolidGlass.SolidglassBumpAmount) as AssetPropertyDouble).Value;
        AssetProperty p = asset.FindByName(SolidGlass.SolidglassBumpMap);
        Asset a = getConnectedAsset(p);
        if (asset != null)
        {
            setRoughnessMap(a, true);
            this.Attributes.Floats.Add(OmniGlass.RoughnessAmount, (float)roughnessAmount);
        }
        Revit.Color color = (asset.FindByName(SolidGlass.SolidglassTransmittanceCustomColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniGlass.GlassColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        double ior = (asset.FindByName(SolidGlass.SolidglassRefractionIor) as AssetPropertyDouble).Value;
        this.Attributes.Floats.Add(OmniGlass.Ior, (float)ior);
    }
    private void getGlazingAttributes(Asset asset)
    {
        setOmniGlass();
        double ior = (asset.FindByName(Glazing.GlazingReflectance) as AssetPropertyDouble).Value;
        this.Attributes.Floats.Add(OmniGlass.Ior, (float)ior);
        Revit.Color color = (asset.FindByName(Glazing.GlazingTransmittanceMap) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniGlass.GlassColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
    }
    private void getCeramicAttributes(Asset asset)
    {
        setOmniPbr();
        bool setUVs = true;
        AssetProperty p = asset.FindByName(Ceramic.CeramicColor);
        Revit.Color color = (p as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Asset a = getConnectedAsset(p);
        if (a != null)
        {
            setAlbedoMap(a);
            setUVs = false;
        }
        double normalAmount = (asset.FindByName(Ceramic.CeramicPatternAmount) as AssetPropertyDouble).Value;
        p = asset.FindByName(Ceramic.CeramicPatternMap);
        a = getConnectedAsset(p);
        if (a != null)
        {
            setNormalMap(a, false, setUVs);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
            setUVs = false;
        }

        int ceramicFinish = getEnumOrIntValue(asset, Ceramic.CeramicApplication);
        if (ceramicFinish == 0) // glossy
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.1f);
        }
        else if (ceramicFinish == 1) // satin
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.5f);
        }
        else if (ceramicFinish == 2) // matte
        {
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, 0.9f);
        }
    }
    private void getWaterAttributes(Asset asset)
    {
        setOmniPbr();
        Revit.Color color = (asset.FindByName(Water.WaterTintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        Revit.Color tint = (asset.FindByName(Water.CommonTintColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoTint, new usd.exporter.revit.GfVec3f(tint.Red / 255f, tint.Green / 255f, tint.Blue / 255f));
        double normalAmount = (asset.FindByName(Water.WaterBumpAmount) as AssetPropertyDouble).Value;
        int waterType = getEnumOrIntValue(asset, Water.WaterType);
        if (waterType == 0) // swimming pool
        {
            string relativePath = getTexturePath(@"3\Mats\water_swimmingpool_bump.jpg", "normal");
            this.Attributes.Assets.Add(OmniPBR.NormalMap, relativePath);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
        }
        else if (waterType == 1) // reflecting pool
        {
            string relativePath = getTexturePath(@"3\Mats\water_calm.png", "roughness");
            this.Attributes.Assets.Add(OmniPBR.RoughnessMap, relativePath);
        }
        else if (waterType == 2 || waterType == 3) // 2 = stream / river; 3 = pond / lake
        {
            string roughnessPath = getTexturePath(@"3\Mats\water_seacalm_rough.jpg", "roughness");
            this.Attributes.Assets.Add(OmniPBR.RoughnessMap, roughnessPath);
            string normalPath = getTexturePath(@"3\Mats\water_seacalm_bump.jpg", "normal");
            this.Attributes.Assets.Add(OmniPBR.NormalMap, normalPath);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
        }
        else if (waterType == 4) // sea or ocean
        {
            string roughnessPath = getTexturePath(@"3\Mats\water_seawavy_rough.jpg", "roughness");
            this.Attributes.Assets.Add(OmniPBR.RoughnessMap, roughnessPath);
            string normalPath = getTexturePath(@"3\Mats\water_seawavy_norm.jpg", "normal");
            this.Attributes.Assets.Add(OmniPBR.NormalMap, normalPath);
            this.Attributes.Floats.Add(OmniPBR.NormalAmount, (float)normalAmount);
        }
        this.Attributes.Floats.Add(OmniPBR.OpacityAmount, 0.25f);
        this.Attributes.Booleans.Add(OmniPBR.OpacityOn, true);
    }
    private void getMetallicPaintAttributes(Asset asset)
    {
        setOmniPbr();
        Revit.Color color = (asset.FindByName(MetallicPaint.MetallicpaintBaseColor) as AssetPropertyDoubleArray4d).GetValueAsColor();
        this.Attributes.GfVec3fs.Add(OmniPBR.AlbedoColor, new usd.exporter.revit.GfVec3f(color.Red / 255f, color.Green / 255f, color.Blue / 255f));
        int fleck = getEnumOrIntValue(asset, MetallicPaint.MetallicpaintFlecks);
        if (fleck == 1)
        {
            string relativePath = getTexturePath(@"3\Mats\Finishes.Painting.Paint.FLaking.Bump.jpg", "metallic");
            this.Attributes.Assets.Add(OmniPBR.MetallicMap, relativePath);
        }

        int finish = getEnumOrIntValue(asset, MetallicPaint.MetallicpaintFinish);
        if (finish == 1)
        {
            double roughness = (asset.FindByName(MetallicPaint.MetallicpaintTopcoatGlossy) as AssetPropertyDouble).Value;
            this.Attributes.Floats.Add(OmniPBR.RoughnessAmount, (float)roughness);
        }
    }

    private static int getEnumOrIntValue(Asset a, string prop)
    {
        AssetProperty ap = a.FindByName(prop);
        if (ap == null)
        {
            return -1;
        }
        else
        {
            if (ap is AssetPropertyEnum)
            {
                return ((AssetPropertyEnum)ap).Value;
            }
            else if (ap is AssetPropertyInteger)
            {
                return ((AssetPropertyInteger)ap).Value;
            }
            else
            {
                {
                    return -1;
                }
            }
        }
    }

    private static usd.exporter.revit.GfVec3f getColorFromTemp(double temperature)
    {
        float red = 0.0f;
        float green = 0.0f;
        float blue = 0.0f;
        double temp = temperature / 100;
        // calc red
        if (temp <= 66)
        {
            red = 1.0f;
        }
        else
        {
            double test = temp - 60;
            test = 329.698727446 * (Math.Pow(test, -0.1332047592));
            if (test < 0)
            {
                red = 0.0f;
            }
            else if (test > 255)
            {
                red = 1.0f;
            }
            else
            {
                red = (float)test / 255.0f;
            }
        }
        // calc green
        if (temp <= 66)
        {
            double test = temp;
            test = 99.4708025861 * Math.Log(test) - 161.1195681611;
            if (test < 0)
            {
                green = 0.0f;
            }
            else if (test >= 255)
            {
                green = 1.0f;
            }
            else
            {
                green = (float)test / 255.0f;
            }
        }
        else
        {
            double test = temp - 60;
            test = 288.1221695283 * (Math.Pow(test, -0.0755148492));
            if (test < 0)
            {
                green = 0.0f;
            }
            else if (test > 255)
            {
                green = 1.0f;
            }
            else
            {
                green = (float)test / 255.0f;
            }
        }
        // calc blue
        if (temp >= 66)
        {
            blue = 1.0f;
        }
        else
        {
            if (temp <= 19)
            {
                blue = 0;
            }
            else
            {
                double test = temp - 10;
                test = 138.5177312231 * Math.Log(test) - 305.0447927307;
                if (test < 0)
                {
                    blue = 0.0f;
                }
                else if (test > 255)
                {
                    blue = 1.0f;
                }
                else
                {
                    blue = (float)test / 255.0f;
                }
            }
        }
        return new usd.exporter.revit.GfVec3f(red, green, blue);
    }
}

internal class MaterialAttributes
{
    public Dictionary<string, bool> Booleans = new Dictionary<string, bool>();
    public Dictionary<string, int> Integers = new Dictionary<string, int>();
    public Dictionary<string, float> Floats = new Dictionary<string, float>();
    public Dictionary<string, usd.exporter.revit.GfVec2f> GfVec2fs = new Dictionary<string, usd.exporter.revit.GfVec2f>();
    public Dictionary<string, usd.exporter.revit.GfVec3f> GfVec3fs = new Dictionary<string, usd.exporter.revit.GfVec3f>();
    public Dictionary<string, string> Assets = new Dictionary<string, string>();

    private List<string> alreadyWritten = new List<string>() { OmniGlass.GlassColor, OmniGlass.Ior, OmniGlass.RoughnessAmount, OmniPBR.AlbedoColor, OmniPBR.RoughnessAmount, OmniPBR.OpacityAmount, OmniPBR.OpacityOn, OmniPBR.MetallicAmount };

    // Emissive color/intensity are authored via addEmissiveColorToPbrMaterial (MDL + Preview Surface), not as raw MDL-only inputs.
    private static readonly List<string> emissiveKeys = new List<string>() { OmniPBR.EmissiveOn, OmniPBR.EmissiveColor, OmniPBR.EmissiveIntensity };

    public void Write(long stageId, string materialPath)
    {
        bool emissiveWritten = writeEmissive(stageId, materialPath);
        foreach (var kvp in Booleans)
        {
            if (alreadyWritten.Contains(kvp.Key) || (emissiveWritten && emissiveKeys.Contains(kvp.Key)))
            {
                continue;
            }
            usd.exporter.revit.core.createMdlShaderInputBool(stageId, materialPath, kvp.Key, kvp.Value);
        }
        foreach (var kvp in Integers)
        {
            usd.exporter.revit.core.createMdlShaderInputInt(stageId, materialPath, kvp.Key, kvp.Value);
        }
        foreach (var kvp in Floats)
        {
            if (alreadyWritten.Contains(kvp.Key) || (emissiveWritten && emissiveKeys.Contains(kvp.Key)))
            {
                continue;
            }
            usd.exporter.revit.core.createMdlShaderInputFloat(stageId, materialPath, kvp.Key, kvp.Value);
        }
        foreach (var kvp in GfVec2fs)
        {
            usd.exporter.revit.core.createMdlShaderInputFloat2(stageId, materialPath, kvp.Key, kvp.Value);
        }
        foreach (var kvp in GfVec3fs)
        {
            if (alreadyWritten.Contains(kvp.Key) || (emissiveWritten && emissiveKeys.Contains(kvp.Key)))
            {
                continue;
            }
            usd.exporter.revit.core.createMdlShaderInputColor3f(stageId, materialPath, kvp.Key, usd.exporter.revit.core.sRgbToLinear(kvp.Value));
        }
        foreach (var kvp in Assets)
        {
            switch (kvp.Key)
            {
                case OmniPBR.AlbedoMap:
                    usd.exporter.revit.core.addDiffuseTextureToPbrMaterial(stageId, materialPath, kvp.Value);
                    break;
                case OmniPBR.NormalMap:
                    usd.exporter.revit.core.addNormalTextureToPbrMaterial(stageId, materialPath, kvp.Value);
                    break;
                case OmniPBR.OpacityMap:
                    usd.exporter.revit.core.addOpacityTextureToPbrMaterial(stageId, materialPath, kvp.Value);
                    break;
                case OmniPBR.RoughnessMap:
                    usd.exporter.revit.core.addRoughnessTextureToPbrMaterial(stageId, materialPath, kvp.Value);
                    break;
                case OmniPBR.MetallicMap:
                    usd.exporter.revit.core.addMetallicTextureToPbrMaterial(stageId, materialPath, kvp.Value);
                    break;
                case OmniPBR.OrmMap:
                    usd.exporter.revit.core.addOrmTextureToPbrMaterial(stageId, materialPath, kvp.Value);
                    break;
                case OmniGlass.GlassMap:
                    usd.exporter.revit.core.createMdlShaderInputAsset(stageId, materialPath, kvp.Key, kvp.Value, usd.exporter.revit.ColorSpace.eSrgb);
                    break;
                default:
                    usd.exporter.revit.core.createMdlShaderInputAsset(stageId, materialPath, kvp.Key, kvp.Value, usd.exporter.revit.ColorSpace.eRaw);
                    break;
            }
        }
    }

    // Authors the emissive color to both the OmniPBR MDL shader and the UsdPreviewSurface shader so self-illuminated
    // materials render in non-RTX/MDL viewers while keeping the same OmniPBR inputs for the RTX path.
    private bool writeEmissive(long stageId, string materialPath)
    {
        if (!Booleans.TryGetValue(OmniPBR.EmissiveOn, out bool emissiveOn) || !emissiveOn)
        {
            return false;
        }
        usd.exporter.revit.GfVec3f color = GfVec3fs.TryGetValue(OmniPBR.EmissiveColor, out usd.exporter.revit.GfVec3f emissiveColor) ? emissiveColor : new usd.exporter.revit.GfVec3f(1.0f, 1.0f, 1.0f);
        float intensity = Floats.TryGetValue(OmniPBR.EmissiveIntensity, out float emissiveIntensity) ? emissiveIntensity : 0.0f;
        return usd.exporter.revit.core.addEmissiveColorToPbrMaterial(stageId, materialPath, usd.exporter.revit.core.sRgbToLinear(color), intensity);
    }
}

internal static class OmniPBR
{
    public const string Path = "OmniPBR.mdl";
    public const string Module = "OmniPBR";
    public const string AlbedoColor = "diffuse_color_constant";
    public const string AlbedoMap = "diffuse_texture";
    public const string AlbedoDesaturation = "albedo_desaturation";
    public const string AlbedoAdd = "albedo_add";
    public const string AlbedoBrightness = "albedo_brightness";
    public const string AlbedoTint = "diffuse_tint";
    public const string AoAmount = "ao_to_diffuse";
    public const string AoMap = "ao_texture";
    public const string EmissiveOn = "enable_emission";
    public const string EmissiveColor = "emissive_color";
    public const string EmissiveMaskMap = "emissive_mask_texture";
    public const string EmissiveIntensity = "emissive_intensity";
    public const string MetallicAmount = "metallic_constant";
    public const string MetallicMapInfluence = "metallic_texture_influence";
    public const string MetallicMap = "metallic_texture";
    public const string SpecularAmount = "specular_level";
    public const string OrmOn = "enable_ORM_texture";
    public const string OrmMap = "ORM_texture";
    public const string RoughnessAmount = "reflection_roughness_constant";
    public const string RoughnessMapInfluence = "reflection_roughness_texture_influence";
    public const string RoughnessMap = "reflectionroughness_texture";
    public const string NormalAmount = "bump_factor";
    public const string NormalMap = "normalmap_texture";
    public const string DetailNormalAmount = "detail_bump_factor";
    public const string DetailNormalMap = "detail_normalmap_texture";
    public const string OpacityOn = "enable_opacity";
    public const string OpacityTextureOn = "enable_opacity_texture";
    public const string OpacityAmount = "opacity_constant";
    public const string OpacityMap = "opacity_texture";
    public const string OpacityThreshold = "opacity_threshold";
    public const string UvwCoorindatesOn = "project_uvw";
    public const string WorldCoordinatesOn = "world_or_object";
    public const string UvIndex = "uv_space_index";
    public const string TextureTranslate = "texture_translate";
    public const string TextureScale = "texture_scale";
    public const string TextureRotation = "texture_rotate";
    public const string DetailTextureTranslate = "detail_texture_translate";
    public const string DetailTextureScale = "detail_texture_scale";
    public const string DetailTextureRotate = "detail_texture_rotate";

    public const float DefaultOpacity = 1.0f;
    public const float DefaultRoughness = 0.5f;
    public const float DefaultMetallic = 0.0f;
}

internal static class OmniGlass
{
    public const string Path = "OmniGlass.mdl";
    public const string Module = "OmniGlass";
    public const string GlassColor = "glass_color";
    public const string GlassMap = "glass_color_texture";
    public const string Absorbtion = "depth";
    public const string RoughnessAmount = "frosting_roughness";
    public const string RoughnessMapInfluence = "roughness_texture_influence";
    public const string RoughnessMap = "roughness_texture";
    public const string Ior = "glass_ior";
    public const string OpacityAmount = "cutout_opacity";
    public const string ThinWalled = "thin_walled";
    public const string ReflectionMap = "reflection_color_texture";
    public const string ReflectionColor = "reflection_color";
    public const string UvwCoorindatesOn = "project_uvw";
    public const string WorldCoordinatesOn = "world_or_object";
    public const string UvIndex = "uv_space_index";
    public const string TextureTranslate = "texture_translate";
    public const string TextureScale = "texture_scale";
    public const string TextureRotation = "texture_rotate";

    public const float DefaultIor = 1.491f;
    public const float DefaultRoughness = 0.02f;
}
}

// SPDX-FileCopyrightText: Copyright (c) 2024-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using RevitUsdExportSdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RevitUsdExportPlugin.UI
{
/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class Settings : Window
{
    public readonly string _nvs3_mats = @"https://omniverse-content-production.s3-us-west-2.amazonaws.com/Materials";
    public readonly string _nvs3_assets = @"https://omniverse-content-production.s3-us-west-2.amazonaws.com/Assets";

    private RevitUsdExportSettings settings;
    private SettingsContext context;

    // used to block events where needed
    private bool populating = false;
    // indidcates the dialog was canceled
    private bool cleanclose = false;

    private List<FamilyData> familyData;
    private List<MaterialData> materialData;
    private Dictionary<string, List<MdlFile>> mdlFiles = new Dictionary<string, List<MdlFile>>();
    private Dictionary<string, List<UsdFile>> usdFiles = new Dictionary<string, List<UsdFile>>();
    private List<MdlFile> nvMdlCache = new List<MdlFile>();
    private List<UsdFile> nvUsdCache = new List<UsdFile>();

    public SettingsDialogResult Result = new SettingsDialogResult();

    // Index of the Mapping-Library comboBox.
    private int combo_root_previousSelectedIndex = 0;

    // Copy the active selections and mapped items into settings before serialization.
    private void CopyMappingUIToSettings()
    {
        CopyActiveMappingSelectionToSettings();

        // Update material mappings from materialData list
        List<UserMaterialMapping> maps = new List<UserMaterialMapping>();
        foreach (MaterialData mat in materialData)
        {
            if (mat.Mapped)
            {
                UserMaterialMapping map = new UserMaterialMapping();
                map.Id = mat.Id;
                map.Name = mat.Name;
                map.MdlPath = mat.MdlPath;
                map.MdlModule = mat.MdlModule;
                maps.Add(map);
            }
        }
        settings.Mappings.Materials.UserMapped = maps;

        // Update family type mappings from familyData list
        List<UserFamilyTypeMapping> families = new List<UserFamilyTypeMapping>();
        foreach (FamilyData fam in familyData)
        {
            if (fam.Mapped)
            {
                UserFamilyTypeMapping family = new UserFamilyTypeMapping();
                family.Id = fam.Id;
                family.AssetPath = fam.AssetPath;
                family.FamilyName = fam.FamilyName;
                family.TypeName = fam.TypeName;
                families.Add(family);
            }
        }
        settings.Mappings.FamilyTypes.UserMapped = families;
    }

    // Copy visible selections without changing the saved state for the inactive mapping mode.
    private void CopyActiveMappingSelectionToSettings()
    {
        bool isMaterialsMode = radio_materials.IsChecked == true;
        settings.Mappings.IsMaterialsMode = isMaterialsMode;

        MappingUIState currentState = isMaterialsMode
            ? settings.Mappings.MaterialsUIState
            : settings.Mappings.FamiliesUIState;

        currentState.SelectedLibrary = combo_root.SelectedItem as string ?? string.Empty;
        currentState.SelectedGroup = combo_bucket.SelectedItem as string ?? string.Empty;
        currentState.SelectedFile = combo_file.SelectedItem as string ?? string.Empty;

        if (isMaterialsMode)
        {
            currentState.SelectedModule = combo_module.SelectedItem as string ?? string.Empty;
        }
        else
        {
            currentState.SelectedCategory = combo_category.SelectedItem as string ?? string.Empty;
        }
    }

    private void RestoreMappingSelection(MappingUIState currentState)
    {
        bool wasPopulating = populating;
        populating = true;

        try
        {
            RestoreComboBoxSelection(combo_bucket, currentState.SelectedGroup);

            string root = combo_root.SelectedItem as string;
            string group = combo_bucket.SelectedItem as string;
            combo_file.Items.Clear();
            combo_module.Items.Clear();

            if (radio_materials.IsChecked == true)
            {
                List<MdlFile> files;
                if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(group) && mdlFiles.TryGetValue(root, out files))
                {
                    List<MdlFile> groupFiles = files.Where(f => f.Bucket == group).OrderBy(f => f.Name).ToList();
                    foreach (MdlFile file in groupFiles)
                    {
                        combo_file.Items.Add(file.Name);
                    }

                    RestoreComboBoxSelection(combo_file, currentState.SelectedFile);

                    string fileName = combo_file.SelectedItem as string;
                    MdlFile selectedFile = groupFiles.FirstOrDefault(f => f.Name == fileName);
                    if (selectedFile != null)
                    {
                        foreach (string module in selectedFile.GetModules().OrderBy(m => m))
                        {
                            combo_module.Items.Add(module);
                        }
                    }
                }

                RestoreComboBoxSelection(combo_module, currentState.SelectedModule);
                combo_module.IsEnabled = combo_module.Items.Count > 0;
            }
            else
            {
                List<UsdFile> files;
                if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(group) && usdFiles.TryGetValue(root, out files))
                {
                    foreach (UsdFile file in files.Where(f => f.Bucket == group).OrderBy(f => f.Name))
                    {
                        combo_file.Items.Add(file.Name);
                    }
                }

                RestoreComboBoxSelection(combo_file, currentState.SelectedFile);
                combo_module.IsEnabled = false;
                RestoreComboBoxSelection(combo_category, currentState.SelectedCategory);
                populateMappingSelection();
            }
        }
        finally
        {
            populating = wasPopulating;
        }
    }

    private void RestoreComboBoxSelection(ComboBox comboBox, string selectedValue)
    {
        if (!string.IsNullOrEmpty(selectedValue) && comboBox.Items.Contains(selectedValue))
        {
            comboBox.SelectedItem = selectedValue;
        }
        else if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private void SyncMappingsToUIData()
    {
        // Sync material mappings from settings to materialData list
        foreach (MaterialData mat in materialData)
        {
            // Reset mapping state first
            mat.Mapped = false;
            mat.MdlPath = string.Empty;
            mat.MdlModule = string.Empty;

            // Find matching mapping in settings
            UserMaterialMapping mapping = settings.Mappings.Materials.UserMapped.FirstOrDefault(m => m.Id == mat.Id);
            if (mapping != null)
            {
                mat.Mapped = true;
                mat.MdlPath = mapping.MdlPath;
                mat.MdlModule = mapping.MdlModule;
            }
        }

        // Sync family type mappings from settings to familyData list
        foreach (FamilyData fam in familyData)
        {
            // Reset mapping state first
            fam.Mapped = false;
            fam.AssetPath = string.Empty;

            // Find matching mapping in settings
            UserFamilyTypeMapping mapping = settings.Mappings.FamilyTypes.UserMapped.FirstOrDefault(m => m.Id == fam.Id);
            if (mapping != null)
            {
                fam.Mapped = true;
                fam.AssetPath = mapping.AssetPath;
            }
        }
    }

    public string GetSettings()
    {
        CopyMappingUIToSettings();
        return Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
    }
    public Settings(
        SettingsContext context,
        string settings,
        bool oneClickEnabled,
        List<string> roomSchemes,
        List<string> spaceSchemes,
        List<string> publishSets,
        List<string> phaseFilters,
        List<string> viewTemplates,
        Dictionary<string, bool> views,
        List<MaterialData> materials,
        List<FamilyData> families
    )
    {
        populating = true;
        InitializeComponent();
        this.Closing += Settings_Closing;

        this.context = context;
        this.settings = Newtonsoft.Json.JsonConvert.DeserializeObject<RevitUsdExportSettings>(settings);

        if (!this.settings.Mappings.Libraries.MaterialFolders.Contains(this.settings.Mappings.Materials.DefaultLibraryUri))
        {
            this.settings.Mappings.Libraries.MaterialFolders.Add(this.settings.Mappings.Materials.DefaultLibraryUri);
        }
        if (!this.settings.Mappings.Libraries.AssetFolders.Contains(this.settings.Mappings.FamilyTypes.DefaultLibraryUri))
        {
            this.settings.Mappings.Libraries.AssetFolders.Add(this.settings.Mappings.FamilyTypes.DefaultLibraryUri);
        }

        foreach (UnitType unitType in Enum.GetValues(typeof(UnitType)))
        {
            string name = Enum.GetName(typeof(UnitType), unitType);
            combo_units.Items.Add(name);
        }
        combo_units.SelectedIndex = 0;

        materialData = materials;
        familyData = families;
        cb_use1click.IsChecked = oneClickEnabled;
        Result.OneClick = oneClickEnabled;

        foreach (string roomScheme in roomSchemes)
        {
            combo_rooms.Items.Add(roomScheme);
        }
        if (roomSchemes.Count > 0)
        {
            combo_rooms.SelectedIndex = 0;
        }

        foreach (string spaceScheme in spaceSchemes)
        {
            combo_spaces.Items.Add(spaceScheme);
        }
        if (spaceSchemes.Count > 0)
        {
            combo_spaces.SelectedIndex = 0;
        }

        foreach (string publishSet in publishSets)
        {
            combo_drawings.Items.Add(publishSet);
        }
        if (publishSets.Count > 0)
        {
            combo_drawings.SelectedIndex = 0;
        }

        combo_phaseFilter.Items.Add("NOT USED");
        foreach (string phaseFilter in phaseFilters)
        {
            combo_phaseFilter.Items.Add(phaseFilter);
        }
        combo_viewTemplate.Items.Add("NOT USED");
        foreach (string viewTemplate in viewTemplates)
        {
            combo_viewTemplate.Items.Add(viewTemplate);
        }
        foreach (KeyValuePair<string, bool> view in views)
        {
            CheckBox view_cb = new CheckBox();
            view_cb.Checked += View_cb_Checked;
            view_cb.Unchecked += View_cb_Unchecked;
            view_cb.Content = view.Key;
            view_cb.IsChecked = view.Value;
            if (view.Value && !this.settings.ViewsToExport.Contains(view.Key))
            {
                this.settings.ViewsToExport.Add(view.Key);
            }
            sp_view.Children.Add(view_cb);
        }

        combo_extension.Items.Add(".usdc");
        combo_extension.Items.Add(".usda");
        combo_extension.Items.Add(".usd");

        combo_instancing.Items.Add("Internal Classes");
        combo_instancing.Items.Add("External Assets");

        combo_coordinates.Items.Add("Internal Origin");
        combo_coordinates.Items.Add("Project Base Point");
        combo_coordinates.Items.Add("Survey Point");
        combo_coordinates.Items.Add("Shared Coordinates");

        combo_detailLevel.Items.Add("NOT USED");
        combo_detailLevel.Items.Add("Fine");
        combo_detailLevel.Items.Add("Medium");
        combo_detailLevel.Items.Add("Coarse");

        populateUI(); // this function also sets populating value so we need to turn it back on
        populating = true;
        initializeContext();
        populating = false;
    }

    private void Settings_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!cleanclose)
        {
            Result.Canceled = true;
        }
    }

    private void initializeContext()
    {
        switch (context)
        {
            case SettingsContext.RibbonClick:
                b_1.Content = "Save";
                b_1.ToolTip = "Save the settings...";
                b_1.Click += b_save_Click;
                b_2.Content = "Import Json";
                b_2.ToolTip = "Select a settings json to import into this session";
                b_2.Click += b_import_Click;
                b_3.Content = "Export to Json";
                b_3.ToolTip = "Save the settings file to an external json for use by another user or batch process";
                b_3.Click += b_exportJson_Click;
                break;
            case SettingsContext.FileExport:
                b_1.Content = "Start Export";
                b_1.ToolTip = "Begin export to usd with current settings";
                b_1.Click += b_startExport_Click;
                b_2.Visibility = Visibility.Hidden;
                b_3.Content = "Cancel";
                b_3.ToolTip = "Cancel the export and return to Revit";
                b_3.Click += b_cancel_Click;
                break;
            case SettingsContext.BatchExport:
                tab_control.SelectedIndex = 4;
                b_1.Content = "Start Export";
                b_1.ToolTip = "Begin batch export to usd with current settings";
                b_1.Click += b_startExport_Click;
                b_2.Content = "Import Json";
                b_2.ToolTip = "Select a batch settings json to use for export";
                b_2.Click += b_import_Click;
                b_3.Content = "Cancel";
                b_3.ToolTip = "Cancel the batch export and return to Revit";
                b_3.Click += b_cancel_Click;
                break;
        }
    }

    private static void setToolTip(TextBlock item)
    {
        if (!string.IsNullOrEmpty(item.Text))
        {
            item.ToolTip = item.Text;
        }
        else
        {
            item.ToolTip = null;
        }
    }
    private static void setToolTip(ComboBox item)
    {
        if (!string.IsNullOrEmpty((string)item.SelectedItem))
        {
            item.ToolTip = (string)item.SelectedItem;
        }
        else
        {
            item.ToolTip = null;
        }
    }

    private void populateUI()
    {
        populating = true;

        // output tab
        tb_fileName.Text = settings.File.FileName;
        foreach (string item in combo_extension.Items)
        {
            if (item == settings.File.Extension)
            {
                combo_extension.SelectedItem = item;
            }
        }
        text_folderPath.Text = string.IsNullOrEmpty(settings.File.OutputFolder) ? "..." : settings.File.OutputFolder;
        setToolTip(text_folderPath);

        radio_internalMats.IsChecked = settings.Options.MaterialStyle == MaterialStyle.InternalLibrary;
        radio_externalMats.IsChecked = !radio_internalMats.IsChecked;

        tb_materialPrim.Text = settings.Options.MaterialFolderName;
        radio_references.IsChecked = settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsReference;
        radio_payloads.IsChecked = !radio_references.IsChecked.Value;

        foreach (string item in combo_units.Items)
        {
            if (item == Enum.GetName(typeof(UnitType), settings.Options.UnitType))
            {
                combo_units.SelectedItem = item;
            }
        }

        cb_override.IsChecked = !string.IsNullOrEmpty(settings.OverrideJsonPath);
        text_overridePath.Text = settings.OverrideJsonPath;
        setToolTip(text_overridePath);

        // options tab
        cb_lights.IsChecked = settings.Options.IncludeLights;
        cb_links.IsChecked = settings.Options.IncludeLinks;
        cb_cameras.IsChecked = settings.Options.IncludeCameras;
        cb_bim.IsChecked = settings.Options.IncludeBimData;
        cb_rooms.IsChecked = settings.Options.IncludeRooms;
        foreach (string item in combo_rooms.Items)
        {
            if (item == settings.Options.RoomColorScheme)
            {
                combo_rooms.SelectedItem = item;
            }
        }
        cb_spaces.IsChecked = settings.Options.IncludeSpaces;
        foreach (string item in combo_spaces.Items)
        {
            if (item == settings.Options.SpaceColorScheme)
            {
                combo_spaces.SelectedItem = item;
            }
        }
        cb_drawings.IsChecked = settings.Options.IncludeDrawings;
        foreach (string item in combo_drawings.Items)
        {
            if (item == settings.Options.DrawingPublishSet)
            {
                combo_drawings.SelectedItem = item;
            }
        }
        cb_instance.IsChecked = settings.Options.InstanceFamilies;
        if (settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.InternalClasses)
        {
            combo_instancing.SelectedIndex = 0;
        }
        else
        {
            combo_instancing.SelectedIndex = 1;
        }
        combo_coordinates.SelectedIndex = settings.Options.CoordinateSystem;

        // view tab
        // Update view checkboxes based on settings.ViewsToExport
        foreach (var child in sp_view.Children)
        {
            if (child is CheckBox viewCheckBox)
            {
                string viewName = viewCheckBox.Content as string;
                if (!string.IsNullOrEmpty(viewName))
                {
                    viewCheckBox.IsChecked = settings.ViewsToExport.Contains(viewName);
                }
            }
        }

        if (!settings.AnyViewManipulations())
        {
            combo_phaseFilter.SelectedIndex = 0;
            combo_detailLevel.SelectedIndex = 0;
            combo_viewTemplate.SelectedIndex = 0;
        }
        else
        {
            if (!string.Equals(settings.View.DetailLevel, "not used", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string item in combo_detailLevel.Items)
                {
                    if (string.Equals(item, settings.View.DetailLevel, StringComparison.OrdinalIgnoreCase))
                    {
                        combo_detailLevel.SelectedItem = item;
                    }
                }
            }
            if (!string.Equals(settings.View.PhaseFilter, "not used", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string item in combo_phaseFilter.Items)
                {
                    if (string.Equals(item, settings.View.PhaseFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        combo_phaseFilter.SelectedItem = item;
                    }
                }
            }
            if (!string.Equals(settings.View.ViewTemplate, "not used", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string item in combo_viewTemplate.Items)
                {
                    if (string.Equals(item, settings.View.ViewTemplate, StringComparison.OrdinalIgnoreCase))
                    {
                        combo_viewTemplate.SelectedItem = item;
                    }
                }
            }
        }
        // mappings tab
        getMdlCache();
        getUsdCache();

        // Restore radio button state and library
        if (settings.Mappings.IsMaterialsMode)
        {
            radio_materials.IsChecked = true;
            string libToUse = !string.IsNullOrEmpty(settings.Mappings.MaterialsUIState.SelectedLibrary) ? settings.Mappings.MaterialsUIState.SelectedLibrary : settings.Mappings.Materials.DefaultLibraryUri;
            populateMappingLibrary(libToUse);
        }
        else
        {
            radio_families.IsChecked = true;
            string libToUse = !string.IsNullOrEmpty(settings.Mappings.FamiliesUIState.SelectedLibrary) ? settings.Mappings.FamiliesUIState.SelectedLibrary : settings.Mappings.FamilyTypes.DefaultLibraryUri;
            populateMappingLibrary(libToUse);
        }

        populating = true;

        populateMappingSelection();

        MappingUIState currentState = settings.Mappings.IsMaterialsMode ? settings.Mappings.MaterialsUIState : settings.Mappings.FamiliesUIState;
        RestoreMappingSelection(currentState);
        CopyActiveMappingSelectionToSettings();
        populating = true;

        // batch tab
        text_batchInput.Text = string.IsNullOrEmpty(settings.Batch.InputFolder) ? "..." : settings.Batch.InputFolder;
        text_batchOutput.Text = string.IsNullOrEmpty(settings.Batch.OutputFolder) ? "..." : settings.Batch.OutputFolder;
        setToolTip(text_batchInput);
        setToolTip(text_batchOutput);
        text_folderDepth.Text = settings.Batch.FolderDepth.ToString();
        slider_folderDepth.Value = settings.Batch.FolderDepth;
        tb_batchViewString.Text = settings.Batch.ViewToFind;

        populating = false;
    }

    private void populateMappingSelection()
    {
        populating = true;
        string selectedCategory = combo_category.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedCategory) && radio_families.IsChecked == true)
        {
            selectedCategory = settings.Mappings.FamiliesUIState.SelectedCategory;
        }

        combo_category.Items.Clear();
        lb_mappings.Items.Clear();
        if (radio_materials.IsChecked == true)
        {
            combo_category.IsEnabled = false;
            foreach (MaterialData mat in materialData)
            {
                if (string.IsNullOrEmpty(tb_search.Text) || mat.Name.ToLower().Contains(tb_search.Text.ToLower())) // NOSONAR
                {
                    TextBlock tb = new TextBlock();
                    tb.MouseWheel += lb_mappings_MouseWheel;
                    tb.Name = "text_" + mat.Id.ToString();
                    if (mat.Mapped)
                    {
                        tb.Text = mat.Name + " | " + mat.MdlModule;
                        tb.Background = new SolidColorBrush(Colors.LightGray);
                        tb.ToolTip = mat.MdlPath;
                    }
                    else
                    {
                        tb.Background = new SolidColorBrush(Colors.White);
                        tb.Text = mat.Name;
                    }
                    lb_mappings.Items.Add(tb);
                }
            }
        }
        else
        {
            combo_category.IsEnabled = true;
            List<string> categories = familyData.Select(f => f.Category).Distinct().OrderBy(f => f).ToList();
            foreach (string c in categories)
            {
                combo_category.Items.Add(c);
            }

            if (!string.IsNullOrEmpty(selectedCategory) && categories.Contains(selectedCategory))
            {
                combo_category.SelectedItem = selectedCategory;
            }
            else if (categories.Count > 0)
            {
                combo_category.SelectedIndex = 0;
            }

            string category = combo_category.SelectedItem as string;

            foreach (FamilyData fam in familyData)
            {
                string fullname = fam.FamilyName + " - " + fam.TypeName;
                if (fam.Category == category && (string.IsNullOrEmpty(tb_search.Text) || fullname.ToLower().Contains(tb_search.Text.ToLower()))) // NOSONAR
                {
                    TextBlock tb = new TextBlock();
                    tb.MouseWheel += lb_mappings_MouseWheel;
                    tb.Name = "text_" + fam.Id.ToString();
                    if (fam.Mapped)
                    {
                        tb.Text = string.Concat(fullname, " | ", fam.AssetPath.Substring(fam.AssetPath.LastIndexOf('/') + 1)); // NOSONAR
                        tb.Background = new SolidColorBrush(Colors.LightGray);
                        tb.ToolTip = fam.AssetPath;
                    }
                    else
                    {
                        tb.Background = new SolidColorBrush(Colors.White);
                        tb.Text = fullname;
                    }
                    lb_mappings.Items.Add(tb);
                }
            }
        }
        populating = false;
    }

    private void getMdlCache()
    {
        FileInfo assembly = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string jsonPath = System.IO.Path.Combine(assembly.Directory.FullName, @"assets\nv_mdl.json");
        if (File.Exists(jsonPath))
        {
            string jsonString = File.ReadAllText(jsonPath);
            nvMdlCache = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MdlFile>>(jsonString);
        }
    }
    private void getUsdCache()
    {
        FileInfo assembly = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string jsonPath = System.IO.Path.Combine(assembly.Directory.FullName, @"assets\nv_assets.json");
        if (File.Exists(jsonPath))
        {
            string jsonString = File.ReadAllText(jsonPath);
            nvUsdCache = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UsdFile>>(jsonString);
        }
    }
    private void populateMappingLibrary(string uri)
    {
        populating = true;
        if (radio_materials.IsChecked == true)
        {
            combo_root.Items.Clear();
            int index = 0;
            for (int i = 0; i < settings.Mappings.Libraries.MaterialFolders.Count; i++)
            {
                if (settings.Mappings.Libraries.MaterialFolders[i] == uri)
                {
                    index = i;
                }
                combo_root.Items.Add(settings.Mappings.Libraries.MaterialFolders[i]);
            }
            combo_root.Items.Add("Add a Library");
            combo_root.SelectedIndex = index;
            List<MdlFile> files;
            if (!mdlFiles.TryGetValue(uri, out files) && (uri.Contains("NVIDIA/Materials") || uri.Contains(_nvs3_mats)))
            {
                files = nvMdlCache;
                mdlFiles[uri] = files;
            }
            else
            {
                // Only local folders can be walked; remote paths fall back to the NVIDIA cache
                files = IsLocalFolder(uri) ? getMdlFiles(uri) : nvMdlCache;
                mdlFiles[uri] = files;
            }

            combo_bucket.Items.Clear();
            List<string> buckets = files.Select(m => m.Bucket).Distinct().OrderBy(m => m).ToList();
            foreach (string bucket in buckets)
            {
                combo_bucket.Items.Add(bucket);
            }

            combo_module.Items.Clear();
            if (buckets.Count > 0)
            {
                combo_bucket.SelectedIndex = 0;

                combo_file.Items.Clear();
                List<string> bucketFiles = files.Where(m => m.Bucket == buckets[0]).Select(m => m.Name).OrderBy(m => m).ToList();
                foreach (string file in bucketFiles)
                {
                    combo_file.Items.Add(file);
                }

                if (bucketFiles.Count > 0)
                {
                    combo_file.SelectedIndex = 0;

                    combo_module.IsEnabled = true;
                    MdlFile selectedMdl = files.Where(m => m.Bucket == buckets[0] && m.Name == bucketFiles[0]).FirstOrDefault();
                    if (selectedMdl != null)
                    {
                        List<string> modules = selectedMdl.GetModules().OrderBy(m => m).ToList();
                        foreach (string module in modules)
                        {
                            combo_module.Items.Add(module);
                        }
                        if (modules.Count > 0)
                        {
                            combo_module.SelectedIndex = 0;
                        }
                    }
                }
                else
                {
                    combo_module.IsEnabled = false;
                }
            }
            else
            {
                combo_file.Items.Clear();
                combo_module.IsEnabled = false;
            }
        }
        else
        {
            combo_root.Items.Clear();
            int index = 0;
            for (int i = 0; i < settings.Mappings.Libraries.AssetFolders.Count; i++)
            {
                if (settings.Mappings.Libraries.AssetFolders[i] == uri)
                {
                    index = i;
                }
                combo_root.Items.Add(settings.Mappings.Libraries.AssetFolders[i]);
            }
            combo_root.Items.Add("Add a Library");
            combo_root.SelectedIndex = index;
            List<UsdFile> files;
            if (!usdFiles.TryGetValue(uri, out files) && (uri.Contains("NVIDIA/Assets") || uri.Contains(_nvs3_assets)))
            {
                files = nvUsdCache;
                usdFiles[uri] = files;
            }
            else
            {
                // Only local folders can be walked; remote paths fall back to the NVIDIA cache
                files = IsLocalFolder(uri) ? getUsdFiles(uri) : nvUsdCache;
                usdFiles[uri] = files;
            }
            combo_bucket.Items.Clear();
            List<string> buckets = files.Select(m => m.Bucket).Distinct().OrderBy(m => m).ToList();
            foreach (string bucket in buckets)
            {
                combo_bucket.Items.Add(bucket);
            }

            if (buckets.Count > 0)
            {
                combo_bucket.SelectedIndex = 0;

                combo_file.Items.Clear();
                List<string> bucketFiles = files.Where(m => m.Bucket == buckets[0]).Select(m => m.Name).OrderBy(m => m).ToList();
                foreach (string file in bucketFiles)
                {
                    combo_file.Items.Add(file);
                }

                if (bucketFiles.Count > 0)
                {
                    combo_file.SelectedIndex = 0;
                }
            }

            combo_module.Items.Clear();
            combo_module.IsEnabled = false;
        }
        populating = false;

        setToolTip(combo_root);
        setToolTip(combo_bucket);
        setToolTip(combo_file);
        setToolTip(combo_module);
    }

    private bool IsLocalFolder(string uri)
    {
        // First check if it's clearly a remote URI
        if (uri.StartsWith("omniverse:/", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Try to check if the folder exists on the local file system
        try
        {
            // Convert URI-style path to Windows path
            string localPath = uri.Replace('/', '\\').TrimEnd('\\');
            return Directory.Exists(localPath);
        }
        catch
        {
            // If we can't check, assume it's not a local folder
            return false;
        }
    }

    private bool HasUnsupportedCloudPaths(RevitUsdExportSettings importSettings)
    {
        List<string> importedPaths = GetImportedPathSettings(importSettings);
        for (int i = 0; i < importedPaths.Count; i++)
        {
            if (IsUnsupportedCloudPath(importedPaths[i]))
            {
                return true;
            }
        }

        return false;
    }

    private List<string> GetImportedPathSettings(RevitUsdExportSettings importSettings)
    {
        var paths = new List<string>
        {
            importSettings.File.OutputFolder,
            importSettings.Batch.OutputFolder,
            importSettings.Mappings.Materials.DefaultLibraryUri,
            importSettings.Mappings.FamilyTypes.DefaultLibraryUri,
            importSettings.Mappings.MaterialsUIState.SelectedLibrary,
            importSettings.Mappings.FamiliesUIState.SelectedLibrary,
        };

        List<string> materialFolders = importSettings.Mappings.Libraries.MaterialFolders;
        for (int i = 0; i < materialFolders.Count; i++)
        {
            paths.Add(materialFolders[i]);
        }

        List<string> assetFolders = importSettings.Mappings.Libraries.AssetFolders;
        for (int i = 0; i < assetFolders.Count; i++)
        {
            paths.Add(assetFolders[i]);
        }

        List<UserMaterialMapping> userMaterials = importSettings.Mappings.Materials.UserMapped;
        for (int i = 0; i < userMaterials.Count; i++)
        {
            paths.Add(userMaterials[i].MdlPath);
        }

        List<UserFamilyTypeMapping> userFamilyTypes = importSettings.Mappings.FamilyTypes.UserMapped;
        for (int i = 0; i < userFamilyTypes.Count; i++)
        {
            paths.Add(userFamilyTypes[i].AssetPath);
        }

        return paths;
    }

    private bool IsUnsupportedCloudPath(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.StartsWith("omniverse:", StringComparison.OrdinalIgnoreCase)
            || uri.StartsWith("omni:", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowUnsupportedCloudPathWarning()
    {
        MessageBox.Show(
            this,
            "Nucleus paths are not supported. Please choose local export and library paths before exporting.",
            "Unsupported Nucleus Paths",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private List<UsdFile> getUsdFiles(string uri)
    {
        List<UsdFile> files = new List<UsdFile>();
        List<string> usd = walkFolder(uri, ".usd");
        foreach (string u in usd)
        {
            files.Add(new UsdFile(u));
        }
        return files;
    }

    private List<MdlFile> getMdlFiles(string uri)
    {
        List<MdlFile> files = new List<MdlFile>();
        List<string> mdl = walkFolder(uri, ".mdl");
        foreach (string m in mdl)
        {
            files.Add(new MdlFile(m));
        }
        return files;
    }

    private List<string> walkFolder(string uri, string ext)
    {
        // Check if URI is a local folder by verifying it exists on the local file system
        if (IsLocalFolder(uri))
        {
            return walkLocalFolder(uri, ext);
        }
        revit.log.warning($"Cannot list files for non-local folder: \"{uri}\"");
        return new List<string>();
    }

    private List<string> walkLocalFolder(string folderPath, string ext)
    {
        List<string> output = new List<string>();

        try
        {
            // Convert URI-style path to Windows path if needed
            string localPath = folderPath.Replace('/', '\\').TrimEnd('\\');

            if (!Directory.Exists(localPath))
            {
                return output;
            }

            // Get all files with the specified extension in the current directory
            string[] files = Directory.GetFiles(localPath, "*" + ext, SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                // Convert back to URI-style path
                string uriPath = file.Replace('\\', '/');
                output.Add(uriPath);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - just return what we found so far
            System.Diagnostics.Debug.WriteLine($"Error walking local folder {folderPath}: {ex.Message}");
        }

        return output;
    }

    // Output Tab Events
    private void tb_fileName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!populating)
        {
            settings.File.FileName = tb_fileName.Text;
        }
    }

    private void combo_extension_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            settings.File.Extension = combo_extension.SelectedItem as string;
        }
    }

    private void b_selectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            string folderUri = FilePicker.GetFolderUri("Select Output Folder", "OK", settings.File.OutputFolder);
            if (!string.IsNullOrEmpty(folderUri))
            {
                settings.File.OutputFolder = folderUri;
                text_folderPath.Text = folderUri;
            }
            setToolTip(text_folderPath);
        }
    }

    private void radio_internalMats_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.MaterialStyle = MaterialStyle.InternalLibrary;
        }
    }

    private void radio_externalMats_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.MaterialStyle = (radio_payloads.IsChecked.Value) ? MaterialStyle.ExternalLibraryAsPayload : MaterialStyle.ExternalLibraryAsReference;
        }
    }

    private void tb_materialPrim_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.MaterialFolderName = tb_materialPrim.Text;
        }
    }

    private void radio_references_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            if (settings.Options.MaterialStyle == MaterialStyle.ExternalLibraryAsPayload)
            {
                settings.Options.MaterialStyle = MaterialStyle.ExternalLibraryAsReference;
            }
            if (settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsPayload || settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.None)
            {
                settings.Options.FamilyInstanceStyle = FamilyInstancingStyle.ExternalAssetAsReference;
            }
        }
    }

    private void radio_payloads_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            if (settings.Options.MaterialStyle == MaterialStyle.ExternalLibraryAsReference)
            {
                settings.Options.MaterialStyle = MaterialStyle.ExternalLibraryAsPayload;
            }
            if (settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.ExternalAssetAsReference || settings.Options.FamilyInstanceStyle == FamilyInstancingStyle.None)
            {
                settings.Options.FamilyInstanceStyle = FamilyInstancingStyle.ExternalAssetAsPayload;
            }
        }
    }

    private void combo_units_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.UnitType = (UnitType)Enum.Parse(typeof(UnitType), combo_units.SelectedItem as string);
        }
    }

    private void cb_use1click_Checked(object sender, RoutedEventArgs e)
    {
        Result.OneClick = true;
    }

    private void cb_use1click_Unchecked(object sender, RoutedEventArgs e)
    {
        Result.OneClick = false;
    }

    private void cb_override_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.OverrideJsonPath = text_overridePath.Text;
            if (!string.IsNullOrEmpty(settings.OverrideJsonPath))
            {
                settings.OverrideWithJson(settings.OverrideJsonPath);
                populateUI();
            }
        }
    }

    private void cb_override_Unchecked(object sender, RoutedEventArgs e)
    {
        settings.OverrideJsonPath = string.Empty;
        text_overridePath.Text = "";
        populateUI();
    }

    private void b_selectJsonOverride_Click(object sender, RoutedEventArgs e)
    {
        string path = FilePicker.GetJsonUri("Select Override Json", "OK", settings.OverrideJsonPath);
        if (!string.IsNullOrEmpty(path))
        {
            text_overridePath.Text = path;
            cb_override.IsChecked = true;
            if (cb_override.IsChecked.Value)
            {
                settings.OverrideJsonPath = text_overridePath.Text;
                if (cb_override.IsChecked == true)
                {
                    settings.OverrideWithJson(settings.OverrideJsonPath);
                    populateUI();
                }
            }
        }
        setToolTip(text_overridePath);
    }

    // Options Tab Events
    private void cb_cameras_Checked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeCameras = true;
    }

    private void cb_cameras_Unchecked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeCameras = false;
    }

    private void cb_links_Checked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeLinks = true;
    }

    private void cb_links_Unchecked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeLinks = false;
    }

    private void cb_lights_Checked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeLights = true;
    }

    private void cb_lights_Unchecked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeLights = false;
    }

    private void cb_bim_Checked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeBimData = true;
    }

    private void cb_bim_Unchecked(object sender, RoutedEventArgs e)
    {
        settings.Options.IncludeBimData = false;
    }

    private void cb_rooms_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.IncludeRooms = true;
            settings.Options.RoomColorScheme = combo_rooms.SelectedItem as string;
        }
    }

    private void cb_rooms_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.IncludeRooms = false;
            settings.Options.RoomColorScheme = "NOT USED";
        }
    }

    private void combo_rooms_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            if (cb_rooms.IsChecked.Value)
            {
                settings.Options.RoomColorScheme = combo_rooms.SelectedItem as string;
            }
        }
    }

    private void cb_spaces_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.IncludeSpaces = true;
            settings.Options.SpaceColorScheme = combo_spaces.SelectedItem as string;
        }
    }

    private void cb_spaces_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.IncludeSpaces = false;
            settings.Options.SpaceColorScheme = "NOT USED";
        }
    }

    private void combo_spaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            if (cb_spaces.IsChecked.Value)
            {
                settings.Options.SpaceColorScheme = combo_spaces.SelectedItem as string;
            }
        }
    }

    private void cb_drawings_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.IncludeDrawings = true;
            settings.Options.DrawingPublishSet = combo_drawings.SelectedItem as string;
        }
    }

    private void cb_drawings_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.IncludeDrawings = false;
            settings.Options.DrawingPublishSet = "NOT USED";
        }
    }

    private void combo_drawings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            if (cb_drawings.IsChecked.Value)
            {
                settings.Options.DrawingPublishSet = combo_drawings.SelectedItem as string;
            }
        }
    }

    private void cb_instance_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.InstanceFamilies = true;
        }
    }

    private void cb_instance_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.InstanceFamilies = false;
        }
    }

    private void combo_instancing_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            if ((string)combo_instancing.SelectedItem == "External Assets")
            {
                settings.Options.FamilyInstanceStyle = (radio_payloads.IsChecked.Value) ? FamilyInstancingStyle.ExternalAssetAsPayload : FamilyInstancingStyle.ExternalAssetAsReference;
            }
            else
            {
                settings.Options.FamilyInstanceStyle = FamilyInstancingStyle.InternalClasses;
            }
        }
    }

    private void combo_coordinates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            settings.Options.CoordinateSystem = combo_coordinates.SelectedIndex;
        }
    }

    // View Tab Events
    private void combo_detailLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            settings.View.DetailLevel = combo_detailLevel.SelectedItem as string;
        }
    }

    private void combo_phaseFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            settings.View.PhaseFilter = combo_phaseFilter.SelectedItem as string;
        }
    }

    private void combo_viewTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            settings.View.ViewTemplate = combo_viewTemplate.SelectedItem as string;
        }
    }
    private void View_cb_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            CheckBox cb = (CheckBox)sender;
            if (!this.settings.ViewsToExport.Contains((string)cb.Content))
            {
                this.settings.ViewsToExport.Add((string)cb.Content);
            }
        }
    }

    private void View_cb_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            CheckBox cb = (CheckBox)sender;
            if (this.settings.ViewsToExport.Contains((string)cb.Content))
            {
                this.settings.ViewsToExport.Remove((string)cb.Content);
            }
        }
    }

    // Batch Tab Events
    private void b_selectBatchInput_Click(object sender, RoutedEventArgs e)
    {
        string inputUri = FilePicker.GetFolderUri("Select Folder with Revit Models", "OK", settings.Batch.InputFolder);
        if (!string.IsNullOrEmpty(inputUri))
        {
            settings.Batch.InputFolder = inputUri;
            text_batchInput.Text = inputUri;
        }
        setToolTip(text_batchInput);
    }

    private void b_selectBatchOutput_Click(object sender, RoutedEventArgs e)
    {
        string outputUri = FilePicker.GetFolderUri("Select Output Folder for Batch Export", "OK", settings.Batch.OutputFolder);
        if (!string.IsNullOrEmpty(outputUri))
        {
            settings.Batch.OutputFolder = outputUri;
            text_batchOutput.Text = outputUri;
        }
        setToolTip(text_batchOutput);
    }

    private void slider_folderDepth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!populating)
        {
            settings.Batch.FolderDepth = (int)slider_folderDepth.Value;
            text_folderDepth.Text = settings.Batch.FolderDepth.ToString();
        }
    }

    private void tb_batchViewString_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!populating)
        {
            settings.Batch.ViewToFind = tb_batchViewString.Text;
        }
    }

    // Primary Button Events
    private void b_save_Click(object sender, RoutedEventArgs e)
    {
        Result.Save = true;
        this.cleanclose = true;
        this.Close();
    }

    private void b_startExport_Click(object sender, RoutedEventArgs e)
    {
        this.cleanclose = true;
        this.Close();
    }

    private void b_exportJson_Click(object sender, RoutedEventArgs e)
    {
        string exportUri = FilePicker.SaveJsonUri("Save Json Settings as...", "Save", settings.OverrideJsonPath);
        Result.ExportJsonUri = exportUri;
        if (!populating && !string.IsNullOrEmpty(exportUri))
        {
            // Update settings with current mappings from materialData and familyData
            CopyMappingUIToSettings();
            settings.Write(exportUri);
        }
    }

    private void b_import_Click(object sender, RoutedEventArgs e)
    {
        string importUri = FilePicker.GetJsonUri("Select Json to Import", "OK");
        if (System.IO.File.Exists(importUri))
        {
            Console.WriteLine("Importing settings from: " + importUri);
            settings = RevitUsdExportSettings.Read(importUri);
            bool hasUnsupportedCloudPaths = HasUnsupportedCloudPaths(settings);
            Console.WriteLine("imported");
            SyncMappingsToUIData();
            populateUI();
            Console.WriteLine("populated");
            if (hasUnsupportedCloudPaths)
            {
                ShowUnsupportedCloudPathWarning();
            }
        }
    }

    private void b_cancel_Click(object sender, RoutedEventArgs e)
    {
        this.Result.Canceled = true;
        this.Close();
    }

    // Mapping Events
    private void combo_root_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            string uri = (string)combo_root.SelectedItem;
            if (radio_materials.IsChecked == true)
            {
                if (combo_root.SelectedIndex == combo_root.Items.Count - 1)
                {
                    string result = FilePicker.GetFolderUri("Select a Root Library for Mapping", "Select", settings.Mappings.Materials.DefaultLibraryUri);
                    if (!string.IsNullOrEmpty(result))
                    {
                        settings.Mappings.Materials.DefaultLibraryUri = result;
                        if (!settings.Mappings.Libraries.MaterialFolders.Contains(result))
                        {
                            settings.Mappings.Libraries.MaterialFolders.Add(result);
                        }
                    }
                    uri = result;
                }
            }
            else
            {
                if (combo_root.SelectedIndex == combo_root.Items.Count - 1)
                {
                    string result = FilePicker.GetFolderUri("Select a Root Library for Mapping", "Select", settings.Mappings.FamilyTypes.DefaultLibraryUri);
                    if (!string.IsNullOrEmpty(result))
                    {
                        settings.Mappings.FamilyTypes.DefaultLibraryUri = result;
                        if (!settings.Mappings.Libraries.AssetFolders.Contains(result))
                        {
                            settings.Mappings.Libraries.AssetFolders.Add(result);
                        }
                    }
                    uri = result;
                }
            }
            if (!string.IsNullOrEmpty(uri))
            {
                combo_root_previousSelectedIndex = combo_root.SelectedIndex;
                // Save the selected library
                if (radio_materials.IsChecked == true)
                {
                    settings.Mappings.MaterialsUIState.SelectedLibrary = uri;
                }
                else
                {
                    settings.Mappings.FamiliesUIState.SelectedLibrary = uri;
                }
                populateMappingLibrary(uri);
                populateMappingSelection();
                CopyActiveMappingSelectionToSettings();
            }
            else
            {
                // Return to the previous selection.
                combo_root.SelectionChanged -= combo_root_SelectionChanged;
                combo_root.SelectedIndex = combo_root_previousSelectedIndex;
                combo_root.SelectionChanged += combo_root_SelectionChanged;
            }

            setToolTip(combo_root);
            setToolTip(combo_bucket);
            setToolTip(combo_file);
            setToolTip(combo_module);
        }
    }

    private void combo_bucket_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            populating = true;
            string bucket = (string)combo_bucket.SelectedItem;
            string root = (string)combo_root.SelectedItem;
            // Save the selected group
            if (radio_materials.IsChecked == true)
            {
                settings.Mappings.MaterialsUIState.SelectedGroup = bucket;
            }
            else
            {
                settings.Mappings.FamiliesUIState.SelectedGroup = bucket;
            }
            combo_file.Items.Clear();
            combo_module.Items.Clear();
            if (radio_materials.IsChecked == true)
            {
                List<MdlFile> files = mdlFiles[root].Where(f => f.Bucket == bucket).ToList();
                foreach (MdlFile file in files)
                {
                    combo_file.Items.Add(file.Name);
                }
                if (files.Count > 0)
                {
                    combo_file.SelectedIndex = 0;
                    List<string> modules = files[0].GetModules();
                    foreach (string module in modules)
                    {
                        combo_module.Items.Add(module);
                    }
                    if (modules.Count > 0)
                    {
                        combo_module.SelectedIndex = 0;
                    }
                }
            }
            else
            {
                List<UsdFile> files = usdFiles[root].Where(f => f.Bucket == bucket).ToList();
                foreach (UsdFile file in files)
                {
                    combo_file.Items.Add(file.Name);
                }
                if (files.Count > 0)
                {
                    combo_file.SelectedIndex = 0;
                }
            }
            populating = false;
            CopyActiveMappingSelectionToSettings();
        }
        setToolTip(combo_bucket);
        setToolTip(combo_file);
        setToolTip(combo_module);
    }

    private void combo_file_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            string file = (string)combo_file.SelectedItem;
            // Save the selected file
            if (radio_materials.IsChecked == true)
            {
                settings.Mappings.MaterialsUIState.SelectedFile = file;
                populating = true;
                combo_module.Items.Clear();
                string bucket = (string)combo_bucket.SelectedItem;
                string root = (string)combo_root.SelectedItem;
                List<MdlFile> inBucket = mdlFiles[root].Where(f => f.Bucket == bucket).ToList();
                MdlFile selected = inBucket.FirstOrDefault(f => f.Name == file);
                if (selected == null)
                    return;
                List<string> modules = selected.GetModules();
                foreach (string module in modules)
                {
                    combo_module.Items.Add(module);
                }
                combo_module.SelectedIndex = 0;
                populating = false;
            }
            else
            {
                settings.Mappings.FamiliesUIState.SelectedFile = file;
            }
            CopyActiveMappingSelectionToSettings();
        }
        setToolTip(combo_file);
        setToolTip(combo_module);
    }

    private void combo_module_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            string module = (string)combo_module.SelectedItem;
            settings.Mappings.MaterialsUIState.SelectedModule = module;
        }
        setToolTip(combo_module);
    }

    private void b_assign_Click(object sender, RoutedEventArgs e)
    {
    }

    private void combo_category_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!populating)
        {
            if (radio_families.IsChecked == true)
            {
                string category = (string)combo_category.SelectedItem;
                settings.Mappings.FamiliesUIState.SelectedCategory = category;
                lb_mappings.UnselectAll();
                populateMappingSelection();
                CopyActiveMappingSelectionToSettings();
            }
        }
    }

    private void tb_search_TextChanged(object sender, TextChangedEventArgs e)
    {
        lb_mappings.UnselectAll();
        populateMappingSelection();
    }

    private void b_all_Click(object sender, RoutedEventArgs e)
    {
    }

    private void lb_mappings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void assign_mapping_Click(object sender, RoutedEventArgs e)
    {

        string root = (string)combo_root.SelectedItem;
        string bucket = (string)combo_bucket.SelectedItem;
        string file = (string)combo_file.SelectedItem;
        if (radio_materials.IsChecked == true)
        {
            string module = (string)combo_module.SelectedItem;
            List<MdlFile> inBucket = mdlFiles[root].Where(f => f.Bucket == bucket).ToList();
            MdlFile selected = inBucket.FirstOrDefault(f => f.Name == file);
            if (selected == null)
                return;
            foreach (var item in lb_mappings.SelectedItems)
            {
                TextBlock tb = (TextBlock)item;
                long id = long.Parse(tb.Name.Replace("text_", ""));
                MaterialData m = materialData.FirstOrDefault(f => f.Id == id);
                if (m == null)
                    continue;
                tb.Background = new SolidColorBrush(Colors.LightGray);
                tb.Text = m.Name + " | " + module;
                tb.ToolTip = selected.Path;
                m.Mapped = true;
                m.MdlModule = module;
                m.MdlPath = selected.Path;
            }
        }
        else
        {
            List<UsdFile> inBucket = usdFiles[root].Where(f => f.Bucket == bucket).ToList();
            UsdFile selected = inBucket.FirstOrDefault(f => f.Name == file);
            if (selected == null)
                return;
            foreach (var item in lb_mappings.SelectedItems)
            {
                TextBlock tb = (TextBlock)item;
                long id = long.Parse(tb.Name.Replace("text_", ""));
                FamilyData m = familyData.FirstOrDefault(f => f.Id == id);
                if (m == null)
                    continue;
                tb.Background = new SolidColorBrush(Colors.LightGray);
                tb.Text = m.FamilyName + " - " + m.TypeName + " | " + selected.Name;
                tb.ToolTip = selected.Path;
                m.AssetPath = selected.Path;
                m.Mapped = true;
            }
        }
        lb_mappings.UnselectAll();
    }

    private void remove_mapping_Click(object sender, RoutedEventArgs e)
    {
        if (radio_materials.IsChecked == true)
        {
            foreach (var item in lb_mappings.SelectedItems)
            {
                TextBlock tb = (TextBlock)item;
                long id = long.Parse(tb.Name.Replace("text_", ""));
                MaterialData m = materialData.FirstOrDefault(f => f.Id == id);
                if (m == null)
                    continue;
                tb.Background = new SolidColorBrush(Colors.White);
                tb.Text = m.Name;
                tb.ToolTip = string.Empty;
                m.Mapped = false;
                m.MdlModule = string.Empty;
                m.MdlPath = string.Empty;
            }
            lb_mappings.UnselectAll();
        }
        else
        {
            foreach (var item in lb_mappings.SelectedItems)
            {
                TextBlock tb = (TextBlock)item;
                long id = long.Parse(tb.Name.Replace("text_", ""));
                FamilyData m = familyData.FirstOrDefault(f => f.Id == id);
                if (m == null)
                    continue;
                tb.Background = new SolidColorBrush(Colors.White);
                tb.Text = m.FamilyName + " - " + m.TypeName;
                tb.ToolTip = string.Empty;
                m.Mapped = false;
            }
            lb_mappings.UnselectAll();
        }
    }

    private void lb_mappings_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
        {
            sv_selection.LineUp();
            sv_selection.LineUp();
            sv_selection.LineUp();
        }
        else
        {
            sv_selection.LineDown();
            sv_selection.LineDown();
            sv_selection.LineDown();
        }
    }

    private void radio_materials_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Mappings.IsMaterialsMode = true;

            // Use saved library if available, otherwise use default
            string libToUse = !string.IsNullOrEmpty(settings.Mappings.MaterialsUIState.SelectedLibrary) ? settings.Mappings.MaterialsUIState.SelectedLibrary : settings.Mappings.Materials.DefaultLibraryUri;

            populateMappingLibrary(libToUse);
            populateMappingSelection();

            MappingUIState currentState = settings.Mappings.MaterialsUIState;
            RestoreMappingSelection(currentState);
            CopyActiveMappingSelectionToSettings();
        }
    }

    private void radio_families_Checked(object sender, RoutedEventArgs e)
    {
        if (!populating)
        {
            settings.Mappings.IsMaterialsMode = false;

            // Use saved library if available, otherwise use default
            string libToUse = !string.IsNullOrEmpty(settings.Mappings.FamiliesUIState.SelectedLibrary) ? settings.Mappings.FamiliesUIState.SelectedLibrary : settings.Mappings.FamilyTypes.DefaultLibraryUri;

            populateMappingLibrary(libToUse);
            populateMappingSelection();

            MappingUIState currentState = settings.Mappings.FamiliesUIState;
            RestoreMappingSelection(currentState);
            CopyActiveMappingSelectionToSettings();
        }
    }
}

[Serializable]
internal class MdlFile
{
    public string Name = string.Empty;
    public string Path = string.Empty;
    public string Bucket = string.Empty;
    public List<string> Modules = new List<string>();

    [Newtonsoft.Json.JsonConstructor]
    public MdlFile()
    {
    }

    public MdlFile(string uri)
    {
        Path = uri;
        string[] splits = uri.Split('/');
        if (splits.Length >= 2)
        {
            Name = splits[splits.Length - 1];
            Bucket = splits[splits.Length - 2];
        }
    }

    public List<string> GetModules()
    {
        if (Modules.Count == 0)
        {
            string localPath = Path.Replace('/', System.IO.Path.DirectorySeparatorChar);
            if (revit.file.client.isLocalUri(Path) && File.Exists(localPath))
            {
                string[] lines = File.ReadAllLines(localPath);
                foreach (string line in lines)
                {
                    if (line.Contains("export material"))
                    {
                        string theRest = line.Replace("export material ", "");
                        Modules.Add(theRest.Split('(')[0]);
                    }
                }
            }
        }
        return Modules;
    }
}

[Serializable]
internal class UsdFile
{
    public string Name = string.Empty;
    public string Path = string.Empty;
    public string Bucket = string.Empty;

    [Newtonsoft.Json.JsonConstructor]
    public UsdFile()
    {
    }

    public UsdFile(string uri)
    {
        Path = uri;
        string[] splits = uri.Split('/');
        if (splits.Length >= 2)
        {
            Name = splits[splits.Length - 1];
            Bucket = splits[splits.Length - 2];
        }
    }
}
}

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
using RevitUsdExportSdk;
using System.IO;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json.Linq;

namespace RevitUsdExportPlugin
{
public static class Storage
{
    public static RevitUsdExportSettings GetSettings(Document doc, out bool oneClick)
    {
        oneClick = false;
        RevitUsdExportSettings settings = new RevitUsdExportSettings(doc.Title);
        bool savedInternal = isInternalSettings(doc, out oneClick);

        // Reads internal settings when "Enable 1-Click Export"(oneClick) is true.
        if (oneClick)
        {
            settings = getInternalSettings(doc, false);
        }
        else
        {
            string cleanTitle = App.RemoveBadWindowsFilePathChars(doc.Title);
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $@"Omniverse/Revit/{cleanTitle}");
            if (Directory.Exists(path))
            {
                string settingFile = Path.Combine(path, "settings.json");
                if (File.Exists(settingFile))
                {
                    string settingsString = File.ReadAllText(settingFile);
                    settings = Newtonsoft.Json.JsonConvert.DeserializeObject<RevitUsdExportSettings>(settingsString);
                }
            }
        }
        return settings;
    }

    // Save internally when "Enable 1-Click Export"(isOn) is true.
    public static void SaveSettings(Document doc, RevitUsdExportSettings settings, bool isOn)
    {
        if (isOn)
        {
            using (Transaction t = new Transaction(doc))
            {
                if (t.Start("Save Omniverse Advanced Settings") == TransactionStatus.Started)
                {
                    DataStorage storage = getSettingsDataStorage(doc, true);
                    saveSettings(doc.Title, storage, settings, isOn);
                }
                t.Commit();
            }
        }
        else
        {
            updateInternalSettingsOnStorage(doc, isOn);
            saveSettings(doc.Title, null, settings, isOn);
        }
    }

    // If the settings are saved in the model file, isOn is updated.
    private static void updateInternalSettingsOnStorage(Document doc, bool isOn)
    {
        bool oneClick = false;
        bool savedInternal = isInternalSettings(doc, out oneClick);
        if (!savedInternal || oneClick == isOn)
            return;

        using (Transaction t = new Transaction(doc))
        {
            if (t.Start("Save Omniverse Advanced Settings") == TransactionStatus.Started)
            {
                DataStorage storage = getSettingsDataStorage(doc, true);
                if (storage != null)
                {
                    Schema schema = getSettingsSchema(true);
                    Entity entity = storage.GetEntity(schema);
                    if (entity.Schema != null && entity.IsValidObject)
                    {
                        Field on = schema.GetField("isOn");
                        entity.Set<bool>(on, isOn);
                        storage.SetEntity(entity);
                    }
                }
            }
            t.Commit();
        }
    }

    public static bool isInternalSettings(Document doc, out bool oneClick)
    {
        oneClick = false;
        DataStorage storage = getSettingsDataStorage(doc, false);
        if (storage == null)
        {
            return false;
        }
        else
        {
            Schema schema = getSettingsSchema(false);
            if (schema == null)
            {
                return false;
            }
            else
            {
                Entity e = storage.GetEntity(schema);
                if (e.Schema != null && e.IsValidObject)
                {
                    Field field = schema.GetField("isOn");
                    oneClick = e.Get<bool>(field);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    private static RevitUsdExportSettings getInternalSettings(Document doc, bool doTransaction)
    {
        string json = string.Empty;
        if (doTransaction)
        {
            using (Transaction t = new Transaction(doc))
            {
                if (t.Start("Get Advanced Settings") == TransactionStatus.Started)
                {
                    DataStorage storage = getSettingsDataStorage(doc, doTransaction);
                    Schema schema = getSettingsSchema(doTransaction);
                    Entity entity = storage.GetEntity(schema);
                    if (entity.Schema != null && entity.IsValidObject)
                    {
                        Field field = schema.GetField("settings");
                        json = entity.Get<string>(field);
                    }
                }
                t.Commit();
            }
        }
        else
        {
            DataStorage storage = getSettingsDataStorage(doc, doTransaction);
            Schema schema = getSettingsSchema(doTransaction);
            if (schema != null && storage != null)
            {
                Entity entity = storage.GetEntity(schema);
                if (entity.Schema != null && entity.IsValidObject)
                {
                    Field field = schema.GetField("settings");
                    json = entity.Get<string>(field);
                }
            }
        }
        if (json != string.Empty)
        {
            JObject jObj = JObject.Parse(json);
            return RevitUsdExportSettings.Read(jObj, string.Empty);
        }
        return new RevitUsdExportSettings();
    }

    private static DataStorage getSettingsDataStorage(Document doc, bool inTransaction)
    {
        var dataStorages = new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).Cast<DataStorage>().ToList();
        foreach (DataStorage ds in dataStorages)
        {
            Schema schema = getSettingsSchema(inTransaction);
            if (schema != null)
            {
                Entity e = ds.GetEntity(schema);
                if (e.Schema != null && e.IsValid())
                {
                    return ds;
                }
            }
        }
        if (inTransaction)
        {
            return DataStorage.Create(doc);
        }
        return null;
    }

    private static void saveSettings(string docTitle, DataStorage e, RevitUsdExportSettings settings, bool isOn)
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
        if (isOn && e != null)
        {
            Schema schema = getSettingsSchema(true);
            Entity entity = e.GetEntity(schema);
            if (entity.Schema != null && entity.IsValidObject)
            {
                Field field = schema.GetField("settings");
                entity.Set<string>(field, json);
                Field on = schema.GetField("isOn");
                entity.Set<bool>(on, isOn);
                // Field over = schema.GetField("overridePath");
                // entity.Set<string>(over, overridePath);
                e.SetEntity(entity);
            }
            else
            {
                entity = new Entity(schema);
                Field field = schema.GetField("settings");
                entity.Set<string>(field, json);
                Field on = schema.GetField("isOn");
                entity.Set<bool>(on, isOn);
                // Field over = schema.GetField("overridePath");
                // entity.Set<string>(over, overridePath);
                e.SetEntity(entity);
            }
        }
        else
        {
            string cleanTitle = App.RemoveBadWindowsFilePathChars(docTitle);
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $@"Omniverse/Revit/{cleanTitle}");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            File.WriteAllText(Path.Combine(path, "settings.json"), json);
        }
    }

    private static Schema getSettingsSchema(bool inTransaction)
    {
        List<Schema> schemas = Schema.ListSchemas().ToList();
        if (schemas == null || schemas.Count == 0)
        {
            if (inTransaction)
            {
                return createSettingsSchema();
            }
            return null;
        }
        else
        {
            if (schemas.Any(s => s.SchemaName == "OmniSettings"))
            {
                return schemas.Where(s => s.SchemaName == "OmniSettings").First();
            }
            else
            {
                if (inTransaction)
                {
                    return createSettingsSchema();
                }
                return null;
            }
        }
    }

    private static Schema createSettingsSchema()
    {
        Guid guid = new Guid("12c5e7ca-0aa5-44e4-87f1-30fd4abc97ac");
        SchemaBuilder builder = new SchemaBuilder(guid);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.SetSchemaName("OmniSettings");
        builder.SetDocumentation("field for storing Omniverse publish settings");
        FieldBuilder setting = builder.AddSimpleField("settings", typeof(string));
        FieldBuilder isOn = builder.AddSimpleField("isOn", typeof(bool));
        FieldBuilder over = builder.AddSimpleField("overridePath", typeof(string));
        return builder.Finish();
    }
}
}

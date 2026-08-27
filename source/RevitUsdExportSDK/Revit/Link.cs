// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace RevitUsdExportSdk
{
internal class Link
{
    public long LinkId;
    public long StageId;
    public long MaterialStageId;
    public Transform Transform;

    public Link(long linkId, long stageId, long materialStageId)
    {
        LinkId = linkId;
        StageId = stageId;
        MaterialStageId = materialStageId;
    }

    public static void SetUpLinks(List<RevitLinkInstance> links, string folderPath)
    {
        Scope linkCategory = new Scope(ExportManager.MainStage.Id, "RVT Links", ExportManager.MainStage.Default);
        List<LinkData> direct = new List<LinkData>();
        List<LinkData> nested = new List<LinkData>();
        foreach (RevitLinkInstance link in links)
        {
            LinkData l = new LinkData(link);
            if (l.ParentName == string.Empty)
            {
                direct.Add(l);
            }
            else
            {
                nested.Add(l);
            }
        }
        foreach (LinkData l in direct)
        {
            Xform linkXform = new Xform(linkCategory.StageId, l.IndexedName, PrimKind.Component, linkCategory);
            Link link = l.CreateLink(folderPath + "/" + l.Document.Title);
            linkXform.AddStageReference(link.StageId, true);
            ExportManager.AddElement(ExportManager.GetMainDocument().Title, l.LinkId, linkXform);
            ExportManager.MainStage.Links.Add(l.LinkId, link);
            Stage matStage = ExportManager.TryGetStage(link.MaterialStageId);
            MaterialManager.ProcessMaterials(l.Document, matStage.Default);
            if (nested.Any(n => n.ParentName == l.Name))
            {
                Stage stage = ExportManager.TryGetStage(link.StageId);
                Scope nlCategory = new Scope(stage.Id, "RVT Links", stage.Default);
                foreach (LinkData nl in nested.Where(n => n.ParentName == l.Name))
                {
                    Xform nlXform = new Xform(link.StageId, nl.IndexedName, PrimKind.Component, nlCategory);
                    Link nestedLink = nl.CreateLink(folderPath + "/Links/" + nl.Document.Title);
                    nlXform.AddStageReference(nl.StageId, true);
                    ExportManager.AddElement(l.Document.Title, nl.LinkId, nlXform);
                    stage.Links.Add(nl.LinkId, nestedLink);
                    Stage nmStage = ExportManager.TryGetStage(nestedLink.MaterialStageId);
                    MaterialManager.ProcessMaterials(nl.Document, nmStage.Default);
                }
            }
        }
    }
}

internal class LinkData
{
    public long LinkId;
    public string InstanceName;
    public string Name;
    public string IndexedName;
    public string ParentName;
    public Document Document;
    public long StageId;

    public LinkData(RevitLinkInstance instance)
    {
        LinkId = instance.Id.GetValue();
        this.InstanceName = instance.Name;
        string[] splits = instance.Name.Split(':');
        if (splits.Length == 4)
        {
            ParentName = splits[0].Replace(".rvt", "").Trim();
        }
        else
        {
            ParentName = string.Empty;
        }
        string index = "_" + splits[splits.Length - 2].Trim();
        string name = splits[splits.Length - 3].Replace(".rvt", "").Trim();
        Name = name;
        IndexedName = name + index;
        Document = instance.GetLinkDocument();
    }

    public Link CreateLink(string folderPath)
    {
        Stage stage = new Stage(folderPath, IndexedName.RemoveBadWindowsFilePathChars(), ExportManager.Settings.File.Extension, Name, true);
        StageId = stage.Id;
        Stage materialStage;
        if (ExportManager.Settings.Options.MaterialStyle == MaterialStyle.InternalLibrary)
        {
            materialStage = stage;
        }
        else
        {
            materialStage = new Stage(folderPath, ExportManager.Settings.Options.MaterialFolderName, ExportManager.Settings.File.Extension, ExportManager.Settings.Options.MaterialFolderName, true);
            if (ExportManager.Settings.Options.MaterialStyle == MaterialStyle.ExternalLibraryAsPayload)
            {
                stage.Default.AddStageReference(materialStage.Id, true);
            }
            else
            {
                stage.Default.AddStageReference(materialStage.Id, false);
            }
        }
        return new Link(LinkId, stage.Id, materialStage.Id);
    }
}
}

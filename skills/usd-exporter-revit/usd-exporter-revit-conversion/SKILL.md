---
name: usd-exporter-revit-conversion
description: Use when a user asks to export Autodesk Revit models to USD, USDA, USDC, or USDZ with the usd-exporter-revit, choose export settings, validate outputs, or troubleshoot exported USD.
version: "0.1.0"
license: Apache-2.0 AND CC-BY-4.0
metadata:
  author: "NVIDIA Corporation"
  tags: [omniverse, openusd, revit, exporter, conversion, settings]
tools: [Read, Edit]
---
<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

# Conversion - usd-exporter-revit

## Purpose

Guide users to export Revit models to OpenUSD through the plugin UI in a live Revit session (or document in-process `UsdExporterRevitCore` usage for custom Revit hosts), including single-view and batch exports. Choose export settings from user intent, verify outputs, and troubleshoot export results.

## When to Use

Use this module when a user asks to export a Revit model, choose export settings, explain Revit-to-USD mapping, troubleshoot exported USD, or inspect supported export behavior.

For installation, builds, plugin install, or first-run smoke tests, load `../usd-exporter-revit-getting-started/SKILL.md`.

## Prerequisites

- Matching Autodesk Revit version with the plugin installed, or a custom in-process Revit host using `UsdExporterRevitCore` (core calls require a live `UIApplication`).
- A Revit document with a 3D view that shows the intended elements, opened in Revit by the user.
- Exports write to local folders selected through the Windows file picker or settings (`File.OutputFolder`).

## Supported Outputs

Supported USD outputs: `.usd`, `.usda`, `.usdc`, `.usdz` (default `.usdc`).

USDZ preserves:

- Geometry, hierarchy, transforms, metadata, material bindings, and enabled export options.
- Resolved USD layers, payload layers, image textures, and other resolved dependencies.
- Revit-derived `UsdPreviewSurface` materials and supported texture inputs.

USDZ removes:

- MDL shader prims.
- `outputs:mdl:*` material connections.
- `.mdl` asset paths.

Use `.usd`, `.usda`, or `.usdc` when the downstream workflow requires MDL or editable layers.

User-mapped MDL-only materials require a `UsdPreviewSurface` representation to retain their appearance.

Packaging fails when a non-MDL dependency cannot resolve. The exporter does not replace an existing package after failure.

Export is view-driven: only elements visible in the selected 3D view are exported. Geometry is tessellated `UsdGeomMesh` or, for eligible pipes/conduits/round ducts, `UsdGeomCylinder`.

For concept mapping and the export-options matrix, prefer `docs/concept_mapping.md` (section "Export Options That Affect Mapping") and the plugin settings UI.

## Instructions

1. Confirm the source Revit model opens and the intended 3D view shows the elements to export.
2. Confirm the requested output path uses a supported USD extension.
3. Choose export settings from the user's intent for cameras, lights, rooms, spaces, drawings, BIM data, links, family instancing, materials, units, or coordinate systems. Enable only the options the user's request requires.
4. Instruct the user to export through the USD Exporter ribbon in Revit (single-view or batch). For API integrators only, document `UsdExporterRevitCore.Exporter` APIs that run inside a Revit host.
5. Return the exact output path, relevant settings, and any warnings or validation results the user reports.

## Plugin And Core Entry Points

- Plugin UI: USD Exporter ribbon export commands in Revit (primary end-user path).
- Core entry point: `source/UsdExporterRevitCore/Exporter.cs` (`ExportView` / `ExportBatch` require live `UIApplication`; in-process Revit hosts only).
- Settings model: `source/UsdExporterRevitCore/UsdExporterRevitSettings.cs`.
- Product docs: [Revit documentation](https://docs.omniverse.nvidia.com/connect/latest/revit.html)
- Per-model settings path: `%USERPROFILE%\Documents\Omniverse\UsdExporterRevit\<model name>\settings.json`, or inside the Revit file when 1-Click Export is enabled.
- Logs: `%USERPROFILE%\.usd_exporter_revit\logs\Revit-<revit version>\UsdExporterRevit-<plugin version>\<YYYYMMDD_HHMMSS>.log`

## Examples

Typical UI flow (matches README Usage):

1. Open a Revit model.
2. Select export command from USD Exporter ribbon.
3. Choose export settings and local output folder.
4. Export model as OpenUSD.

API-oriented settings focus:

```text
File.Extension = .usdc
Options.IncludeCameras / IncludeLights / IncludeLinks / IncludeBimData as requested
Options.CoordinateSystem = 0 Internal Origin | 1 Project Base Point | 2 Survey Point | 3 Shared Coordinates
Options.UnitType = Feet (default) or requested linear unit
Options.MaterialStyle = ExternalLibraryAsReference (default)
```

## Verification

| Check | How |
|-------|-----|
| Export result | User exports a small 3D view; confirm the root USD file exists and matches requested includes |
| Mapping / hierarchy claims | Compare against `docs/concept_mapping.md` and the authored stage |
| USDZ materials | Confirm material bindings, `UsdPreviewSurface` outputs, and packaged texture paths resolve |

## Output Format

Agents cannot run the export. Default response is guidance:

```markdown
Export `<model/view>` via the USD Exporter ribbon in Revit.

Settings:
- <extension/units/coordinate/include options to use>

Output:
- <expected path or File.OutputFolder>

Notes:
- <materials/instancing/links/BIM choices, if any>
- <verification steps>
```

If the user already exported and reports a path, summarize:

```markdown
Exported `<model/view>` to `<output>` with usd-exporter-revit.

Settings:
- <extension/units/coordinate/include options used>

Notes:
- <warnings or validation results, if any>
```

## Limitations

- Agents cannot run the export for the user; provide ribbon steps, settings, and verification guidance instead (see root skill Core Constraints for host/CLI rules).
- Export does not preserve enough information for general USD-to-Revit round trip.
- Native Revit B-rep topology is not emitted as an OpenUSD B-rep schema.
- Option-gated concepts (rooms, spaces, links, lights, cameras, drawings, BIM attributes) appear only when enabled.
- USDZ removes MDL data and uses `UsdPreviewSurface` for material appearance.
- User-mapped MDL-only materials do not retain their appearance without a `UsdPreviewSurface` representation.
- USDZ export requires all non-MDL dependencies to resolve during packaging.

## Troubleshooting

For empty stages, missing materials/links/BIM data, wrong orientation/units, or crashes without useful context, read `references/troubleshooting.md`.

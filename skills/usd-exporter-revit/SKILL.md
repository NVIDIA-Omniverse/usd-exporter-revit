---
name: usd-exporter-revit
description: Use when a user asks to install, build, package, document, troubleshoot, modify, or run the usd-exporter-revit to export Autodesk Revit models to OpenUSD.
version: "0.1.0"
license: Apache-2.0 AND CC-BY-4.0
metadata:
  author: "NVIDIA Corporation"
  tags: [omniverse, openusd, revit, usd-exporter-revit, exporter]
tools: [Read, Edit, Shell]
---

# usd-exporter-revit

<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

The usd-exporter-revit is a Windows Revit add-in, reusable C# SDK, native OpenUSD bindings, installer, and test harness for exporting Autodesk Revit models to OpenUSD for Omniverse and other OpenUSD ecosystem applications.

It supports single-view and batch exports, preserves Revit geometry and BIM data, and writes outputs to local folders selected through the Windows file picker. Treat exported USD and logs as sensitive (logs may contain full paths); prefer private local folders and keep Include BIM Data off unless needed.

## Route First

Load the narrowest module that matches the user's task:

- For installation, build setup, plugin install, or first-run smoke tests, read `usd-exporter-revit-getting-started/SKILL.md`.
- For USD export, export settings selection, concept mapping, output verification, or export troubleshooting, read `usd-exporter-revit-conversion/SKILL.md`.
- For code changes, docs updates, tests, packaging, or contribution workflow, read `usd-exporter-revit-contributing/SKILL.md`.

## Use This Skill For

- Install, verify, build, or troubleshoot usd-exporter-revit.
- Guide users to export Revit 2024/2025/2026 models to `.usd`, `.usda`, or `.usdc` through the USD Exporter ribbon in Revit, or document in-process `UsdExporterRevitSdk.Exporter` for custom Revit hosts.
- Choose export settings for cameras, lights, rooms, spaces, drawings, BIM data, links, family instancing, materials, units, or coordinate systems.
- Update code, docs, tests, packaging, or release behavior for Revit-to-OpenUSD workflows.

## Core Constraints

1. Target host is Autodesk Revit on Windows only. Supported host versions are Revit 2024, 2025, and 2026.
2. No product CLI or headless conversion. Export always runs inside a live Autodesk Revit session with the plugin (Omniverse ribbon) or another in-process Revit host calling `UsdExporterRevitSdk` (`Exporter.ExportView` / `ExportBatch` require `UIApplication`). Agents cannot convert `.rvt` files for the user; guide the user to export from Revit. `RevitTestHarness` launches `Revit.exe` over IPC for integration tests only — not an end-user conversion CLI.
3. Export is view-driven. Only elements visible in the selected 3D view are exported; Revit view visibility, section box, phase filter, detail level, and temporary view settings define the source set.
4. Supported USD outputs are `.usd`, `.usda`, and `.usdc` only.
5. Prefer product docs, `README.md`, and `docs/concept_mapping.md` when explaining mapping behavior. Keep README snippets, settings defaults, and tests aligned when export options or packaging change.
6. Do not commit machine-specific RevitDevKit paths, build output, proprietary Revit models, or local environment files.

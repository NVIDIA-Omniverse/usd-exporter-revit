---
name: revit-usd-export-contributing
description: Use when a user asks to modify, test, document, package, or review code changes for the NVIDIA OpenUSD Exporter Plugin for Revit repository, SDK, native bindings, installer, or tests.
version: "0.1.0"
license: Apache-2.0 AND CC-BY-4.0
metadata:
  author: "NVIDIA Corporation"
  tags: [omniverse, openusd, revit, contributing, packaging]
tools: [Read, Edit, Shell]
---

# Contributing - Revit USD Export

<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

## Purpose

Guide code, packaging, documentation, and test changes for the NVIDIA OpenUSD Exporter Plugin for Revit repository without mixing contributor workflow into the end-user export skill.

## When to Use

Use this module when a user asks to change exporter implementation, settings, native bindings, plugin UI, installer, smoke/integration tests, docs, release notes, or repository contribution workflow.

For installing or building the plugin, load `../revit-usd-export-getting-started/SKILL.md`. For running exports or choosing settings, load `../revit-usd-export-conversion/SKILL.md`.

## Read First

Before editing, identify the change type and read the local implementation:

| Change type | Read first |
|-------------|------------|
| Export entry points | `source/RevitUsdExportSDK/Exporter.cs` |
| Export settings | `source/RevitUsdExportSDK/RevitUsdExportSettings.cs` |
| Geometry / materials capture | `source/RevitUsdExportSDK/ExportContext.cs`, `source/RevitUsdExportSDK/Managers/` |
| USD authoring (C#) | `source/RevitUsdExportSDK/Usd/` |
| Revit concept mapping (C#) | `source/RevitUsdExportSDK/Revit/` |
| Native OpenUSD bindings | `source/cpp/`, `source/bindings/` |
| Plugin UI / commands | `source/RevitUsdExportPlugin/`, `source/RevitUsdExportPlugin.UI/` |
| Installer | `source/RevitUsdExportSetup/`, `tools/nsis/`, `tools/repoman/installer.py` |
| Test harness | `source/RevitTestHarness/`, `tests/` |
| Concept mapping docs | `docs/concept_mapping.md` |
| Build and contribution docs | `README.md`, `CONTRIBUTING.md` |

Keep README snippets, settings defaults, concept mapping docs, and tests aligned when export options, entry points, install flow, or packaging behavior change.

## Hard Rules

1. Keep changes focused and add tests for affected behavior.
2. Keep public SDK APIs backwards compatible unless the change intentionally proposes a breaking change.
3. Preserve valid OpenUSD identifiers, namespace structure, stage metadata, and composition behavior when changing export logic.
4. Export APIs (`Exporter.ExportView` / `ExportBatch`) require a live Revit `UIApplication`. Do not invent a product CLI (see root skill Core Constraints).
5. Build every supported Revit version affected by the change (`2024` / `2025` / `2026`).
6. Do not commit machine-local RevitDevKit paths, `_build` output, proprietary Revit models, or local environment files.
7. Sign off commits with DCO (`git commit -s`) per `CONTRIBUTING.md`. Do not report security vulnerabilities through public issues; see `SECURITY.md`.

## Verification

Use the smallest verification that covers the changed surface:

| Change | Verification |
|--------|--------------|
| Native C++ / bindings | `.\repo.bat test --suite core --config release` |
| Export settings or SDK export flow | Small model export via plugin or harness; inspect USD |
| Plugin UI / commands | Build matching Revit year, install setup exe, smoke the ribbon command |
| Revit integration behavior | Elevated `.\repo.bat test --suite revit2024 --config release` (replace `2024`); requires `tests/inputs/<ver>/rac_basic/rac_basic_sample_project.rvt` |
| Concept mapping or docs | Check claims against `docs/concept_mapping.md`, `README.md`, and exported stage structure |
| Installer / packaging | Build release, run setup for affected Revit years |

Development quickstart from README:

```powershell
.\repo.bat --set-token revit_ver:2024 build --config release
.\repo.bat test --suite core --config release
```

## Output

When done, summarize changed files, relevant verification, and any build/test steps that could not be run locally.

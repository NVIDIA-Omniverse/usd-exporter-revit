---
name: revit-usd-export-getting-started
description: Use when a user asks to install, build, set up RevitDevKit, install the plugin, troubleshoot installation, or smoke-test the NVIDIA OpenUSD Exporter Plugin for Revit.
version: "0.1.0"
license: Apache-2.0 AND CC-BY-4.0
metadata:
  author: "NVIDIA Corporation"
  tags: [omniverse, openusd, revit, install, packaging]
tools: [Read, Shell]
---

# Getting Started - Revit USD Export

<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

## Purpose

Install and verify the NVIDIA OpenUSD Exporter Plugin for Revit for a target Revit version. Prefer a published NGC download for end-user install; use a local source build and the generated installer when changing this repository or when a published build is unavailable.

## When to Use

Use this module when the task asks to install, build, set up dependencies, install the add-in, or troubleshoot the plugin before running exports. Stop applying once the matching Revit version has the plugin installed and a first export or smoke check succeeds.

For export settings, concept mapping, or export troubleshooting, load `../revit-usd-export-conversion/SKILL.md`.

## Hard Rules

1. Require a licensed Autodesk Revit 2024, 2025, or 2026 installation before build or export smoke tests.
2. Build and test only for Revit versions installed on the machine: `2024`, `2025`, or `2026`.
3. For Revit 2024 source builds, developers must obtain Autodesk Revit 2024 SDK (RevitDevKit) themselves, uncomment `revitdevkit_2024` in `deps/target-deps.packman.xml`, and point it at that local SDK.
4. Do not accept Autodesk license terms, run silent Autodesk installers, or commit a user-specific RevitDevKit path on the user's behalf.
5. Do not commit machine-specific SDK paths, `_build` output, or proprietary Revit models.
6. Run Revit integration tests from an elevated command prompt after the matching Revit version is installed.
7. After install, smoke-test and real exports run inside live Revit via the Omniverse ribbon (see root skill Core Constraints).

## Prerequisites

Use `README.md` Requirements. Confirm the matching Revit year is installed before building or smoke-testing.

## Install From Published Download

After running the setup installer from GitHub, binaries live under `C:\Program Files\Omniverse\RevitUsdExportPlugin<year>\` and Revit loads the plugin via `C:\ProgramData\Autodesk\Revit\Addins\<year>\RevitUsdExportPlugin.addin`.

## Build And Install From Source

Follow `README.md` Getting Started. For RevitDevKit staging, `fetch_deps`, or contribution build notes, read `references/source-build.md`.

## Smoke Test

1. Confirm the matching Revit version launches.
2. Confirm the Omniverse ribbon / OpenUSD export commands appear.
3. Export a small 3D view to a local folder through the Windows file picker.
4. Confirm the output file exists under the chosen local folder.

Optional native and integration verification after a source build (see `README.md` Contribution Guidelines):

```powershell
.\repo.bat test --suite core --config release
.\repo.bat test --suite revit2024 --config release
```

Replace `2024` with the installed Revit version. Integration suites require elevated rights and the `rac_basic_sample_project.rvt` sample model under `tests/inputs/<ver>/rac_basic/`. See `tests/inputs/README.md` and `tests/LICENSE.md`.

## Next

After the plugin is installed and the smoke test passes, move to `../revit-usd-export-conversion/SKILL.md` for export settings, concept mapping, verification, and export troubleshooting.

## Limitations

- This module stops at build, plugin install, and smoke-test verification.
- It does not choose export settings or troubleshoot converted USD output; use the conversion module for that work.
- RevitDevKit setup for Revit 2024 requires the user to obtain the SDK and accept Autodesk license terms themselves.

## Troubleshooting

For build failures, missing RevitDevKit, installer/UAC issues, or missing ribbon commands, read `references/troubleshooting.md`.

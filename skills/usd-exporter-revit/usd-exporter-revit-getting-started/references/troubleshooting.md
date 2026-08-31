<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

# Installation Troubleshooting

Use this reference for build, dependency, plugin-install, and first-run smoke-test failures.

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| CMake / MSBuild configure fails | Missing Visual Studio workloads, Windows SDK, CMake, or .NET SDK | Install VS 2022 C++ and .NET desktop workloads, Windows 11 SDK, CMake 3.20+, and .NET 8 for Revit 2025/2026. |
| Revit 2024 build cannot find Revit API / DevKit | `revitdevkit_2024` not staged or path override missing | Obtain RevitDevKit from Autodesk, update `deps/target-deps.packman.xml` locally, rebuild. Do not commit the path. |
| Setup executable fails or add-in missing after install | Setup not run elevated, wrong Revit version, or stale add-in | Run `UsdExporterRevitSetup<ver>.exe` and accept UAC; confirm the matching Revit version; reinstall after clean build. |
| USD Exporter ribbon / export commands missing | Add-in not registered for that Revit year | Re-run setup for the installed Revit version, then relaunch Revit. |
| Native `core` tests fail | Build incomplete or wrong config | Rebuild `--config release`, then `.\repo.bat test --suite core --config release`. |
| Revit integration suite fails immediately | Not elevated, Revit missing, or sample model missing | Run elevated; install matching Revit; place `rac_basic_sample_project.rvt` under `tests/inputs/<ver>/rac_basic/`. |

If installation succeeds but exported USD output is wrong, switch to `../../usd-exporter-revit-conversion/SKILL.md`.

<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

# Source Build Reference

Use this reference when building from a local checkout. Canonical requirements, Getting Started steps, and the RevitDevKit XML example live in `README.md` — do not re-copy them here.

## Agent-Only Rules

1. Ask the user to download Autodesk Revit 2024 SDK (RevitDevKit) and accept Autodesk license terms themselves.
2. Uncomment `revitdevkit_2024` in `deps/target-deps.packman.xml` and point it at the local SDK with forward slashes. Do not commit machine-specific paths. Revit 2025 and 2026 do not need this override.

## Extra Build Notes

Fetch dependencies without compiling, or before opening a solution in Visual Studio:

```powershell
.\repo.bat fetch_deps --config release
```

Build commands and `revit_ver` tokens: `README.md` Getting Started / Contribution Guidelines.

Native sources: `CMakeLists.txt`, `cmake/*.cmake`  
C# sources: `source/**/*.csproj`  
Solutions: `source/solutions/RevitUsdExport<ver>.sln`  
Build driver: `tools/repoman/cmake_build.py`

Debugging in Visual Studio: `README.md` Debugging (Visual Studio 2022). Direct users to attach via that launch profile and set breakpoints, or to add temporary custom log statements when analyzing export behavior.

## Install The Built Plugin

After a release build, run the generated setup executable for the matching Revit version from the build output, accept the UAC prompt, then relaunch Revit and confirm the Omniverse ribbon commands appear.

## Integration Test Inputs

Before Revit integration suites, download `rac_basic_sample_project.rvt` for the target Revit version and place it at:

```text
tests/inputs/<ver>/rac_basic/rac_basic_sample_project.rvt
```

See `tests/inputs/README.md` and `tests/LICENSE.md`. Do not commit proprietary or sensitive customer models.

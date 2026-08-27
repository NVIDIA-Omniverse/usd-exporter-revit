# NVIDIA OpenUSD Exporter Plugin for Revit

Export Autodesk Revit models to OpenUSD for use in NVIDIA Omniverse and other applications in the OpenUSD ecosystem.

**Notice:** This project downloads and installs additional third-party open-source software. Review license terms for those projects before use.

# Overview

NVIDIA OpenUSD Exporter Plugin for Revit adds OpenUSD export workflows to Autodesk Revit on Windows. It supports single-view and batch exports, preserves Revit geometry and BIM data, and exposes an SDK for custom Revit-to-OpenUSD workflows.

Exports write to local folders selected through the standard Windows file picker.
Treat exported USD, textures, and plugin logs as sensitive: they can contain proprietary
geometry, element names, and optional BIM parameters.

## Components

### RevitUsdExportPlugin

- [App](source/RevitUsdExportPlugin/App.cs): Application-level Revit API integration and event handling.
- [Dialogs](source/RevitUsdExportPlugin.UI/Dialogs.cs): WPF settings and About dialogs.
- [Commands](source/RevitUsdExportPlugin/Commands/ExportCommands.cs): Commands connected to Revit ribbon actions.
- [External events](source/RevitUsdExportPlugin/EventHandler.cs): Out-of-process Revit interaction used primarily by test automation.
- [Storage](source/RevitUsdExportPlugin/ExtensibleStorage.cs): Export settings persisted to external files or Revit Extensible Storage.

### RevitUsdExportSdk

- [Exporter](source/RevitUsdExportSDK/Exporter.cs): Entry point for single-view and batch exports.
- [Settings](source/RevitUsdExportSDK/RevitUsdExportSettings.cs): Export and batch-export configuration.
- [Export context](source/RevitUsdExportSDK/ExportContext.cs): Revit `ExportContext` implementation that captures geometry and materials.
- [USD](source/RevitUsdExportSDK/Usd/): OpenUSD representation and authoring classes.
- [Revit](source/RevitUsdExportSDK/Revit/): Revit concepts and their OpenUSD mappings.
- [Managers](source/RevitUsdExportSDK/Managers/): Export and material management.

`RevitUsdExportSdk` contains the reusable export API and OpenUSD authoring logic. Its [native bindings](source/bindings/) connect the Revit API in C# to [OpenUSD](https://openusd.org/release/index.html) in C++.

### RevitUsdExportSetup

Installer and uninstaller for `RevitUsdExportPlugin.addin` across supported Revit versions.

### RevitTestHarness

Command-line test application that automates Revit over IPC to open models, apply export settings, run exports, and close models.

# Getting Started

1. Install a licensed copy of Revit 2024, 2025, or 2026.
2. Install build requirements listed below.
3. Clone this repository.
4. Build plugin for installed Revit version:

```powershell
.\repo.bat --set-token revit_ver:2024 build --config release
```

Replace `2024` with `2025` or `2026` as needed.

5. Run `RevitUsdExportSetup<Revit_Version>.exe` from release build output and accept UAC prompt.
6. Open a model in Revit and use Omniverse ribbon to export it.

# Requirements

- OS/architecture: Windows 10 or 11, x64.
- Host application: Licensed Autodesk Revit 2024, 2025, or 2026 installation.
- IDE/build tools: Visual Studio 2022 with **Desktop development with C++** and **.NET desktop development** workloads.
- Build system: [CMake](https://cmake.org/download/) 3.20 or newer on `PATH`.
- Native toolchain: MSVC v143 and Windows 11 SDK.
- Managed toolchain: .NET Framework 4.8 SDK and targeting pack.
- Revit 2025/2026: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Revit 2024: Autodesk Revit 2024 SDK (RevitDevKit), obtained separately from Autodesk.

For Revit 2024, uncomment `revitdevkit_2024` in `deps/target-deps.packman.xml` and set its source to your local SDK:

```xml
<dependency name="revitdevkit_2024" linkPath="../_build/target-deps/revitdevkit_2024">
    <source path="C:/path/to/your/RevitDevKit_2024" />
</dependency>
```

Do not commit machine-specific SDK paths. Revit 2025 and 2026 builds do not require this override.

# Usage

1. Open a Revit model.
2. Select export command from Omniverse ribbon.
3. Choose export settings and a private local output folder (avoid network shares, Public folders, and cloud-synced locations when handling proprietary models).
4. Export model as OpenUSD.

Per-model settings are stored in `%USERPROFILE%\Documents\Omniverse\Revit\<model name>\settings.json`, or inside Revit file when 1-Click Export is enabled.

Plugin logs are stored in:

```text
%USERPROFILE%\.revit_usd_export_plugin\logs\Revit-<revit version>\RevitUsdExportPlugin-<plugin version>\<YYYYMMDD_HHMMSS>.log
```

Logs may include full filesystem paths, document titles, and element names. Restrict access to this directory and share logs carefully.
- User documentation: [Revit documentation](https://docs.omniverse.nvidia.com/connect/latest/revit.html)
- OpenUSD documentation: [openusd.org](https://openusd.org/release/index.html)
- SDK entry point: [Exporter.cs](source/RevitUsdExportSDK/Exporter.cs)

## Releases & Roadmap

- Release history: [CHANGELOG.md](CHANGELOG.md)
- Published downloads: [NVIDIA NGC Catalog](https://catalog.ngc.nvidia.com/orgs/nvidia/teams/omniverse/resources/omni_revit_connector)

# Contribution Guidelines

- Start here: [CONTRIBUTING.md](CONTRIBUTING.md)
- Keep changes focused and add tests for affected behavior.
- Sign off commits using Developer Certificate of Origin instructions in `CONTRIBUTING.md`.

Development quickstart:

```powershell
git clone <repository-url>
cd revit-connector
.\repo.bat --set-token revit_ver:2024 build --config release
.\repo.bat test --suite core --config release
```

Before running Revit integration tests, download `rac_basic_sample_project.rvt` for your Revit version and place it in `tests/inputs/<ver>/rac_basic/`. See [tests/LICENSE.md](tests/LICENSE.md) and [tests/inputs/README.md](tests/inputs/README.md).

```powershell
.\repo.bat test --suite revit2024 --config release
```

Run Revit integration tests from an elevated command prompt. Replace `2024` with installed Revit version.

## Debugging (Visual Studio 2022)

Close Revit before rebuilding so plugin DLLs are not locked. Open `source/solutions/RevitUsdExport<ver>.sln`, set `RevitUsdExportPlugin<ver>` as the startup project, then configure an **Executable** launch profile:

1. Project properties → **Debug** → **General** → **Open debug launch profiles UI**.
2. Add a profile with **Executable** set to the matching Revit install, for example `C:\Program Files\Autodesk\Revit 2024\Revit.exe`. Enable **Native code debugging** (needed to step into native `revit_usd_export` / OpenUSD bindings).
3. Select that profile in the debug target dropdown and press **F5**.

## Governance & Maintainers

Project is maintained by NVIDIA. Repository owners review contributions and manage releases, issue triage, and project direction.

## Security

- Vulnerability disclosure: [SECURITY.md](SECURITY.md)
- Do not report security vulnerabilities through public issues.

## Support

- Level: Maintained.
- Product documentation: [Revit documentation](https://docs.omniverse.nvidia.com/connect/latest/revit.html)
- Report reproducible defects and feature requests through repository issue tracker.
- Include plugin version, Revit version, reproduction steps, and relevant logs.

# Community

Join NVIDIA Omniverse developer community through [NVIDIA Developer Forums](https://forums.developer.nvidia.com/c/omniverse/300).

# References

- [Autodesk Revit](https://www.autodesk.com/products/revit/overview)
- [OpenUSD](https://openusd.org/)
- [NVIDIA Omniverse](https://www.nvidia.com/en-us/omniverse/)
- [Revit connector documentation](https://docs.omniverse.nvidia.com/connect/latest/revit.html)

# License

This project is licensed under the Apache License, Version 2.0 and the Creative
Commons Attribution 4.0 International Public License. See `LICENSE.md` for details.

Third-party notices and license attributions are listed in
`Third_Party_Notices.md`.

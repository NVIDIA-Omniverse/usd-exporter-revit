# Contributing

Thank you for your interest in contributing to `usd-exporter-revit`.

This project accepts contributions through pull requests. Before opening a pull
request, make sure your change is focused, documented where appropriate, and
tested for the behavior it affects.

## Reporting Issues

Use issues for bug reports, feature requests, and documentation problems.
Include enough detail for maintainers to reproduce or understand the issue:

- Plugin version or commit.
- Windows and Autodesk Revit versions.
- Affected export command, model, view, and settings.
- Exact steps used to reproduce the issue.
- Error output, logs, or a short description of unexpected behavior.
- A minimal model when it can be shared.

Do not report security vulnerabilities through public issues. See `SECURITY.md`
for private disclosure instructions.

## Pull Requests

Pull requests should be scoped to a single logical change. Include a clear
description of the problem being solved and the approach taken.

Before submitting a pull request:

- Build every supported Revit version affected by the change.
- Run native unit tests and relevant Revit integration tests.
- Update `README.md`, `CHANGELOG.md`, or other documentation when behavior,
  requirements, settings, or supported versions change.
- Keep public SDK APIs backwards compatible unless the pull request
  intentionally proposes a breaking change.
- Do not commit machine-specific SDK paths, build output, Revit models
  containing sensitive data, or local environment files.

## Development Setup

Building requires Windows, Visual Studio 2022, MSVC v143, Windows 11 SDK,
CMake 3.20 or newer on `PATH`, .NET Framework 4.8 SDK and targeting pack, and
.NET 8 SDK. A licensed installation of the Autodesk Revit version being built is
also required.

Native C++ builds via **CMake**; C# plugin and test harness build from
checked-in `.csproj` / `.sln` files under `source/`. The `repo` tool
orchestrates dependency fetch, CMake configure/build/install, runtime
staging, and MSBuild/dotnet for C#.

Build from a clean checkout with a Revit version token:

```powershell
.\repo.bat --set-token revit_ver:2024 build --config release
```

Replace `2024` with `2025` or `2026` as needed.

To fetch packman dependencies without compiling (or before opening a solution in
Visual Studio):

```powershell
.\repo.bat fetch_deps --config release
```

Then open e.g. `source\solutions\UsdExporterRevit2025.sln`.

Native sources: `CMakeLists.txt`, `cmake/*.cmake`  
C# sources: `source/**/*.csproj`  
Build driver: `tools/repoman/cmake_build.py`

Revit 2024 builds also require Autodesk Revit 2024 SDK (RevitDevKit). Obtain it
from Autodesk, then edit `deps/target-deps.packman.xml` locally and point the
`revitdevkit_2024` dependency to the SDK:

```xml
<dependency name="revitdevkit_2024" linkPath="../_build/target-deps/revitdevkit_2024">
    <source path="C:/path/to/your/RevitDevKit_2024" />
</dependency>
```

Do not commit local SDK paths. Revit 2025 and 2026 builds do not require this
override.

## Testing

Run native tests after building:

```powershell
.\repo.bat test --suite core --config release
```

Run the integration suite matching the Revit version:

```powershell
.\repo.bat test --suite revit2024 --config release
.\repo.bat test --suite revit2025 --config release
.\repo.bat test --suite revit2026 --config release
```

Revit integration tests uninstall existing add-ins, install the newly built
add-in, automate Revit through `RevitTestHarness`, and uninstall the add-in.
Run these tests from an elevated command prompt. Only run suites for Revit
versions installed on the test machine.

Use small, source-control-friendly Revit models when adding test coverage. Do
not add proprietary or sensitive customer data.

## Signing Your Work

We require that all contributors sign off on their commits using the Developer
Certificate of Origin (DCO). This certifies that the contribution is your
original work, or that you have the right to submit it under this project's
license or a compatible license.

Contributions containing commits that are not signed off may not be accepted.
To sign off on a commit, use the `--signoff` or `-s` option:

```bash
git commit -s -m "Add export option"
```

This appends a line like this to your commit message:

```text
Signed-off-by: Your Name <your.email@example.com>
```

Full text of the DCO:

```text
Developer Certificate of Origin
Version 1.1

Copyright (C) 2004, 2006 The Linux Foundation and its contributors.

Everyone is permitted to copy and distribute verbatim copies of this license
document, but changing it is not allowed.

Developer's Certificate of Origin 1.1

By making a contribution to this project, I certify that:

(a) The contribution was created in whole or in part by me and I have the right
to submit it under the open source license indicated in the file; or

(b) The contribution is based upon previous work that, to the best of my
knowledge, is covered under an appropriate open source license and I have the
right under that license to submit that work with modifications, whether created
in whole or in part by me, under the same open source license (unless I am
permitted to submit under a different license), as indicated in the file; or

(c) The contribution was provided directly to me by some other person who
certified (a), (b) or (c) and I have not modified it.

(d) I understand and agree that this project and the contribution are public and
that a record of the contribution (including all personal information I submit
with it, including my sign-off) is maintained indefinitely and may be
redistributed consistent with this project or the open source license(s)
involved.
```

## Coding Guidelines

- Follow existing code style in files you edit.
- Keep changes narrowly scoped and avoid unrelated formatting churn.
- Add comments only where they clarify non-obvious Revit, interop, or OpenUSD
  behavior.
- Preserve license headers and third-party notices.
- Keep generated build output out of source control unless the repository
  explicitly tracks that artifact.
- Preserve valid OpenUSD identifiers, namespace structure, stage metadata, and
  composition behavior when changing export logic.

## License

By contributing, you agree that your contributions will be licensed under the
terms described in `LICENSE.md`.

# SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

import os
import subprocess

import build_config
import omni.repo.man

g_repo_folders = omni.repo.man.get_repo_paths()
g_repo_root = g_repo_folders["root"]
current_dir = g_repo_root.replace("/", "\\")
solutions_dir = os.path.join(current_dir, "source", "solutions")

# default path to dotnet executable
dotnet_exe_path = "C:\\Program Files\\dotnet\\dotnet.exe"
# AUTOREMOVE: BEGIN
if not os.path.exists(dotnet_exe_path):
    dotnet_exe_path = f'"{current_dir}\\_build\\host-deps\\dotnet8\\dotnet.exe"'
# AUTOREMOVE: END


def _intermediate_dir(project_name: str) -> str:
    return os.path.join(current_dir, "_build", "intermediate", project_name)


def dotnet_restore(revit_version):
    sln_path = os.path.join(solutions_dir, f"UsdExporterRevit{revit_version}.sln")
    cmd = f'"{dotnet_exe_path}" "restore" "{sln_path}"'
    print(f"Executing: {cmd}")

    # Sometimes dotnet restore does not output files correctly, so repeat the process until key output file is found
    intermediate_dir = _intermediate_dir(f"UsdExporterRevit{revit_version}")
    nuget_props_file = os.path.join(intermediate_dir, f"UsdExporterRevit{revit_version}.csproj.nuget.g.props")
    assets_file = os.path.join(intermediate_dir, "project.assets.json")

    max_retries = 10
    for attempt in range(1, max_retries + 1):
        print(f"dotnet restore attempt {attempt}/{max_retries}...")

        try:
            result = subprocess.run(cmd, shell=True, check=True, capture_output=True, text=True)
            print(result.stdout)

            if os.path.exists(nuget_props_file) or os.path.exists(assets_file):
                print(f"dotnet restore successful on attempt {attempt}")
                return True

            print("dotnet restore completed but output files not found. Retrying...")
            if attempt >= max_retries:
                print(f"Warning: dotnet restore completed {max_retries} times but output files still not found")
                return False

        except subprocess.CalledProcessError as e:
            print(f"Error restoring UsdExporterRevit{revit_version} on attempt {attempt}: {e}")
            print(f"Output: {e.stdout}")
            print(f"Error: {e.stderr}")

            if attempt >= max_retries:
                print(f"Failed to restore UsdExporterRevit{revit_version} after {max_retries} attempts")
                return False

    return False


def dotnet_build(revit_version, configuration):
    sln_path = os.path.join(solutions_dir, f"UsdExporterRevit{revit_version}.sln")
    msbuild_config = build_config.to_build_configuration(configuration)
    cmd = f'"{dotnet_exe_path}" "build" "{sln_path}" "/p:Configuration={msbuild_config}"'
    print(f"Executing: {cmd}")
    try:
        result = subprocess.run(cmd, shell=True, check=True, capture_output=True, text=True)
        print(result.stdout)
        return True
    except subprocess.CalledProcessError as e:
        print(f"Error building UsdExporterRevit{revit_version}: {e}")
        print(f"Output: {e.stdout}")
        print(f"Error: {e.stderr}")
        return False


def get_msbuild_location():
    """
    Locate MSBuild.exe from a Visual Studio 2022 installation to build the .NET Framework solutions.
    """
    vs_paths = [
        r"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        r"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        r"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    ]

    for path in vs_paths:
        if os.path.exists(path):
            print(f"Found MSBuild.exe at: {path}")
            return path

# AUTOREMOVE: BEGIN
    host_deps_path = os.path.join(current_dir, "_build", "host-deps", "msvc", "MSBuild", "Current", "Bin", "MSBuild.exe")
    if os.path.exists(host_deps_path):
        print(f"Found MSBuild.exe at: {host_deps_path}")
        return host_deps_path
# AUTOREMOVE: END
    return None


def msbuild_build(sln_path, configuration, project_name=None):
    msbuild_path = get_msbuild_location()
    if not msbuild_path:
        print(f"Error: Could not find MSBuild.exe to build the solution: {sln_path}")
        return False

    msbuild_config = build_config.to_build_configuration(configuration)
    if project_name:
        cmd = f'"{msbuild_path}" "{sln_path}" /restore /t:{project_name} ' f'"/p:Configuration={msbuild_config}" /p:BuildProjectReferences=false'
    else:
        cmd = f'"{msbuild_path}" "{sln_path}" /restore "/p:Configuration={msbuild_config}"'
    print(f"Executing: {cmd}")
    try:
        result = subprocess.run(cmd, shell=True, check=True, capture_output=True, text=True)
        print(result.stdout)
        return True
    except subprocess.CalledProcessError as e:
        print(f"Error building {sln_path}: {e}")
        print(f"Output: {e.stdout}")
        print(f"Error: {e.stderr}")
        return False


def build_csharp_solutions(configuration="release"):
    """Build checked-in C# solutions; outputs land in _build/ via Directory.Build.props."""

    revit_ver: str = omni.repo.man.resolve_tokens("${revit_ver}")
    project_name = f"UsdExporterRevitSetup{revit_ver}"
    print(f"Building usd-exporter-revit {revit_ver} {configuration}...")

    failures = []

    if not msbuild_build(f"{current_dir}\\source\\UsdExporterRevitSetup\\UsdExporterRevitSetup.sln", configuration, project_name):
        failures.append("UsdExporterRevitSetup.sln")

    if not msbuild_build(f"{solutions_dir}\\RevitTestHarness.sln", configuration):
        failures.append("RevitTestHarness.sln")

    if revit_ver == "2024":
        if not msbuild_build(f"{solutions_dir}\\UsdExporterRevit2024.sln", configuration):
            failures.append("UsdExporterRevit2024.sln")
    elif revit_ver == "2025":
        if not (dotnet_restore("2025") and dotnet_build("2025", configuration)):
            failures.append("UsdExporterRevit2025.sln")
    elif revit_ver == "2026":
        if not (dotnet_restore("2026") and dotnet_build("2026", configuration)):
            failures.append("UsdExporterRevit2026.sln")

    if failures:
        raise omni.repo.man.exceptions.QuietExpectedError(f"Build failed for: {', '.join(failures)} (configuration: {configuration})")

    print("All C# solutions built successfully")


def setup_repo_tool(parser, config):
    parser.prog = "build_solutions"
    parser.description = "Build all usd-exporter-revit C# solutions"

    parser.add_argument(
        "--configuration",
        "-c",
        choices=["debug", "release"],
        default="release",
        help="Build configuration: debug or release (default: release)",
    )

    def run_repo_tool(options, config):
        build_csharp_solutions(options.configuration)

    return run_repo_tool

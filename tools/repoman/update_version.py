# SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

from datetime import date
from pathlib import Path
import re

import repoman

repoman.bootstrap()

import omni.repo.man

g_repo_folders = omni.repo.man.get_repo_paths()
g_repo_root = g_repo_folders["root"]


def update_version_file_from_changelog(version_file):
    changelog_path = Path(g_repo_root) / "CHANGELOG.md"
    version_path = Path(g_repo_root) / version_file
    version_heading = re.compile(r"^#\s+(\d+\.\d+(?:\.\d+)?)\s*$")

    with changelog_path.open("r", encoding="utf-8") as file:
        for line in file:
            match = version_heading.match(line)
            if match:
                version = match.group(1)
                break
        else:
            raise ValueError(f"No release-version heading found in {changelog_path}")

    print(f"Updating {version_path} to version {version}")
    version_path.write_text(f"{version}\n", encoding="utf-8")


def assembly_copyright() -> str:
    end_year = date.today().year
    return f"Copyright (c) NVIDIA CORPORATION 2023-{end_year}"


def update_file_version(version, path):
    print(f"Updating {path} to version {version}")

    copyright_text = assembly_copyright()
    updated = []
    with open(path, "r", encoding="utf-8", newline="") as file:
        lines = file.readlines()
        newline = "\r\n" if lines and lines[0].endswith("\r\n") else "\n"
        for line in lines:
            # there is a comment in the cs file that has an example of setting "AssemblyVersion" and we want to skip that, so not line.startswith("//")
            if "AssemblyVersion" in line and not line.startswith("//"):
                updated.append(f'[assembly:AssemblyVersion("{version}.*")]{newline}')
            elif "AssemblyFileVersion" in line:
                updated.append(f'[assembly:AssemblyFileVersion("{version}")]{newline}')
            elif "AssemblyCopyright" in line:
                updated.append(f'[assembly:AssemblyCopyright("{copyright_text}")]{newline}')
            else:
                updated.append(line)
    with open(path, "w", encoding="utf-8", newline="") as file:
        file.writelines(updated)


def update_assemblyinfo_cs_files(version):
    plugin_path = f"{g_repo_root}/source/UsdExporterRevit/Properties/AssemblyInfo.cs"
    core_path = f"{g_repo_root}/source/UsdExporterRevitCore/Properties/AssemblyInfo.cs"
    update_file_version(version=version, path=plugin_path)
    update_file_version(version=version, path=core_path)


def setup_repo_tool(parser, config):
    parser.prog = "update_version"
    parser.description = "Updates VERSION.md and AssemblyInfo.cs files with the latest version in CHANGELOG.md"

    def run_repo_tool(options, config):
        version_file = config["repo"]["folders"]["version_file"]
        update_version_file_from_changelog(version_file)
        package_version = omni.repo.man.build_number.generate_build_number_from_file(version_file)
        version = package_version.split("+")[0]
        print(f"VERSION.md: {version}")
        update_assemblyinfo_cs_files(version=version)

    return run_repo_tool

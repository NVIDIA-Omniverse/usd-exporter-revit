# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
"""Fetch dependencies and build the native C++ targets with CMake, then build C# plugin solutions."""

import argparse
import os
import shutil
from typing import Callable, Dict

import repoman

repoman.bootstrap()

import build_config
import fetch_deps
import omni.repo.man
import stage_runtime


def _resolve_cmake_exe(root: str) -> str:
    """Prefer packman host-deps cmake; fall back to PATH."""
    exe_ext = omni.repo.man.resolve_tokens("${exe_ext}")
    cmake_exe = os.path.join(root, "_build", "host-deps", "cmake", "bin", f"cmake{exe_ext}")
    if os.path.exists(cmake_exe):
        return cmake_exe

    path_cmake = shutil.which(f"cmake{exe_ext}")
    if path_cmake:
        return path_cmake

    raise omni.repo.man.exceptions.QuietExpectedError("CMake was not found in _build/host-deps/cmake or on PATH. " "Install CMake and add its bin directory to PATH before building.")


def _preflight_cmake(root: str) -> str:
    """Fail fast if cmake is unavailable; log which binary will be used."""
    cmake_exe = _resolve_cmake_exe(root)
    omni.repo.man.logger.info(f"preflight: cmake -> {cmake_exe}")
    omni.repo.man.run_process([cmake_exe, "--version"], exit_on_error=True)
    return cmake_exe


def _clean_build_outputs(root: str, platform: str) -> None:
    """Remove build artifacts while preserving packman target-deps and host-deps."""
    for name in (
        "cmake",
        platform,
        "intermediate",
        "test",
        "unittest",
        "unsignedpackages",
        "signedpackages",
    ):
        path = os.path.join(root, "_build", name)
        if os.path.exists(path):
            omni.repo.man.logger.info(f"cmake: removing {path}")
            shutil.rmtree(path, ignore_errors=True)


def _clean_all_build(root: str) -> None:
    path = os.path.join(root, "_build")
    if os.path.exists(path):
        omni.repo.man.logger.info(f"cmake: removing {path}")
        shutil.rmtree(path, ignore_errors=True)


def setup_repo_tool(parser: argparse.ArgumentParser, config: Dict) -> Callable:
    toolConfig = config.get("repo_cmake", {})
    if not toolConfig.get("enabled", True):
        return None

    parser.description = "Fetch deps, build native C++ via CMake, then build C# plugin solutions."
    parser.add_argument(
        "--generate",
        action="store_true",
        help="configure only (emit compile_commands.json), then exit without compiling",
    )
    parser.add_argument(
        "--fetch-only",
        action="store_true",
        dest="fetch_only",
        help="only fetch dependencies (step 1) and stop",
    )
    parser.add_argument(
        "-x",
        "--rebuild",
        action="store_true",
        help="wipe build outputs (preserve packman deps), then build",
    )
    parser.add_argument(
        "--clean",
        action="store_true",
        help="wipe build outputs (preserve packman deps), then exit (no build)",
    )
    parser.add_argument(
        "--clean-deps",
        action="store_true",
        dest="clean_deps",
        help="with --clean or --rebuild, also remove _build/target-deps and _build/host-deps",
    )
    omni.repo.man.add_config_arg(parser)

    def run_repo_tool(options: argparse.Namespace, config: Dict):
        root = omni.repo.man.resolve_tokens("$root")
        repo = omni.repo.man.resolve_tokens("$root/repo${shell_ext}")
        platform = omni.repo.man.resolve_tokens("$platform")
        repo_config = omni.repo.man.resolve_tokens("$config")
        revit_ver = omni.repo.man.resolve_tokens("${revit_ver}")
        cmake_config = build_config.to_build_configuration(repo_config)

        output_dir = f"{root}/_build/{platform}/{repo_config}"
        build_dir = f"{root}/_build/cmake/{platform}/{repo_config}"

        if options.clean or options.rebuild:
            if options.clean_deps:
                _clean_all_build(root)
            else:
                _clean_build_outputs(root, platform)
        if options.clean:
            return

        fetch_deps.fetch_dependencies(config, repo_config)
        if options.fetch_only:
            omni.repo.man.logger.info(f"FETCH finished ({repo_config})")
            return

        omni.repo.man.run_process([repo, "update_version"], exit_on_error=True)

        usd_root = f"{root}/_build/target-deps/usd/{repo_config}"
        tbb_root = f"{root}/_build/target-deps/onetbb/{repo_config}"
        target_deps = f"{root}/_build/target-deps"
        cmake_exe = _preflight_cmake(root)

        configure = [
            cmake_exe,
            "-S",
            root,
            "-B",
            build_dir,
            "-G",
            "Visual Studio 17 2022",
            "-A",
            "x64",
            "-T",
            "v143",
            f"-DUSD_EXPORTER_REVIT_USD_ROOT={usd_root}",
            f"-DUSD_EXPORTER_REVIT_TBB_ROOT={tbb_root}",
            f"-DREVIT_VERSION={revit_ver}",
            "-DUSD_EXPORTER_REVIT_BUILD_TESTS=ON",
            f"-DUSD_EXPORTER_REVIT_DOCTEST_INCLUDE_DIR={target_deps}/doctest/include",
            f"-DPython3_ROOT_DIR={target_deps}/python",
            "-DPython3_FIND_STRATEGY=LOCATION",
        ]
        omni.repo.man.logger.info(" ".join(configure))
        omni.repo.man.run_process(configure, exit_on_error=True)

        if options.generate:
            return

        build = [
            cmake_exe,
            "--build",
            build_dir,
            "--config",
            cmake_config,
            "--",
            f"/m:{os.cpu_count() or 1}",
        ]
        omni.repo.man.logger.info(" ".join(build))
        omni.repo.man.run_process(build, exit_on_error=True)

        install = [
            cmake_exe,
            "--install",
            build_dir,
            "--config",
            cmake_config,
            "--prefix",
            output_dir,
        ]
        omni.repo.man.logger.info(" ".join(install))
        omni.repo.man.run_process(install, exit_on_error=True)

        stage_runtime.stage_python_runtime(root, repo_config)

        omni.repo.man.run_process(
            [repo, "--set-token", f"revit_ver:{revit_ver}", "build_solutions", "-c", repo_config],
            exit_on_error=True,
        )

    return run_repo_tool

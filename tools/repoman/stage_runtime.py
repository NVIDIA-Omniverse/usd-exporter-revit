# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
"""Post-install runtime staging that CMake install() cannot handle (packman junctions on Windows)."""

import argparse
import os
import shutil

import omni.repo.man


def stage_python_runtime(root: str, config: str) -> None:
    """Copy target-deps/python into lib/python-runtime, dereferencing junctions/symlinks."""
    platform = omni.repo.man.resolve_tokens("$platform")
    src = os.path.join(root, "_build", "target-deps", "python")
    dst = os.path.join(root, "_build", platform, config, "lib", "python-runtime")

    if not os.path.isdir(src):
        raise FileNotFoundError(f"Python runtime source not found: {src}")

    if os.path.exists(dst):
        shutil.rmtree(dst)

    shutil.copytree(src, dst, symlinks=False)
    omni.repo.man.logger.info(f"staged python-runtime: {src} -> {dst}")


def setup_repo_tool(parser: argparse.ArgumentParser, config: dict):
    parser.description = "Stage runtime files that CMake install cannot copy (packman junctions)."
    parser.add_argument("-c", "--config", default="release", choices=["release", "debug"])
    omni.repo.man.add_config_arg(parser)

    def run_repo_tool(options: argparse.Namespace, config: dict):
        root = omni.repo.man.resolve_tokens("$root")
        stage_python_runtime(root, options.config)

    return run_repo_tool

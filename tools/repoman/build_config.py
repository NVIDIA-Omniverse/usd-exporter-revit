# SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
#
"""Map repo.toml lower-case config tokens to MSBuild/CMake configuration names."""


def to_build_configuration(config: str) -> str:
    return {"debug": "Debug", "release": "Release"}.get(config.lower(), config)

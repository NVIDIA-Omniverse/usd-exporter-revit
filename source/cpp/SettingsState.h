// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include <string>
#include <unordered_map>

namespace revit::usd_export::core
{

//! Repo-owned configuration loaded from TOML/JSON with token and env resolution.
struct SettingsState
{
    std::string appName;
    std::string appVersion;
    std::string clientName;
    std::string clientVersion;
    std::string usdVersion;
    std::string startTimestamp;
    std::string buildConfig;
    std::string dataFolder;
    std::string logsFolder;
    std::string cacheFolder;
    std::string logFile;
    std::string logLevel;
    bool waitForDebugger = false;

    std::unordered_map<std::string, std::string> exportOptions;
    std::unordered_map<std::string, std::string> importOptions;

    std::string exportOption(const std::string& key) const;
    std::string importOption(const std::string& key) const;
};

//! Load or reload settings. When @p preserveStartTimestamp is true, keep the existing start timestamp.
bool loadSettingsState(SettingsState& state, bool preserveStartTimestamp);

SettingsState& mutableSettingsState();
const SettingsState& settingsState();

} // namespace revit::usd_export::core

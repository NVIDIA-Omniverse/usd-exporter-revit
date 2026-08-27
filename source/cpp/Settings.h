// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#include "ExportApi.h"

extern "C"
{

    //! Settings store static default values and global runtime state loaded from TOML/JSON.
    //! Tokens can be placed into strings for deferred resolution using `${token}` or `${env:VAR}` syntax.
    //! For example, `revit.usd.export.core.toml` configures the default logging via
    //! `log.file = "${logs}/${revit_usd_export_core_start_time}.log"`
    //! @{

    //! Initialize the Revit USD Export Plugin Settings.
    //!
    //! Client plugins may add or override core configuration via a toml file
    //! located at `<plugin_lib_dir>/config/revit.usd.export.client.toml`
    //!
    //! Note that `loadSettings` may be called multiple times, to reset settings
    //! back to the default configuration.
    //!
    //! @returns Whether the required settings were loaded successfully.
    REVIT_USD_EXPORT_API bool loadSettings();

    //! The name of the connected application. This should be the actual application name
    //! rather than the plugin name.
    static constexpr const char* kAppNameSetting = "/app/name";

    //! The version of the connected application. This should be the actual application version
    //! rather than the plugin version. It may be parsed for conditional logic, so must be
    //! a semver-like value (eg 19.5.0, 2023.1, etc).
    static constexpr const char* kAppVersionSetting = "/app/version";

    //! The name of the client plugin.
    static constexpr const char* kClientNameSetting = "/revit.usd.export.core/client/name";

    //! The version of the client plugin. This should be the plugin version rather than the
    //! actual application version. It may be parsed for conditional logic, so must be
    //! a semver-like value (eg 19.5.0, 2023.1, etc).
    static constexpr const char* kClientVersionSetting = "/revit.usd.export.core/client/version";

    /// @}

    //! @ingroup settings
    //! @{

    //! The name of the connected application (controlled via kAppNameSetting)
    static constexpr const char* kAppNameToken = "app_name";

    //! The version of the connected application (controlled via kAppVersionSetting)
    static constexpr const char* kAppVersionToken = "app_version";

    //! The name of the client plugin (controlled via kClientNameSetting)
    static constexpr const char* kClientNameToken = "client_name";

    //! The version of the client plugin (controlled via kClientVersionSetting)
    static constexpr const char* kClientVersionToken = "client_version";

    //! The USD version
    static constexpr const char* kUsdVersionToken = "usd_version";

    //! The timestamp indicating when `startup()` was first called.
    //! Note this timestamp will not be reset if `loadSettings()` is called multiple times.
    static constexpr const char* kStartTimeToken = "revit_usd_export_core_start_time";

    //! A per-user, per-client plugin system folder to store persistent data. This system folder is different for every OS user.
    //! The data folder is where an application can write anything that must reliably persist between sessions.
    static constexpr const char* kDataFolderToken = "data";

    //! A per user, per-client plugin system folder to be used for caching, which could be cleaned up at any time.
    //! An application should be able to rebuild the cache if it is missing.
    static constexpr const char* kCacheFolderToken = "cache";

    //! A per user, per-client plugin system folder to be used for logging.
    static constexpr const char* kLogFolderToken = "logs";

    //! The build configuration of this library. The resolved value is either "release" or "debug"
    static constexpr const char* kBuildConfigToken = "config";

    /// @}
}

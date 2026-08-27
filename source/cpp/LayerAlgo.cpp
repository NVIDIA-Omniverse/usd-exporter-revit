// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "LayerAlgo.h"

#include "Core.h"
#include "Log.h"
#include "SettingsState.h"

#include <pxr/usd/usd/stage.h>

#include <fmt/format.h>

using namespace pxr;

namespace revit::usd_export::core
{

static const std::string g_authoringKey = "creator";

bool hasLayerAuthoringMetadata(pxr::SdfLayerHandle layer)
{
    VtDictionary data = layer->GetCustomLayerData();
    return data.find(g_authoringKey) != data.end();
}

void setLayerAuthoringMetadata(pxr::SdfLayerHandle layer)
{
    if (!initialized())
    {
        REVIT_LOG_ERROR("`startup()` must succeed prior calling `setLayerAuthoringMetadata`");
        return;
    }

    const SettingsState& settings = settingsState();
    const std::string& appName = settings.appName;
    const std::string& appVersion = settings.appVersion;
    const std::string& clientName = settings.clientName;
    const std::string& clientVersion = settings.clientVersion;

    VtDictionary data = layer->GetCustomLayerData();
    data[g_authoringKey] = fmt::format("{0} {1} via {2} {3}", appName, appVersion, clientName, clientVersion);
    layer->SetCustomLayerData(data);
}

} // namespace revit::usd_export::core

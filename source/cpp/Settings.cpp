// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "Settings.h"

#include "Core.h"
#include "SettingsState.h"

extern "C"
{
    bool USD_EXPORTER_REVIT_API loadSettings()
    {
        const bool preserveStartTimestamp = initialized();
        return usd::exporter::revit::core::loadSettingsState(usd::exporter::revit::core::mutableSettingsState(), preserveStartTimestamp);
    }
}

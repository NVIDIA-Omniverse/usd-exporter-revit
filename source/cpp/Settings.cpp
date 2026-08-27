// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "Settings.h"

#include "Core.h"
#include "SettingsState.h"

extern "C"
{
    bool REVIT_USD_EXPORT_API loadSettings()
    {
        const bool preserveStartTimestamp = initialized();
        return revit::usd_export::core::loadSettingsState(revit::usd_export::core::mutableSettingsState(), preserveStartTimestamp);
    }
}

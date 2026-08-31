// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "logging.h"

#include "Log.h"

extern "C"
{
    USD_EXPORTER_REVIT_API void usd_exporter_revit_log_info(const char* message)
    {
        USD_EXPORTER_REVIT_LOG_INFO(message);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_log_warning(const char* message)
    {
        USD_EXPORTER_REVIT_LOG_WARN(message);
    }

    USD_EXPORTER_REVIT_API void usd_exporter_revit_log_error(const char* message)
    {
        USD_EXPORTER_REVIT_LOG_ERROR(message);
    }
}

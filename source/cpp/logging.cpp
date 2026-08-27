// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "logging.h"

#include "Log.h"

extern "C"
{
    REVIT_USD_EXPORT_API void revit_log_info(const char* message)
    {
        REVIT_LOG_INFO(message);
    }

    REVIT_USD_EXPORT_API void revit_log_warning(const char* message)
    {
        REVIT_LOG_WARN(message);
    }

    REVIT_USD_EXPORT_API void revit_log_error(const char* message)
    {
        REVIT_LOG_ERROR(message);
    }
}

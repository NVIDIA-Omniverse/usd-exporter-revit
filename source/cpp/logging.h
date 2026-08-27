// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

extern "C"
{
    /**
     * Log an info message.
     * @param message         The message to log.
     */
    REVIT_USD_EXPORT_API void revit_log_info(const char* message);

    /**
     * Log a warning message.
     * @param message         The message to log.
     */
    REVIT_USD_EXPORT_API void revit_log_warning(const char* message);

    /**
     * Log an error message.
     * @param message         The message to log.
     */
    REVIT_USD_EXPORT_API void revit_log_error(const char* message);
}

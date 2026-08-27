// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <cstdarg>

struct RevitLogChannel
{
    const char* name;
};

extern const RevitLogChannel kRevitUsdExportChannel;

namespace revit::usd_export::core::detail
{

enum class RevitLogLevel
{
    Verbose,
    Info,
    Warn,
    Error,
    Fatal,
};

void revitLogWriteV(RevitLogLevel level, const RevitLogChannel& channel, const char* format, va_list args);
const RevitLogChannel& revitLogDefaultChannel();
bool revitLogShouldLog(RevitLogLevel level);
void revitLogStartup();

inline void revitLogDispatch(RevitLogLevel level, const char* format, ...)
{
    if (!revitLogShouldLog(level))
    {
        return;
    }

    va_list args;
    va_start(args, format);
    revitLogWriteV(level, revitLogDefaultChannel(), format, args);
    va_end(args);
}

inline void revitLogDispatch(RevitLogLevel level, const RevitLogChannel& channel, const char* format, ...)
{
    if (!revitLogShouldLog(level))
    {
        return;
    }

    va_list args;
    va_start(args, format);
    revitLogWriteV(level, channel, format, args);
    va_end(args);
}

} // namespace revit::usd_export::core::detail

#define REVIT_LOG_IMPL(levelEnum, ...) ::revit::usd_export::core::detail::revitLogDispatch(::revit::usd_export::core::detail::RevitLogLevel::levelEnum, __VA_ARGS__)

#define REVIT_LOG_VERBOSE(...) REVIT_LOG_IMPL(Verbose, __VA_ARGS__)
#define REVIT_LOG_INFO(...) REVIT_LOG_IMPL(Info, __VA_ARGS__)
#define REVIT_LOG_WARN(...) REVIT_LOG_IMPL(Warn, __VA_ARGS__)
#define REVIT_LOG_ERROR(...) REVIT_LOG_IMPL(Error, __VA_ARGS__)
#define REVIT_LOG_FATAL(...) REVIT_LOG_IMPL(Fatal, __VA_ARGS__)

extern "C"
{
    /**
     * Initialize the Revit USD Export logging system.
     */
    REVIT_USD_EXPORT_API void revit_usd_export_core_startupLog();
}

// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <cstdarg>

struct UsdExporterRevitLogChannel
{
    const char* name;
};

extern const UsdExporterRevitLogChannel kUsdExporterRevitChannel;

namespace usd::exporter::revit::core::detail
{

enum class UsdExporterRevitLogLevel
{
    Verbose,
    Info,
    Warn,
    Error,
    Fatal,
};

void usdExporterRevitLogWriteV(UsdExporterRevitLogLevel level, const UsdExporterRevitLogChannel& channel, const char* format, va_list args);
const UsdExporterRevitLogChannel& usdExporterRevitLogDefaultChannel();
bool usdExporterRevitLogShouldLog(UsdExporterRevitLogLevel level);
void usdExporterRevitLogStartup();

inline void usdExporterRevitLogDispatch(UsdExporterRevitLogLevel level, const char* format, ...)
{
    if (!usdExporterRevitLogShouldLog(level))
    {
        return;
    }

    va_list args;
    va_start(args, format);
    usdExporterRevitLogWriteV(level, usdExporterRevitLogDefaultChannel(), format, args);
    va_end(args);
}

inline void usdExporterRevitLogDispatch(UsdExporterRevitLogLevel level, const UsdExporterRevitLogChannel& channel, const char* format, ...)
{
    if (!usdExporterRevitLogShouldLog(level))
    {
        return;
    }

    va_list args;
    va_start(args, format);
    usdExporterRevitLogWriteV(level, channel, format, args);
    va_end(args);
}

} // namespace usd::exporter::revit::core::detail

#define USD_EXPORTER_REVIT_LOG_IMPL(levelEnum, ...) ::usd::exporter::revit::core::detail::usdExporterRevitLogDispatch(::usd::exporter::revit::core::detail::UsdExporterRevitLogLevel::levelEnum, __VA_ARGS__)

#define USD_EXPORTER_REVIT_LOG_VERBOSE(...) USD_EXPORTER_REVIT_LOG_IMPL(Verbose, __VA_ARGS__)
#define USD_EXPORTER_REVIT_LOG_INFO(...) USD_EXPORTER_REVIT_LOG_IMPL(Info, __VA_ARGS__)
#define USD_EXPORTER_REVIT_LOG_WARN(...) USD_EXPORTER_REVIT_LOG_IMPL(Warn, __VA_ARGS__)
#define USD_EXPORTER_REVIT_LOG_ERROR(...) USD_EXPORTER_REVIT_LOG_IMPL(Error, __VA_ARGS__)
#define USD_EXPORTER_REVIT_LOG_FATAL(...) USD_EXPORTER_REVIT_LOG_IMPL(Fatal, __VA_ARGS__)

extern "C"
{
    /**
     * Initialize the usd-exporter-revit logging system.
     */
    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_startupLog();
}

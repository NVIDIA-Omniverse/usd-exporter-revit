// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "Log.h"

#include "SettingsState.h"

#include <array>
#include <chrono>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <mutex>
#include <sstream>
#include <string>

#if defined(_WIN32)
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

const UsdExporterRevitLogChannel kUsdExporterRevitChannel = { "usd.exporter.revit.core" };

namespace
{

using UsdExporterRevitLogLevel = usd::exporter::revit::core::detail::UsdExporterRevitLogLevel;

static constexpr size_t g_logMessageBufferSize = 4096;

std::mutex g_logMutex;
std::ofstream g_logFile;
UsdExporterRevitLogLevel g_minLogLevel = UsdExporterRevitLogLevel::Info;
bool g_logFileInitialized = false;

const char* levelToString(usd::exporter::revit::core::detail::UsdExporterRevitLogLevel level)
{
    switch (level)
    {
        case usd::exporter::revit::core::detail::UsdExporterRevitLogLevel::Verbose:
            return "Verbose";
        case usd::exporter::revit::core::detail::UsdExporterRevitLogLevel::Info:
            return "Info";
        case usd::exporter::revit::core::detail::UsdExporterRevitLogLevel::Warn:
            return "Warn";
        case usd::exporter::revit::core::detail::UsdExporterRevitLogLevel::Error:
            return "Error";
        case usd::exporter::revit::core::detail::UsdExporterRevitLogLevel::Fatal:
            return "Fatal";
        default:
            return "Unknown";
    }
}

UsdExporterRevitLogLevel parseLogLevel(const std::string& levelName)
{
    std::string normalized = levelName;
    for (char& ch : normalized)
    {
        ch = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
    }

    if (normalized == "verbose")
    {
        return UsdExporterRevitLogLevel::Verbose;
    }
    if (normalized == "info")
    {
        return UsdExporterRevitLogLevel::Info;
    }
    if (normalized == "warn" || normalized == "warning")
    {
        return UsdExporterRevitLogLevel::Warn;
    }
    if (normalized == "error")
    {
        return UsdExporterRevitLogLevel::Error;
    }
    if (normalized == "fatal")
    {
        return UsdExporterRevitLogLevel::Fatal;
    }

    return UsdExporterRevitLogLevel::Info;
}

void applyLogLevelFromSettings()
{
    const std::string& levelName = usd::exporter::revit::core::settingsState().logLevel;
    if (!levelName.empty())
    {
        g_minLogLevel = parseLogLevel(levelName);
    }
}

std::string currentTimestamp()
{
    const auto now = std::chrono::system_clock::now();
    const std::time_t nowTime = std::chrono::system_clock::to_time_t(now);
    std::tm localTime{};
#if defined(_WIN32)
    localtime_s(&localTime, &nowTime);
#else
    localtime_r(&nowTime, &localTime);
#endif

    std::ostringstream stream;
    stream << std::put_time(&localTime, "%Y-%m-%dT%H:%M:%S");
    return stream.str();
}

bool createParentDirectories(const std::filesystem::path& filePath)
{
    const std::filesystem::path parentPath = filePath.parent_path();
    if (parentPath.empty())
    {
        return true;
    }

#if defined(_WIN32)
    std::error_code errorCode;
    if (std::filesystem::exists(parentPath, errorCode))
    {
        return std::filesystem::is_directory(parentPath, errorCode);
    }

    std::wstring widePath = parentPath.wstring();
    if (widePath.empty())
    {
        return false;
    }

    for (size_t index = 0; index < widePath.size(); ++index)
    {
        if (widePath[index] == L'/' || widePath[index] == L'\\')
        {
            const std::wstring partialPath = widePath.substr(0, index);
            if (!partialPath.empty() && partialPath.back() != L':')
            {
                CreateDirectoryW(partialPath.c_str(), nullptr);
            }
        }
    }

    return CreateDirectoryW(widePath.c_str(), nullptr) != 0 || GetLastError() == ERROR_ALREADY_EXISTS;
#else
    std::error_code errorCode;
    std::filesystem::create_directories(parentPath, errorCode);
    return !errorCode;
#endif
}

bool openLogFile(const std::string& logFilePath)
{
    if (logFilePath.empty())
    {
        return false;
    }

    const std::filesystem::path filePath(logFilePath);
    if (!createParentDirectories(filePath))
    {
        return false;
    }

    g_logFile.open(filePath, std::ios::out | std::ios::app);
    return g_logFile.is_open();
}

void ensureLogFileOpen()
{
    if (g_logFileInitialized)
    {
        return;
    }

    const std::string& logFilePath = usd::exporter::revit::core::settingsState().logFile;
    if (logFilePath.empty())
    {
        return;
    }

    if (openLogFile(logFilePath))
    {
        g_logFileInitialized = true;
    }
}

void writeToOutputs(const std::string& formattedMessage)
{
    std::fputs(formattedMessage.c_str(), stdout);
    std::fputc('\n', stdout);
    std::fflush(stdout);

#if defined(_WIN32)
    OutputDebugStringA(formattedMessage.c_str());
    OutputDebugStringA("\n");
#endif
}

std::string formatLogLine(usd::exporter::revit::core::detail::UsdExporterRevitLogLevel level, const char* channelName, const char* messageText)
{
    std::array<char, g_logMessageBufferSize> lineBuffer{};
    std::snprintf(lineBuffer.data(), lineBuffer.size(), "[%s] [%s] [%s] %s", currentTimestamp().c_str(), levelToString(level), channelName, messageText);
    return lineBuffer.data();
}

} // namespace

namespace usd::exporter::revit::core::detail
{

bool usdExporterRevitLogShouldLog(UsdExporterRevitLogLevel level)
{
    return static_cast<int>(level) >= static_cast<int>(g_minLogLevel);
}

const UsdExporterRevitLogChannel& usdExporterRevitLogDefaultChannel()
{
    return kUsdExporterRevitChannel;
}

void usdExporterRevitLogWriteV(UsdExporterRevitLogLevel level, const UsdExporterRevitLogChannel& channel, const char* format, va_list args)
{
    if (!usdExporterRevitLogShouldLog(level))
    {
        return;
    }

    std::array<char, g_logMessageBufferSize> messageBuffer{};
    va_list argsCopy;
    va_copy(argsCopy, args);
    const int formattedLength = std::vsnprintf(messageBuffer.data(), messageBuffer.size(), format, argsCopy);
    va_end(argsCopy);

    const char* messageText = messageBuffer.data();
    std::string ownedMessage;
    if (formattedLength < 0)
    {
        messageText = format;
    }
    else if (static_cast<size_t>(formattedLength) >= messageBuffer.size())
    {
        ownedMessage.resize(static_cast<size_t>(formattedLength) + 1);
        std::vsnprintf(ownedMessage.data(), ownedMessage.size(), format, args);
        messageText = ownedMessage.c_str();
    }

    const char* channelName = channel.name ? channel.name : "usd.exporter.revit.core";
    const std::string formattedMessage = formatLogLine(level, channelName, messageText);

    std::lock_guard<std::mutex> lock(g_logMutex);
    ensureLogFileOpen();
    writeToOutputs(formattedMessage);

    if (g_logFile.is_open())
    {
        g_logFile << formattedMessage << std::endl;
        g_logFile.flush();
    }
}

void usdExporterRevitLogStartup()
{
    std::lock_guard<std::mutex> lock(g_logMutex);

    applyLogLevelFromSettings();

    if (g_logFile.is_open())
    {
        g_logFileInitialized = true;
        return;
    }

    const std::string& logFilePath = usd::exporter::revit::core::settingsState().logFile;
    if (logFilePath.empty())
    {
        return;
    }

    if (openLogFile(logFilePath))
    {
        g_logFileInitialized = true;
    }
    else
    {
        const std::string warning = formatLogLine(UsdExporterRevitLogLevel::Warn, kUsdExporterRevitChannel.name, ("Failed to open log file '" + logFilePath + "'; file logging disabled, retrying on next log.").c_str());
        writeToOutputs(warning);
    }
}

} // namespace usd::exporter::revit::core::detail

extern "C"
{
    USD_EXPORTER_REVIT_API void usd_exporter_revit_core_startupLog()
    {
        usd::exporter::revit::core::detail::usdExporterRevitLogStartup();

        USD_EXPORTER_REVIT_LOG_VERBOSE("Initialized [%s] logging channel", kUsdExporterRevitChannel.name);
    }
}

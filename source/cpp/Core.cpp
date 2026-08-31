// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "Core.h"

#include "Log.h"
#include "Settings.h"

#include "SettingsState.h"

#if defined(_WIN32)
#include <conio.h>
#include <windows.h>
#else
#include <pwd.h>
#include <unistd.h>
#endif

#include <pxr/usd/usdGeom/metrics.h>

#include <algorithm>
#include <chrono>
#include <clocale>
#include <cstdlib>
#include <filesystem>
#include <locale>
#include <string>
#include <thread>

PXR_NAMESPACE_USING_DIRECTIVE

static bool g_initialized = false;
static constexpr const char* g_emptyString = "";

namespace
{

bool isDebuggerAttached()
{
#if defined(_WIN32)
    return IsDebuggerPresent() != FALSE;
#else
    return false;
#endif
}

void waitForDebugger()
{
#if defined(_WIN32)
    const DWORD processId = GetCurrentProcessId();
#else
    const int processId = getpid();
#endif
    printf("[usd.exporter.revit.core] Waiting for debugger to attach, press the Enter key to skip... [pid: %d]\n", processId);
    fflush(stdout);

    auto getKeyPress = []() -> bool
    {
#if defined(_WIN32)
        if (_kbhit())
        {
            _getch();
            return true;
        }
        return false;
#elif defined(__unix__) || defined(__APPLE__)
        struct timespec req = { 0, 0 };
        fd_set fds;
        FD_ZERO(&fds);
        FD_SET(STDIN_FILENO, &fds);
        pselect(STDIN_FILENO + 1, &fds, NULL, NULL, &req, NULL);
        if (FD_ISSET(STDIN_FILENO, &fds))
        {
            getchar();
            return true;
        }
        return false;
#else
        return false;
#endif
    };

    while (!isDebuggerAttached() && !getKeyPress())
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

} // namespace

extern "C"
{
    USD_EXPORTER_REVIT_API bool initialized()
    {
        return g_initialized;
    }

    USD_EXPORTER_REVIT_API bool usd_exporter_revit_core_startup()
    {
        static constexpr char localeName[] = "ja_JP.utf-8";
        std::setlocale(LC_ALL, localeName);
        std::locale::global(std::locale(localeName));

        if (g_initialized)
        {
            return true;
        }

        if (!loadSettings())
        {
            return false;
        }

        usd_exporter_revit_core_startupLog();

        if (usd::exporter::revit::core::settingsState().waitForDebugger)
        {
            waitForDebugger();
        }

        g_initialized = true;

        return initialized();
    }

    USD_EXPORTER_REVIT_API const char* usd_exporter_revit_install_path()
    {
        static std::string s_appPath;
        if (!s_appPath.empty())
        {
            return s_appPath.c_str();
        }

#if defined(_WIN32)
        HMODULE module = nullptr;
        if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, reinterpret_cast<LPCWSTR>(&usd_exporter_revit_install_path), &module))
        {
            wchar_t modulePath[MAX_PATH] = {};
            const DWORD length = GetModuleFileNameW(module, modulePath, MAX_PATH);
            if (length > 0 && length < MAX_PATH)
            {
                s_appPath = std::filesystem::path(modulePath).parent_path().string();
            }
        }
#endif

        if (s_appPath.empty())
        {
            USD_EXPORTER_REVIT_LOG_FATAL("Cannot resolve plugin install path from module location.");
            return g_emptyString;
        }

        std::replace(s_appPath.begin(), s_appPath.end(), '\\', '/');
        return s_appPath.c_str();
    }

    USD_EXPORTER_REVIT_API double usd_exporter_revit_getGeomLinearUnits(const char* name)
    {
        const std::string metricsName = pxr::TfStringToLower(name);

        if (metricsName == "centimeters")
        {
            return pxr::UsdGeomLinearUnits::centimeters;
        }
        else if (metricsName == "feet")
        {
            return pxr::UsdGeomLinearUnits::feet;
        }
        else if (metricsName == "inches")
        {
            return pxr::UsdGeomLinearUnits::inches;
        }
        else if (metricsName == "kilometers")
        {
            return pxr::UsdGeomLinearUnits::kilometers;
        }
        else if (metricsName == "lightyears")
        {
            return pxr::UsdGeomLinearUnits::lightYears;
        }
        else if (metricsName == "meters")
        {
            return pxr::UsdGeomLinearUnits::meters;
        }
        else if (metricsName == "micrometers")
        {
            return pxr::UsdGeomLinearUnits::micrometers;
        }
        else if (metricsName == "miles")
        {
            return pxr::UsdGeomLinearUnits::miles;
        }
        else if (metricsName == "millimeters")
        {
            return pxr::UsdGeomLinearUnits::millimeters;
        }
        else if (metricsName == "nanometers")
        {
            return pxr::UsdGeomLinearUnits::nanometers;
        }
        else if (metricsName == "yards")
        {
            return pxr::UsdGeomLinearUnits::yards;
        }

        USD_EXPORTER_REVIT_LOG_WARN("getGeomLinearUnits -> The specified unit name cannot be found: %s", name);
        return 0.0;
    }
}

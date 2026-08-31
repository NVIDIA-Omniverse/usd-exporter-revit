// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace usd.exporter.revit
{
public class log
{
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_log_info", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_log_info(byte[] message);

    public static void info(string message, [CallerFilePath] string file = "", [CallerMemberName] string methodName = "")
    {
        file = file.Substring(file.LastIndexOf('\\') + 1);
        usd_exporter_revit_log_info(Encoding.UTF8.GetBytes($"[{file}] [{methodName}] {message}"));
    }

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_log_warning", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_log_warning(byte[] message);

    public static void warning(string message, [CallerFilePath] string file = "", [CallerMemberName] string methodName = "")
    {
        file = file.Substring(file.LastIndexOf('\\') + 1);
        usd_exporter_revit_log_warning(Encoding.UTF8.GetBytes($"[{file}] [{methodName}] {message}"));
    }

    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_log_error", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void usd_exporter_revit_log_error(byte[] message);

    public static void error(string message, [CallerFilePath] string file = "", [CallerMemberName] string methodName = "")
    {
        file = file.Substring(file.LastIndexOf('\\') + 1);
        usd_exporter_revit_log_error(Encoding.UTF8.GetBytes($"[{file}] [{methodName}] {message}"));
    }
}
}

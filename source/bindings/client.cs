// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace usd.exporter.revit.file
{
public class client
{
    [DllImport("usd_exporter_revit", EntryPoint = "usd_exporter_revit_file_client_is_local_uri", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool usd_exporter_revit_file_client_is_local_uri(byte[] uri);
    public static bool isLocalUri(string uri)
    {
        return usd_exporter_revit_file_client_is_local_uri(Encoding.UTF8.GetBytes(uri));
    }
}
}

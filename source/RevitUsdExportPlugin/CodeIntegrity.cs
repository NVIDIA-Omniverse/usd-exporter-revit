// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Autodesk.Revit.UI;

namespace RevitUsdExportPlugin
{
/// <summary>
/// Authenticode integrity gate for Program Files installs (skipped for local/dev trees).
/// Uses WinVerifyTrust so the PE digest is checked against the embedded signature,
/// then asserts an NVIDIA publisher subject.
/// </summary>
internal static class CodeIntegrity
{
#if REV2026
    private const int RevitYear = 2026;
#elif REV2025
    private const int RevitYear = 2025;
#elif REV2024
    private const int RevitYear = 2024;
#endif

    private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
    private static readonly Guid WintrustActionGenericVerifyV2 =
        new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint WtdCacheOnlyUrlRetrieval = 0x1000;
    private const uint WtdDisableMd2Md4 = 0x2000;

    public static bool VerifyBeforeNativeStartup()
    {
        string skip = Environment.GetEnvironmentVariable("REVIT_USD_EXPORT_SKIP_CODE_INTEGRITY");
        if (!string.IsNullOrWhiteSpace(skip)
            && (skip.Trim() == "1" || skip.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(pluginDir) || !IsUnderProgramFiles(pluginDir))
        {
            return true;
        }

        string[] files =
        {
            Path.Combine(pluginDir, $"RevitUsdExportPlugin{RevitYear}.dll"),
            Path.Combine(pluginDir, $"RevitUsdExportSDK{RevitYear}.dll"),
            Path.Combine(pluginDir, "revit_usd_export.dll"),
        };

        foreach (string path in files)
        {
            string error = VerifySignedFile(path);
            if (error != null)
            {
                TaskDialog.Show(
                    "NVIDIA OpenUSD Exporter Plugin",
                    "Plugin binaries failed Authenticode verification.\n\n" + error
                    + "\n\nReinstall under Program Files, or set REVIT_USD_EXPORT_SKIP_CODE_INTEGRITY=1 for local testing.");
                return false;
            }
        }

        return true;
    }

    private static bool IsUnderProgramFiles(string path)
    {
        string full = Path.GetFullPath(path);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return StartsWithPath(full, pf) || StartsWithPath(full, pf86);
    }

    private static bool StartsWithPath(string fullPath, string root)
    {
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }
        string rootFull = Path.GetFullPath(root).TrimEnd('\\', '/');
        fullPath = Path.GetFullPath(fullPath);
        return fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.Equals(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static string VerifySignedFile(string path)
    {
        if (!File.Exists(path))
        {
            return $"Missing: {path}";
        }

        int trust = WinVerifyTrustFile(path);
        if (trust != 0)
        {
            return $"Authenticode trust failed (0x{trust:X8}): {path}";
        }

        // Digest already verified; extract signer only to assert publisher identity.
        X509Certificate2 cert;
        try
        {
            cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
        }
        catch (Exception ex)
        {
            return $"No Authenticode signer cert: {path} ({ex.Message})";
        }

        using (cert)
        {
            if (cert.Subject == null || cert.Subject.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return $"Signer is not NVIDIA: {path}";
            }
        }

        return null;
    }

    private static int WinVerifyTrustFile(string filePath)
    {
        IntPtr filePathPtr = Marshal.StringToCoTaskMemUni(filePath);
        IntPtr fileInfoPtr = IntPtr.Zero;
        IntPtr dataPtr = IntPtr.Zero;
        try
        {
            var fileInfo = new WinTrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo)),
                pcwszFilePath = filePathPtr,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };
            fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustFileInfo)));
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var data = new WinTrustData
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData)),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WtdUiNone,
                fdwRevocationChecks = WtdRevokeNone,
                dwUnionChoice = WtdChoiceFile,
                pFile = fileInfoPtr,
                dwStateAction = WtdStateActionVerify,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = WtdCacheOnlyUrlRetrieval | WtdDisableMd2Md4,
                dwUIContext = 0,
                pSignatureSettings = IntPtr.Zero,
            };
            dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustData)));
            Marshal.StructureToPtr(data, dataPtr, false);

            IntPtr actionPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid)));
            try
            {
                Marshal.StructureToPtr(WintrustActionGenericVerifyV2, actionPtr, false);
                int result = NativeMethods.WinVerifyTrust(InvalidHandleValue, actionPtr, dataPtr);

                // Preserve hWVTStateData written by VERIFY, then release provider state.
                data = (WinTrustData)Marshal.PtrToStructure(dataPtr, typeof(WinTrustData));
                data.dwStateAction = WtdStateActionClose;
                Marshal.StructureToPtr(data, dataPtr, false);
                NativeMethods.WinVerifyTrust(InvalidHandleValue, actionPtr, dataPtr);

                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(actionPtr);
            }
        }
        finally
        {
            if (dataPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dataPtr);
            }
            if (fileInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(fileInfoPtr);
            }
            if (filePathPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(filePathPtr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    private static class NativeMethods
    {
        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        public static extern int WinVerifyTrust(IntPtr hwnd, IntPtr pgActionID, IntPtr pWVTData);
    }
}
}

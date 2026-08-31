// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace usd.exporter.revit
{
public class stringutil
{
    // Countermeasures against UTF-8 garbled characters in Windows environment.
    [DllImport("usd_exporter_revit", EntryPoint = "stringutil_getRawData", ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
    public static extern void stringutil_getRawData(IntPtr ptr, out IntPtr data, out int size);

    // Correctly return strings received from C as C# UTF-8.
    // @param[in] ptr   const char* in C language.
    // @return Returns strings containing UTF-8.
    public static string convertUTF8String(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string retString = "";
            IntPtr data = IntPtr.Zero;
            int size = 0;
            stringutil_getRawData(ptr, out data, out size);
            if (data != IntPtr.Zero && size > 0)
            {
                var buffer = new byte[size];
                Marshal.Copy(data, buffer, 0, size);
                retString = Encoding.UTF8.GetString(buffer);
            }
            return retString;
        }
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr);
    }
}

// A class that converts a string array to an IntPtr.
// This is necessary to pass UTF-8 string arrays correctly to C/C++.
// This class allocates temporary memory that will be freed when it goes out of scope.
public class StringArrayToIntPtr
{
    private GCHandle[] _handleArray = null;
    private IntPtr _intPtr = IntPtr.Zero;

    public StringArrayToIntPtr()
    {
        _handleArray = null;
        _intPtr = IntPtr.Zero;
    }

    ~StringArrayToIntPtr()
    {
        FreeHandles();
    }

    private void FreeHandles()
    {
        if (_handleArray != null)
        {
            for (int i = 0; i < _handleArray.Length; ++i)
            {
                _handleArray[i].Free();
            }
            _handleArray = null;
        }

        if (_intPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_intPtr);
            _intPtr = IntPtr.Zero;
        }
    }

    // Convert string[] to IntPtr (byte[][]).
    // It is used when passing it as an argument to getValidPrimNames, getValidAttributeNames etc.
    // @param[in] _stringArray  Array of strings.
    // @return Two-dimensional byte array.This uses an IntPtr.
    public IntPtr ConvertStringArrayToBytesArray(string[] _stringArray)
    {
        FreeHandles();
        if (_stringArray == null || _stringArray.Length == 0)
            return IntPtr.Zero;

        IntPtr[] ptrArray = new IntPtr[_stringArray.Length];
        _handleArray = new GCHandle[_stringArray.Length];
        for (int i = 0; i < _stringArray.Length; ++i)
        {
            var buffer = Encoding.UTF8.GetBytes(_stringArray[i]);
            _handleArray[i] = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptrArray[i] = _handleArray[i].AddrOfPinnedObject();
        }

        int size = Marshal.SizeOf(typeof(IntPtr)) * ptrArray.Length;
        _intPtr = Marshal.AllocHGlobal(size);
        Marshal.Copy(ptrArray, 0, _intPtr, ptrArray.Length);

        return _intPtr;
    }
}
}

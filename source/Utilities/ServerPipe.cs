// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.IO.Pipes;
#if NETFRAMEWORK
using System.Security.AccessControl;
using System.Security.Principal;
#endif

namespace UsdExporterRevit.Utilities
{
public class ServerPipe : ABasicPipe
{
    protected NamedPipeServerStream serverPipeStream;
    protected string PipeName { get; set; }

    public ServerPipe(string pipeName, Action<ABasicPipe> asyncReaderStart)
    {
        this.asyncReaderStart = asyncReaderStart;
        PipeName = pipeName;

#if NETFRAMEWORK
        // Restrict pipe to the current Windows user (test harness is net48).
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
        serverPipeStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 0, 0, pipeSecurity);
#else
        serverPipeStream = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
#if REV2025 || REV2026
            OperatingSystem.IsWindows() ? PipeTransmissionMode.Message : PipeTransmissionMode.Byte,
#else
            PipeTransmissionMode.Message,
#endif
            PipeOptions.Asynchronous
        );
#endif

        pipeStream = serverPipeStream;
    }

    public void BeginConnect()
    {
        serverPipeStream.BeginWaitForConnection(new AsyncCallback(Connected), null);
    }

    protected void Connected(IAsyncResult ar)
    {
        serverPipeStream.EndWaitForConnection(ar);
        asyncReaderStart(this);
        Connected();
    }
}
}

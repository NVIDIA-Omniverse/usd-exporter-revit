// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUsdExport.Utilities
{
public class ClientPipe : ABasicPipe
{
    protected NamedPipeClientStream clientPipeStream;

    public ClientPipe(string serverName, string pipeName, Action<ABasicPipe> asyncReaderStart)
    {
        this.asyncReaderStart = asyncReaderStart;
        clientPipeStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        pipeStream = clientPipeStream;
    }

    /// <summary>
    /// Connects a client pipe to its matching server pipe.
    /// </summary>
    /// <param name="timeout">Timeout in ms.</param>
    public void Connect(int timeout = 7000)
    {
        clientPipeStream.Connect(7000);
        asyncReaderStart(this);
        Connected();
    }
}
}

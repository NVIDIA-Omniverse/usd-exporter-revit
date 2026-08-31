// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UsdExporterRevit.Utilities
{
public class PipeEventArgs : EventArgs
{
    public readonly byte[] Arr;
    public readonly int Len;
    public readonly string Msg;

    public PipeEventArgs(byte[] arr, int len)
    {
        this.Arr = arr;
        this.Len = len;
    }

    public PipeEventArgs(string msg)
    {
        this.Msg = msg;
    }
}

public abstract class ABasicPipe
{
    public event EventHandler<PipeEventArgs> DataReceived;
    public event EventHandler<EventArgs> PipeClosed;
    public event EventHandler<EventArgs> PipeConnected;

    protected PipeStream pipeStream;
    protected Action<ABasicPipe> asyncReaderStart;

    public ABasicPipe()
    {
    }

    public void Close()
    {
#if REV2025 || REV2026
        if (OperatingSystem.IsWindows())
        {
            pipeStream.WaitForPipeDrain();
        }
#else
        pipeStream.WaitForPipeDrain();
#endif
        pipeStream.Close();
        pipeStream.Dispose();
        pipeStream = null;
    }

    public void Flush()
    {
        pipeStream.Flush();
    }

    protected void Connected()
    {
        PipeConnected?.Invoke(this, EventArgs.Empty);
    }

    protected void StartByteReaderAsync(Action<byte[]> packetReceived)
    {
        int intSize = sizeof(int);
        byte[] bDataLength = new byte[intSize];

        pipeStream?.ReadAsync(bDataLength, 0, intSize)
            .ContinueWith(
                t =>
                {
                    int len = t.Result;

                    if (len == 0)
                    {
                        PipeClosed?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        int dataLength = BitConverter.ToInt32(bDataLength, 0);
                        byte[] data = new byte[dataLength];

                        pipeStream?.ReadAsync(data, 0, dataLength)
                            .ContinueWith(
                                t2 =>
                                {
                                    len = t2.Result;

                                    if (len == 0)
                                    {
                                        PipeClosed?.Invoke(this, EventArgs.Empty);
                                    }
                                    else
                                    {
                                        packetReceived(data);
                                        StartByteReaderAsync(packetReceived);
                                    }
                                }
                            );
                    }
                }
            );
    }

    public Task WriteString(string str)
    {
        return WriteBytes(Encoding.UTF8.GetBytes(str));
    }

    public Task WriteBytes(byte[] bytes)
    {
        var blength = BitConverter.GetBytes(bytes.Length);
        var bfull = blength.Concat(bytes).ToArray();
        return pipeStream.WriteAsync(bfull, 0, bfull.Length);
    }

    /// <summary>
    /// Reads an array of bytes, where the first [n] bytes (based on the server's intsize) indicates the number of bytes to read
    /// to complete the packet.
    /// </summary>
    public void StartByteReaderAsync()
    {
        StartByteReaderAsync((b) => DataReceived?.Invoke(this, new PipeEventArgs(b, b.Length)));
    }

    /// <summary>
    /// Reads an array of bytes, where the first [n] bytes (based on the server's intsize) indicates the number of bytes to read
    /// to complete the packet, and invokes the DataReceived event with a string converted from UTF8 of the byte array.
    /// </summary>
    public void StartStringReaderAsync()
    {
        StartByteReaderAsync(
            (b) =>
            {
                string str = Encoding.UTF8.GetString(b).TrimEnd('\0');
                DataReceived?.Invoke(this, new PipeEventArgs(str));
            }
        );
    }
}
}

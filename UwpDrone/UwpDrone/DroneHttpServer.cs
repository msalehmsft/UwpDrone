﻿/*
    Copyright(c) Microsoft Corp. All rights reserved.
    
    The MIT License(MIT)
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :
    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace UwpDrone
{
    public sealed class DroneHttpServer : IDisposable
    {
        private const uint bufLen = 8192;
        private int defaultPort = 3000;
        private readonly StreamSocketListener sock;
        internal FlightController fc;
        private string lastCommand = string.Empty;
        private const string successMsg = "{success=\"ok\"}";

        internal DroneHttpServer(int serverPort, FlightController f)
        {
            fc = f;
            sock = new StreamSocketListener();
            sock.Control.KeepAlive = true;
            defaultPort = serverPort;
            sock.ConnectionReceived += async (s, e) => await ProcessRequestAsync(e.Socket);
        }

        internal async void StartServer()
        {
            await sock.BindServiceNameAsync(defaultPort.ToString());
        }

        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            try
            {
                // Read in the HTTP request, we only care about type 'GET'
                StringBuilder requestFull = new StringBuilder(string.Empty);
                using (IInputStream input = socket.InputStream)
                {
                    byte[] data = new byte[bufLen];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = bufLen;
                    while (dataRead == bufLen)
                    {
                        await input.ReadAsync(buffer, bufLen, InputStreamOptions.Partial);
                        requestFull.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                using (IOutputStream output = socket.OutputStream)
                {
                    try
                    {
                        if (requestFull.Length == 0)
                        {
                            throw (new Exception("WTF dude?"));
                        }

                        string requestStart = requestFull.ToString().Split('\n')[0];
                        string[] requestParts = requestStart.Split(' ');
                        string requestMethod = requestParts[0];
                        if (requestMethod.ToUpper() != "GET")
                        {
                            throw (new Exception("UNSUPPORTED HTTP REQUEST METHOD"));
                        }

                        string requestPath = requestParts[1];
                        var splits = requestPath.Split('?');
                        if (splits.Length < 2)
                        {
                            throw (new Exception("EMPTY OR MISSING QUERY STRING"));
                        }

                        string botCmd = splits[1].ToLower();
                        if (string.IsNullOrEmpty(botCmd))
                        {
                            throw (new Exception("EMPTY OR MISSING QUERY STRING"));
                        }

                        WwwFormUrlDecoder queryBag = new WwwFormUrlDecoder(botCmd);
                        await ProcessCommand(queryBag, output);
                    }
                    catch (Exception e)
                    {
                        // We use 'Bad Request' here since chances are the exception was caused by bad query strings
                        await WriteResponseAsync("400 Bad Request", e.Message + e.StackTrace, output);
                    }
                }
            }
            catch (Exception e)
            {
                // Server can force shutdown which generates an exception. Spew it.
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }

        private async Task ProcessCommand(WwwFormUrlDecoder querybag, IOutputStream outstream)
        {
            try
            {
                string botCmd = querybag.GetFirstValueByName("cmd").ToLowerInvariant(); // Throws System.ArgumentException if not found
                switch (botCmd)
                {
                    case "arm":
                        {
                            fc.Arm();
                            await WriteResponseAsync("200 OK", successMsg, outstream);
                            break;
                        }

                    case "disarm":
                        {
                            fc.Disarm();
                            await WriteResponseAsync("200 OK", successMsg, outstream);
                            break;
                        }
                    case "takeoff":
                        {
                            await fc.takeoff();
                            await WriteResponseAsync("200 OK", successMsg, outstream);
                            break;
                        }
                    case "flyheight":
                        {

                            UInt32 height = UInt32.Parse(querybag.GetFirstValueByName("height")); // in cm!
                            fc.flyToHeight(height);
                            await WriteResponseAsync("200 OK", successMsg, outstream);
                            break;
                        }

                    default:
                        {
                            await WriteResponseAsync("400 Bad Request", string.Format("UNSUPPORTED COMMAND: {0}", botCmd), outstream);
                            break;
                        }
                }

                lastCommand = botCmd;
            }
            catch(ArgumentException)
            {
                await WriteResponseAsync("400 Bad Request", "INVALID QUERY STRING", outstream);
            }
            catch (Exception e)
            {
                await WriteResponseAsync("500 Internal Server Error", e.Message + e.StackTrace, outstream);
            }
        }

        private async Task WriteResponseAsync(string statuscode, string response, IOutputStream outstream)
        {
            using (DataWriter writer = new DataWriter(outstream))
            {
                try
                {
                    string respBody = string.IsNullOrEmpty(response) ? string.Empty : response;
                    string statCode = string.IsNullOrEmpty(statuscode) ? "200 OK" : statuscode;

                    string header = String.Format("HTTP/1.1 {0}\r\n" +
                                                  "Content-Type: text/html\r\n" +
                                                  "Content-Length: {1}\r\n" +
                                                  "Connection: close\r\n\r\n",
                                                  statuscode, response.Length);

                    writer.WriteString(header);
                    writer.WriteString(respBody);
                    await writer.StoreAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message + "\n" + e.StackTrace);
                }
            }
        }

        public void Dispose()
        {
            sock.Dispose();
        }
    }
}

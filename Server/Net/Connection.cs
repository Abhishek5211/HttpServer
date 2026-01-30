using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Server.Http;

namespace Server.Net;

public sealed class Connection
{
    private readonly Socket _socket;
    private const int MaxHeaderBytes = 64 * 1024; // 64KB

    public Connection(Socket socket)
    {
        _socket = socket;
    }

    public async Task HandleAsync(CancellationToken ct)
    {
        // Own the socket through the NetworkStream; disposing the stream will close the socket.
        using var networkStream = new NetworkStream(_socket, ownsSocket: true);
        var buffer = new byte[4096];
        var headerBuffer = new MemoryStream();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                headerBuffer.SetLength(0);

                // Read headers until \r\n\r\n
                int bytesRead;
                bool headersComplete = false;
                while (!headersComplete)
                {
                    bytesRead = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (bytesRead == 0)
                    {
                        // Client closed connection
                        return;
                    }

                    headerBuffer.Write(buffer, 0, bytesRead);
                    if (headerBuffer.Length > MaxHeaderBytes)
                    {
                        var resp = HttpResponse.RequestHeaderFieldsTooLarge();
                        await Writer.WriteAsync(_socket, resp, ct);
                        return;
                    }

                    // Check for end of headers
                    var arr = headerBuffer.GetBuffer();
                    var len = (int)headerBuffer.Length;
                    if (len >= 4)
                    {
                        for (int i = Math.Max(0, len - bytesRead - 4); i <= len - 4; i++)
                        {
                            if (arr[i] == '\r' && arr[i + 1] == '\n' && arr[i + 2] == '\r' && arr[i + 3] == '\n')
                            {
                                headersComplete = true;
                                // We will leave the stream pointer where unread body bytes start by keeping data in headerBuffer
                                break;
                            }
                        }
                    }
                }

                // Extract header bytes up to header end
                var headerBytes = headerBuffer.ToArray();
                int headerEnd = -1;
                for (int i = 0; i + 3 < headerBytes.Length; i++)
                {
                    if (headerBytes[i] == '\r' && headerBytes[i + 1] == '\n' && headerBytes[i + 2] == '\r' && headerBytes[i + 3] == '\n')
                    {
                        headerEnd = i + 4;
                        break;
                    }
                }

                if (headerEnd == -1)
                {
                    // Shouldn't happen because we checked above, but be defensive
                    var resp = HttpResponse.BadRequest("Malformed request");
                    await Writer.WriteAsync(_socket, resp, ct);
                    return;
                }

                // Parse request line and headers using ASCII (per HTTP)
                var headerText = Encoding.ASCII.GetString(headerBytes, 0, headerEnd);
                using var reader = new StringReader(headerText);
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    var resp = HttpResponse.BadRequest("Empty request");
                    await Writer.WriteAsync(_socket, resp, ct);
                    return;
                }

                var parts = requestLine.Split(' ', 3);
                if (parts.Length != 3)
                {
                    var resp = HttpResponse.BadRequest("Invalid request line");
                    await Writer.WriteAsync(_socket, resp, ct);
                    return;
                }

                var method = parts[0];
                var path = parts[1];
                var version = parts[2];

                // Read headers
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    var idx = line.IndexOf(':');
                    if (idx <= 0) continue;
                    var name = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    headers[name] = value;
                }

                // Determine how many body bytes are already in headerBuffer beyond headerEnd
                int remaining = (int)headerBuffer.Length - headerEnd;
                byte[]? body = null;
                long contentLength = 0;
                if (headers.TryGetValue("Content-Length", out var cl) && long.TryParse(cl, out contentLength) && contentLength > 0)
                {
                    body = new byte[contentLength];
                    int copied = 0;
                    if (remaining > 0)
                    {
                        var copyCount = Math.Min(remaining, (int)contentLength);
                        Array.Copy(headerBytes, headerEnd, body, 0, copyCount);
                        copied += copyCount;
                    }

                    while (copied < contentLength)
                    {
                        int toRead = (int)Math.Min(buffer.Length, contentLength - copied);
                        int n = await networkStream.ReadAsync(buffer.AsMemory(0, toRead), ct);
                        if (n == 0)
                        {
                            // Client closed prematurely
                            break;
                        }
                        Array.Copy(buffer, 0, body, copied, n);
                        copied += n;
                    }
                }

                // Keep-alive determination
                bool keepAlive;
                if (version.Equals("HTTP/1.1", StringComparison.OrdinalIgnoreCase))
                    keepAlive = true;
                else
                    keepAlive = false;

                if (headers.TryGetValue("Connection", out var connVal))
                {
                    var v = connVal.Trim().ToLowerInvariant();
                    if (v == "close") keepAlive = false;
                    if (v == "keep-alive") keepAlive = true;
                }

                var request = new HttpRequest(method, path, version, headers, keepAlive, contentLength);

                // Route and generate response
                var handler = Router.Default.Find(method, path);
                HttpResponse response;
                try
                {
                    response = await handler(request);
                }
                catch (Exception ex)
                {
                    // Simple 500
                    var bytes = Encoding.UTF8.GetBytes("Internal Server Error");
                    response = new HttpResponse
                    {
                        StatusCode = 500,
                        ReasonPhrase = "Internal Server Error",
                        Body = bytes
                    };
                    response.Headers["Content-Type"] = "text/plain; charset=utf-8";
                    response.Headers["Content-Length"] = bytes.Length.ToString();
                    response.Headers["Connection"] = "close";
                }

                // Ensure Connection header exists in response
                if (!response.Headers.ContainsKey("Connection"))
                    response.Headers["Connection"] = request.KeepAlive ? "keep-alive" : "close";

                await Writer.WriteAsync(_socket, response, ct);

                // If either side wants to close, break the loop and dispose socket
                if (!request.KeepAlive || response.Headers.TryGetValue("Connection", out var respConn) && respConn.Trim().Equals("close", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Otherwise continue loop to handle next pipelined request on same connection
            }
        }
        catch (OperationCanceledException) { /* canceled - exit gracefully */ }
        catch { /* swallow to ensure socket closed by disposing networkStream */ }
    }
}
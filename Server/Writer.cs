// Server/Http/Writer.cs
using System.Net.Sockets;
using System.Text;

namespace Server.Http
{
    public static class Writer
    {
        public static async Task WriteAsync(Socket socket, HttpResponse resp, CancellationToken ct)
        {
            // Serialize status line and headers
            var sb = new StringBuilder(256);
            sb.Append("HTTP/1.1 ").Append(resp.StatusCode).Append(' ').Append(resp.ReasonPhrase).Append("\r\n");
            foreach (var kv in resp.Headers)
                sb.Append(kv.Key).Append(": ").Append(kv.Value).Append("\r\n");
            sb.Append("\r\n");

            var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());

            // Coalesce headers + body in as few sends as possible
            if (resp.Body.Length == 0)
            {
                await socket.SendAsync(headerBytes, SocketFlags.None, ct);
                return;
            }

            // Try a single gather write using SendAsync(ReadOnlyMemory<byte>[])
            var segments = new ReadOnlyMemory<byte>[] { headerBytes, resp.Body };
            await socket.SendAsync(segments, SocketFlags.None, ct);
        }
    }
}

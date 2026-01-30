 using System.Text;

namespace Server.Http
{
    public enum ParseStatus { NeedMoreData, Ok, BadRequest }

    public sealed class ParseResult
    {
        public ParseStatus Status { get; init; }
        public HttpRequest? Request { get; init; }
        public int ConsumedBytes { get; init; }
    }

    public sealed class HttpParser
    {
        private const int MaxHeaderBytes = 32_768;

        public void Reset() { }

        public ParseResult TryParseRequest(ReadOnlySpan<byte> data)
        {
            // Look for header terminator: \r\n\r\n
            var idx = IndexOfDoubleCrlf(data);
            if (idx < 0)
            {
                if (data.Length > MaxHeaderBytes)
                    return new ParseResult { Status = ParseStatus.BadRequest, ConsumedBytes = data.Length };
                return new ParseResult { Status = ParseStatus.NeedMoreData };
            }

            var headerSpan = data[..idx];
            var consumed = idx + 4;

            // Parse request line
            var lineEnd = headerSpan.IndexOf("\r\n"u8);
            if (lineEnd <= 0) return Bad();
            var reqLine = headerSpan[..lineEnd];
            var parts = Encoding.ASCII.GetString(reqLine).Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return Bad();

            var method = parts[0];
            var path = parts[1];
            var version = parts[2];
            if (version != "HTTP/1.1" && version != "HTTP/1.0") return Bad();

            // Parse headers
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rest = headerSpan[(lineEnd + 2)..]; // skip CRLF
            var s = rest;
            while (s.Length > 0)
            {
                var eol = s.IndexOf("\r\n"u8);
                if (eol < 0) break;
                var line = s[..eol];
                s = s[(eol + 2)..];

                if (line.Length == 0) break; // end

                var colon = line.IndexOf((byte)':');
                if (colon <= 0) return Bad();
                var name = Encoding.ASCII.GetString(line[..colon]).Trim();
                var value = Encoding.ASCII.GetString(line[(colon + 1)..]).Trim();
                headers[name] = value;
            }

            var keepAlive = true;
            if (headers.TryGetValue("Connection", out var conn))
                keepAlive = !conn.Equals("close", StringComparison.OrdinalIgnoreCase);

            var req = new HttpRequest(method, path, version, headers, keepAlive, bodyLength: 0);
            return new ParseResult { Status = ParseStatus.Ok, Request = req, ConsumedBytes = consumed };

            static ParseResult Bad() => new() { Status = ParseStatus.BadRequest };
        }

        private static int IndexOfDoubleCrlf(ReadOnlySpan<byte> data)
        {
            // Scan for \r\n\r\n
            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (data[i] == (byte)'\r' && data[i + 1] == (byte)'\n' &&
                    data[i + 2] == (byte)'\r' && data[i + 3] == (byte)'\n')
                    return i;
            }
            return -1;
        }
    }
}

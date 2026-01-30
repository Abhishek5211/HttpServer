// Server/Http/HttpRequest.cs
namespace Server.Http
{
    public sealed class HttpRequest
    {
        public string Method { get; }
        public string Path { get; }
        public string Version { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public bool KeepAlive { get; }
        public long BodyLength { get; }

        public HttpRequest(string method, string path, string version,
            IReadOnlyDictionary<string, string> headers, bool keepAlive, long bodyLength)
        {
            Method = method; Path = path; Version = version;
            Headers = headers; KeepAlive = keepAlive; BodyLength = bodyLength;
        }
    }
}

// Server/Http/HttpResponse.cs
using System.Text;

namespace Server.Http
{
    public sealed class HttpResponse
    {
        public int StatusCode { get; init; }
        public string ReasonPhrase { get; init; } = "OK";
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[] Body { get; init; } = Array.Empty<byte>();

        public static HttpResponse OkText(string text, bool keepAlive = true)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var resp = new HttpResponse { StatusCode = 200, ReasonPhrase = "OK", Body = bytes };
            resp.Headers["Content-Type"] = "text/plain; charset=utf-8";
            resp.Headers["Content-Length"] = bytes.Length.ToString();
            resp.Headers["Connection"] = keepAlive ? "keep-alive" : "close";
            return resp;
        }

        public static HttpResponse BadRequest(string msg)
        {
            var r = OkText(msg, keepAlive: false);
            r.StatusCode = 400; r.ReasonPhrase = "Bad Request";
            return r;
        }

        public static HttpResponse RequestHeaderFieldsTooLarge()
        {
            var r = OkText("Header too large", keepAlive: false);
            r.StatusCode = 431; r.ReasonPhrase = "Request Header Fields Too Large";
            return r;
        }
    }
}

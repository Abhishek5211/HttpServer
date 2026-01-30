
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


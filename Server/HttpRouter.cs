// Server/Http/Router.cs
namespace Server.Http
{
    public sealed class Router
    {
        private readonly Dictionary<(string method, string path), Func<HttpRequest, ValueTask<HttpResponse>>> _routes
            = new();

        public static Router Default
        {
            get
            {
                var r = new Router();
                r.Map("GET", "/healthz", _ => new ValueTask<HttpResponse>(HttpResponse.OkText("ok")));
                r.Map("GET", "/hello", req =>
                {
                    var agent = req.Headers.TryGetValue("User-Agent", out var ua) ? ua : "unknown";
                    return new ValueTask<HttpResponse>(HttpResponse.OkText($"Hello from C# HTTP! UA={agent}"));
                });
                r.Map("GET", "/", _ => new ValueTask<HttpResponse>(HttpResponse.OkText("Welcome")));
                return r;
            }
        }

        public void Map(string method, string path, Func<HttpRequest, ValueTask<HttpResponse>> handler)
            => _routes[(method, path)] = handler;

        public Func<HttpRequest, ValueTask<HttpResponse>> Find(string method, string path)
            => _routes.TryGetValue((method, path), out var h) ? h : NotFound;

        private static ValueTask<HttpResponse> NotFound(HttpRequest req)
            => new(HttpResponse.OkText("Not Found", keepAlive: req.KeepAlive) { StatusCode = 404, ReasonPhrase = "Not Found" });
    }
}

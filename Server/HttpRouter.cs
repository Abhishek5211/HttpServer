
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        {
            var body = Encoding.UTF8.GetBytes("Not Found");
            var resp = new HttpResponse
            {
                StatusCode = 404,
                ReasonPhrase = "Not Found",
                Body = body
            };
            resp.Headers["Content-Type"] = "text/plain; charset=utf-8";
            resp.Headers["Content-Length"] = body.Length.ToString();
            resp.Headers["Connection"] = req.KeepAlive ? "keep-alive" : "close";
            return new ValueTask<HttpResponse>(resp);
        }
    }
}
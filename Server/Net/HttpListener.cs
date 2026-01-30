using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Net
{
    public sealed class HttpListener
    {
        private readonly IPEndPoint _endPoint;
        private readonly Socket _listenerSocket;
        private readonly int _backlog;
        private readonly int _maxConnections;

        public HttpListener(IPEndPoint endPoint, int backlog = 100, int maxConnections = 1000)
        {
            _endPoint = endPoint;
            _backlog = backlog;
            _maxConnections = maxConnections;
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _listenerSocket.Bind(_endPoint);
            _listenerSocket.Listen(_backlog);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var client = await _listenerSocket.AcceptAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var conn = new Connection(client);
                            await conn.HandleAsync(ct);
                        }
                        catch
                        {
                            try { client.Dispose(); } catch { }
                        }
                    }, ct);
                }
            }
            finally
            {
                try { _listenerSocket.Close(); } catch { }
            }
        }
    }
}
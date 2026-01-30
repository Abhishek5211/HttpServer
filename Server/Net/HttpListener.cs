
using System.Net;
using System.Net.Sockets;

namespace HttpServer
{
    public sealed class HttpListener
    {
         private readonly IPEndPoint _endPoint;
         private readonly Socket _listenerSocket;
         private readonly int _backlog;
         private readonly int _maxConnections;

         private readonly int _activeConnections = 0;

         public HttpListener(IPEndPoint endPoint, int backlog = 100, int maxConnections = 1000)
         {
            _endPoint = endPoint;
            _backlog = backlog;
            _maxConnections = maxConnections;
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
         }

         public async Task StartAsync(CancellationToken ct)
        {
            _listenerSocket.Bind(_endPoint);
            _listenerSocket.Listen(_backlog);
        } 
          
    }
}
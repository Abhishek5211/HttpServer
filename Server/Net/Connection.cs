using System.Net.Sockets;
using System.Text;

namespace Server.Net;

public sealed class Connection
{
    private readonly Socket _socket;

    public Connection(Socket socket)
    {
        _socket = socket;
    }

    public async Task HandleAsync(CancellationToken ct)
    {
        using var networkStream = new NetworkStream(_socket, ownsSocket: true);
        using var reader = new StreamReader(networkStream,  Encoding.UTF8);
        using var writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };

        // Simple HTTP response for demonstration purposes
        string requestLine = await reader.ReadLineAsync();
        Console.WriteLine($"Received request: {requestLine}");

        string httpResponse = "HTTP/1.1 200 OK\r\n" +
                              "Content-Type: text/plain\r\n" +
                              "Content-Length: 13\r\n" +
                              "\r\n" +
                              "Hello, World!";
        await writer.WriteAsync(httpResponse);
    }
}
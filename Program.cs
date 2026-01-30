
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

class Program
{
    static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Any, 42069);
        listener.Start();
        Console.WriteLine("Listening on port 42069...");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            var cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("Client connected.");

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
             

            client.Close();
            Console.WriteLine("Client disconnected.");
        }
    }
}

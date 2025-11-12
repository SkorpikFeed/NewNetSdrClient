using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    public class EchoServer
    {
        private readonly ITcpListenerWrapper _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Func<ITcpClientWrapper, Task> _clientHandler;

        // Constructor with listener injection for testability
        public EchoServer(ITcpListenerWrapper listener, Func<ITcpClientWrapper, Task>? clientHandler = null)
        {
            _listener = listener;
            _cancellationTokenSource = new CancellationTokenSource();
            _clientHandler = clientHandler ?? (client => HandleClientAsync(client, _cancellationTokenSource.Token));
        }

        // Convenience constructor for production use
        public EchoServer(int port) : this(new TcpListenerWrapper(port))
        {
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine("Server started.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    TcpClient rawClient = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");

                    ITcpClientWrapper clientWrapper = new TcpClientWrapper(rawClient);
                    _ = Task.Run(() => _clientHandler(clientWrapper));
                }
                catch (ObjectDisposedException)
                {
                    // Listener has been closed
                    break;
                }
            }

            Console.WriteLine("Server shutdown.");
        }

        public virtual async Task HandleClientAsync(ITcpClientWrapper client, CancellationToken token)
        {
            using (INetworkStreamWrapper stream = client.GetStream())
            {
                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while (!token.IsCancellationRequested && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        // Echo back the received message
                        await stream.WriteAsync(buffer, 0, bytesRead, token);
                        Console.WriteLine($"Echoed {bytesRead} bytes to the client.");
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    Console.WriteLine("Client disconnected.");
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener.Stop();
            _cancellationTokenSource.Dispose();
            Console.WriteLine("Server stopped.");
        }

        public static async Task Main(string[] args)
        {
            EchoServer server = new EchoServer(5000);

            // Start the server in a separate task
            _ = Task.Run(() => server.StartAsync());

            string host = "127.0.0.1"; // Target IP
            int port = 60000;          // Target Port
            int intervalMilliseconds = 5000; // Send every 3 seconds

            using (var sender = new UdpTimedSender(host, port))
            {
                Console.WriteLine("Press any key to stop sending...");
                sender.StartSending(intervalMilliseconds);

                Console.WriteLine("Press 'q' to quit...");
                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                    // Just wait until 'q' is pressed
                }

                sender.StopSending();
                server.Stop();
                Console.WriteLine("Sender stopped.");
            }
        }
    }


    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IUdpClientWrapper _udpClient;
        private Timer? _timer;
        private ushort _sequenceNumber;

        public UdpTimedSender(string host, int port, IUdpClientWrapper? udpClient = null)
        {
            _host = host;
            _port = port;
            _udpClient = udpClient ?? new UdpClientWrapper();
            _sequenceNumber = 0;
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        private void SendMessageCallback(object? state)
        {
            try
            {
                //dummy data
                Random rnd = new Random();
                byte[] samples = new byte[1024];
                rnd.NextBytes(samples);
                _sequenceNumber++;

                byte[] msg = (new byte[] { 0x04, 0x84 }).Concat(BitConverter.GetBytes(_sequenceNumber)).Concat(samples).ToArray();
                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _udpClient.Send(msg, msg.Length, endpoint);
                Console.WriteLine($"Message sent to {_host}:{_port} ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void StopSending()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            StopSending();
            _udpClient.Dispose();
        }
    }
}
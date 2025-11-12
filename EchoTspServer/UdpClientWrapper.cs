using System.Net;
using System.Net.Sockets;

namespace EchoServer
{
    public class UdpClientWrapper : IUdpClientWrapper
    {
        private readonly UdpClient _client;

        public UdpClientWrapper()
        {
            _client = new UdpClient();
        }

        public int Send(byte[] dgram, int bytes, IPEndPoint endPoint)
        {
            return _client.Send(dgram, bytes, endPoint);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}

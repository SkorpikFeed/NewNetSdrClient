using System;
using System.Net;

namespace EchoServer
{
    public interface IUdpClientWrapper : IDisposable
    {
        int Send(byte[] dgram, int bytes, IPEndPoint endPoint);
    }
}

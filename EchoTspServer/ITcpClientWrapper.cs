using System;

namespace EchoServer
{
    public interface ITcpClientWrapper : IDisposable
    {
        INetworkStreamWrapper GetStream();
        void Close();
    }
}

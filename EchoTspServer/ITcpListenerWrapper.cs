using System.Net.Sockets;
using System.Threading.Tasks;

namespace EchoServer
{
    public interface ITcpListenerWrapper
    {
        void Start();
        void Stop();
        Task<TcpClient> AcceptTcpClientAsync();
    }
}

using EchoServer;
using FluentAssertions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace EchoTspServer.Tests;

public class WrapperIntegrationTests : IDisposable
{
    private TcpListener? _testListener;
    private TcpClient? _testClient;
    private const int TestPort = 9876;

    [Fact]
    public async Task TcpListenerWrapper_StartStopAccept_WorksCorrectly()
    {
        // Arrange
        var wrapper = new TcpListenerWrapper(TestPort);

        // Act - Start
        wrapper.Start();

        // Create a client to connect
        var client = new TcpClient();
        var connectTask = client.ConnectAsync("127.0.0.1", TestPort);
        
        // Accept connection
        var acceptTask = wrapper.AcceptTcpClientAsync();
        
        await Task.WhenAll(connectTask, acceptTask);
        var acceptedClient = await acceptTask;

        // Assert
        acceptedClient.Should().NotBeNull();
        acceptedClient.Connected.Should().BeTrue();

        // Cleanup
        acceptedClient.Close();
        client.Close();
        wrapper.Stop();
    }

    [Fact]
    public void TcpListenerWrapper_Constructor_CreatesListener()
    {
        // Arrange & Act
        var wrapper = new TcpListenerWrapper(TestPort + 1);

        // Assert
        wrapper.Should().NotBeNull();
        
        // Start and stop to verify it works
        wrapper.Start();
        wrapper.Stop();
    }

    [Fact]
    public async Task TcpClientWrapper_GetStream_ReturnsWorkingStream()
    {
        // Arrange - Setup a server
        _testListener = new TcpListener(IPAddress.Any, TestPort + 2);
        _testListener.Start();

        // Create and connect a client
        _testClient = new TcpClient();
        await _testClient.ConnectAsync("127.0.0.1", TestPort + 2);

        var wrapper = new TcpClientWrapper(_testClient);

        // Act
        var streamWrapper = wrapper.GetStream();

        // Assert
        streamWrapper.Should().NotBeNull();
        streamWrapper.Should().BeAssignableTo<INetworkStreamWrapper>();
    }

    [Fact]
    public async Task TcpClientWrapper_CloseAndDispose_WorksCorrectly()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Any, TestPort + 3);
        _testListener.Start();

        _testClient = new TcpClient();
        await _testClient.ConnectAsync("127.0.0.1", TestPort + 3);

        var wrapper = new TcpClientWrapper(_testClient);

        // Act
        wrapper.Close();

        // Assert - No exception thrown when disposing after Close
        Action act = () => wrapper.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task NetworkStreamWrapper_ReadWriteAsync_WorksCorrectly()
    {
        // Arrange - Setup server and client
        _testListener = new TcpListener(IPAddress.Any, TestPort + 4);
        _testListener.Start();

        var clientTask = Task.Run(async () =>
        {
            _testClient = new TcpClient();
            await _testClient.ConnectAsync("127.0.0.1", TestPort + 4);
            return _testClient;
        });

        var serverClient = await _testListener.AcceptTcpClientAsync();
        var client = await clientTask;

        var serverStream = new NetworkStreamWrapper(serverClient.GetStream());
        var clientStream = new NetworkStreamWrapper(client.GetStream());

        // Act - Write from client
        var testData = Encoding.UTF8.GetBytes("Hello, Server!");
        await clientStream.WriteAsync(testData, 0, testData.Length, CancellationToken.None);

        // Read on server
        var buffer = new byte[1024];
        var bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        // Assert
        bytesRead.Should().Be(testData.Length);
        var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        receivedData.Should().Be("Hello, Server!");

        // Cleanup
        serverStream.Dispose();
        clientStream.Dispose();
        serverClient.Close();
        client.Close();
    }

    [Fact]
    public async Task NetworkStreamWrapper_Dispose_ClosesStream()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Any, TestPort + 5);
        _testListener.Start();

        _testClient = new TcpClient();
        await _testClient.ConnectAsync("127.0.0.1", TestPort + 5);
        
        var serverClient = await _testListener.AcceptTcpClientAsync();
        var streamWrapper = new NetworkStreamWrapper(serverClient.GetStream());

        // Act & Assert - disposing should not throw
        Action firstDispose = () => streamWrapper.Dispose();
        firstDispose.Should().NotThrow();

        // Calling dispose a second time should also not throw (idempotent)
        Action secondDispose = () => streamWrapper.Dispose();
        secondDispose.Should().NotThrow();
        
        serverClient.Close();
    }

    [Fact]
    public async Task UdpClientWrapper_Send_WorksCorrectly()
    {
        // Arrange
        var wrapper = new UdpClientWrapper();
        var testEndpoint = new IPEndPoint(IPAddress.Loopback, TestPort + 6);
        var testData = Encoding.UTF8.GetBytes("UDP Test Message");

        // Create a receiver
        using var receiver = new UdpClient(TestPort + 6);
        
        // Act - Send
        var bytesSent = wrapper.Send(testData, testData.Length, testEndpoint);

        // Receive
        var receiveTask = receiver.ReceiveAsync();
        var result = await receiveTask;

        // Assert
        bytesSent.Should().Be(testData.Length);
        result.Buffer.Should().BeEquivalentTo(testData);

        // Cleanup
        wrapper.Dispose();
    }

    [Fact]
    public void UdpClientWrapper_Send_MultipleMessages_Success()
    {
        // Arrange
        var wrapper = new UdpClientWrapper();
        var testEndpoint = new IPEndPoint(IPAddress.Loopback, TestPort + 7);
        var testData1 = Encoding.UTF8.GetBytes("Message 1");
        var testData2 = Encoding.UTF8.GetBytes("Message 2");

        // Act - Send multiple messages
        var bytes1 = wrapper.Send(testData1, testData1.Length, testEndpoint);
        var bytes2 = wrapper.Send(testData2, testData2.Length, testEndpoint);

        // Assert
        bytes1.Should().Be(testData1.Length);
        bytes2.Should().Be(testData2.Length);

        // Cleanup
        wrapper.Dispose();
    }

    [Fact]
    public void UdpClientWrapper_Dispose_DoesNotThrow()
    {
        // Arrange
        var wrapper = new UdpClientWrapper();

        // Act & Assert
        var act = () => wrapper.Dispose();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _testClient?.Close();
        _testClient?.Dispose();
        _testListener?.Stop();
    }
}

using NetSdrClientApp.Networking;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSdrClientAppTests;

public class NetworkingWrapperIntegrationTests : IDisposable
{
    private TcpListener? _testListener;
    private TcpClient? _testClient;
    private TcpClientWrapper? _wrapperUnderTest;
    private const int TestTcpPort = 8765;
    private const int TestUdpPort = 8766;

    [Test]
    public async Task TcpClientWrapper_ConnectAndDisconnect_Success()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Loopback, TestTcpPort);
        _testListener.Start();

        var wrapper = new TcpClientWrapper("127.0.0.1", TestTcpPort);

        // Act
        var acceptTask = _testListener.AcceptTcpClientAsync();
        wrapper.Connect();
        _testClient = await acceptTask;

        // Assert
        Assert.That(wrapper.Connected, Is.True);

        // Cleanup
        wrapper.Disconnect();
        Assert.That(wrapper.Connected, Is.False);
    }

    [Test]
    public async Task TcpClientWrapper_SendAndReceiveMessage_Success()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Loopback, TestTcpPort + 1);
        _testListener.Start();

        var wrapper = new TcpClientWrapper("127.0.0.1", TestTcpPort + 1);
        byte[]? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        wrapper.MessageReceived += (sender, data) =>
        {
            receivedMessage = data;
            messageReceived.SetResult(true);
        };

        // Act - Connect
        var acceptTask = _testListener.AcceptTcpClientAsync();
        wrapper.Connect();
        _testClient = await acceptTask;
        var serverStream = _testClient.GetStream();

        // Send message from wrapper
        var testMessage = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        await wrapper.SendMessageAsync(testMessage);

        // Read on server side
        var serverBuffer = new byte[1024];
        var bytesRead = await serverStream.ReadAsync(serverBuffer, 0, serverBuffer.Length);
        Assert.That(bytesRead, Is.EqualTo(testMessage.Length));
        Assert.That(serverBuffer.Take(bytesRead).ToArray(), Is.EqualTo(testMessage));

        // Send response back
        var response = new byte[] { 0xAA, 0xBB };
        await serverStream.WriteAsync(response, 0, response.Length);

        // Wait for MessageReceived event
        await Task.WhenAny(messageReceived.Task, Task.Delay(2000));

        // Assert
        Assert.That(receivedMessage, Is.Not.Null);
        Assert.That(receivedMessage, Is.EqualTo(response));

        wrapper.Disconnect();
    }

    [Test]
    public async Task TcpClientWrapper_SendStringMessage_Success()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Loopback, TestTcpPort + 2);
        _testListener.Start();

        var wrapper = new TcpClientWrapper("127.0.0.1", TestTcpPort + 2);

        // Act - Connect
        var acceptTask = _testListener.AcceptTcpClientAsync();
        wrapper.Connect();
        _testClient = await acceptTask;
        var serverStream = _testClient.GetStream();

        // Send string message
        var testString = "Hello, Server!";
        await wrapper.SendMessageAsync(testString);

        // Read on server side
        var serverBuffer = new byte[1024];
        var bytesRead = await serverStream.ReadAsync(serverBuffer, 0, serverBuffer.Length);
        var receivedString = Encoding.UTF8.GetString(serverBuffer, 0, bytesRead);

        // Assert
        Assert.That(receivedString, Is.EqualTo(testString));

        wrapper.Disconnect();
    }

    [Test]
    public void TcpClientWrapper_ConnectWhenAlreadyConnected_LogsMessage()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Loopback, TestTcpPort + 3);
        _testListener.Start();

        var wrapper = new TcpClientWrapper("127.0.0.1", TestTcpPort + 3);

        // Act - Connect twice
        var acceptTask = _testListener.AcceptTcpClientAsync();
        wrapper.Connect();
        
        // Second connect attempt
        wrapper.Connect();

        // Assert - No exception thrown
        Assert.That(wrapper.Connected, Is.True);

        wrapper.Disconnect();
    }

    [Test]
    public void TcpClientWrapper_DisconnectWhenNotConnected_LogsMessage()
    {
        // Arrange
        var wrapper = new TcpClientWrapper("127.0.0.1", TestTcpPort + 4);

        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => wrapper.Disconnect());
    }

    [Test]
    public void TcpClientWrapper_ConnectToInvalidHost_HandlesException()
    {
        // Arrange
        var wrapper = new TcpClientWrapper("invalid.host.that.does.not.exist.local", 12345);

        // Act - Try to connect to invalid host
        wrapper.Connect();

        // Assert - Should handle exception gracefully
        Assert.That(wrapper.Connected, Is.False);
    }

    [Test]
    public async Task UdpClientWrapper_StartAndStopListening_Success()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(TestUdpPort);
        var receivedData = new List<byte[]>();
        var messageReceived = new TaskCompletionSource<bool>();

        wrapper.MessageReceived += (sender, data) =>
        {
            receivedData.Add(data);
            messageReceived.SetResult(true);
        };

        // Act - Start listening
        var listeningTask = wrapper.StartListeningAsync();

        // Give it time to start
        await Task.Delay(100);

        // Send UDP message
        using var sender = new UdpClient();
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        await sender.SendAsync(testData, testData.Length, "127.0.0.1", TestUdpPort);

        // Wait for message
        await Task.WhenAny(messageReceived.Task, Task.Delay(2000));

        // Stop listening
        wrapper.StopListening();

        // Assert
        Assert.That(receivedData.Count, Is.GreaterThan(0));
        Assert.That(receivedData[0], Is.EqualTo(testData));
    }

    [Test]
    public void UdpClientWrapper_StopListeningWhenNotStarted_NoException()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(TestUdpPort + 1);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.StopListening());
    }

    [Test]
    public void UdpClientWrapper_Exit_CallsStopListening()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(TestUdpPort + 2);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.Exit());
    }

    [Test]
    public void UdpClientWrapper_GetHashCode_ConsistentResults()
    {
        // Arrange
        var wrapper1 = new UdpClientWrapper(TestUdpPort + 3);
        
        // Act
        var hash1 = wrapper1.GetHashCode();
        var hash2 = wrapper1.GetHashCode();

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void UdpClientWrapper_Equals_SamePortReturnsTrue()
    {
        // Arrange
        var wrapper1 = new UdpClientWrapper(TestUdpPort + 4);
        var wrapper2 = new UdpClientWrapper(TestUdpPort + 4);

        // Act
        var result = wrapper1.Equals(wrapper2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task TcpClientWrapper_MessageReceivedMultipleTimes_AllReceived()
    {
        // Arrange
        _testListener = new TcpListener(IPAddress.Loopback, TestTcpPort + 5);
        _testListener.Start();

        var wrapper = new TcpClientWrapper("127.0.0.1", TestTcpPort + 5);
        var receivedMessages = new List<byte[]>();
        var messageCount = new TaskCompletionSource<bool>();
        var expectedMessages = 3;
        var receivedCount = 0;

        wrapper.MessageReceived += (sender, data) =>
        {
            receivedMessages.Add(data);
            receivedCount++;
            if (receivedCount >= expectedMessages)
            {
                messageCount.SetResult(true);
            }
        };

        // Act - Connect
        var acceptTask = _testListener.AcceptTcpClientAsync();
        wrapper.Connect();
        _testClient = await acceptTask;
        var serverStream = _testClient.GetStream();

        // Send multiple messages
        for (int i = 0; i < expectedMessages; i++)
        {
            var message = new byte[] { (byte)i };
            await serverStream.WriteAsync(message, 0, message.Length);
            await Task.Delay(50); // Small delay between messages
        }

        // Wait for all messages
        await Task.WhenAny(messageCount.Task, Task.Delay(3000));

        // Assert
        Assert.That(receivedMessages.Count, Is.GreaterThanOrEqualTo(expectedMessages));

        wrapper.Disconnect();
    }

    public void Dispose()
    {
        _wrapperUnderTest?.Disconnect();
        _testClient?.Close();
        _testClient?.Dispose();
        _testListener?.Stop();
    }
}

using EchoServer;
using FluentAssertions;
using Moq;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EchoTspServer.Tests
{
    public class EchoServerTests
    {
        [Fact]
        public async Task StartAsync_ShouldStartListener()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            var cts = new CancellationTokenSource();
            
            mockListener.Setup(l => l.AcceptTcpClientAsync())
                .Returns(async () =>
                {
                    await Task.Delay(10000, cts.Token); // Long delay to keep waiting
                    throw new OperationCanceledException();
                });

            var server = new EchoServer.EchoServer(mockListener.Object);

            // Act
            var startTask = Task.Run(() => server.StartAsync());
            await Task.Delay(100); // Give server time to start
            server.Stop();
            
            try
            {
                await startTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // Expected if stop doesn't immediately cancel
            }

            // Assert
            mockListener.Verify(l => l.Start(), Times.Once);
            mockListener.Verify(l => l.Stop(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_ShouldAcceptAndHandleClients()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            var handlerCalledTcs = new TaskCompletionSource<bool>();

            var callCount = 0;
            mockListener.Setup(l => l.AcceptTcpClientAsync())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return new TcpClient();
                    }
                    // Block forever on second call
                    return Task.Run(async () =>
                    {
                        await Task.Delay(Timeout.Infinite);
                        return new TcpClient();
                    }).Result;
                });

            bool handlerCalled = false;
            Func<ITcpClientWrapper, Task> clientHandler = async (client) =>
            {
                handlerCalled = true;
                handlerCalledTcs.SetResult(true);
                await Task.CompletedTask;
            };

            var server = new EchoServer.EchoServer(mockListener.Object, clientHandler);

            // Act
            var startTask = Task.Run(() => server.StartAsync());
            await handlerCalledTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            server.Stop();
            
            // Give it time to stop
            await Task.Delay(100);

            // Assert
            handlerCalled.Should().BeTrue();
            mockListener.Verify(l => l.AcceptTcpClientAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task HandleClientAsync_ShouldEchoDataBack()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            byte[] receivedData = System.Text.Encoding.UTF8.GetBytes("Hello, Server!");
            byte[]? echoedData = null;

            var readCallCount = 0;
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    readCallCount++;
                    if (readCallCount == 1)
                    {
                        Array.Copy(receivedData, 0, buffer, offset, receivedData.Length);
                        return receivedData.Length;
                    }
                    return 0; // Simulate end of stream
                });

            mockStream.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((buffer, offset, count, token) =>
                {
                    echoedData = new byte[count];
                    Array.Copy(buffer, offset, echoedData, 0, count);
                })
                .Returns(Task.CompletedTask);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServer.EchoServer(mockListener.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            echoedData.Should().NotBeNull();
            echoedData.Should().Equal(receivedData);
            mockStream.Verify(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
            mockStream.Verify(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            mockClient.Verify(c => c.Close(), Times.Once);
        }

        [Fact]
        public async Task HandleClientAsync_ShouldHandleMultipleMessages()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            byte[] message1 = System.Text.Encoding.UTF8.GetBytes("Message 1");
            byte[] message2 = System.Text.Encoding.UTF8.GetBytes("Message 2");
            var echoedMessages = new System.Collections.Generic.List<byte[]>();

            var readCallCount = 0;
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    readCallCount++;
                    if (readCallCount == 1)
                    {
                        Array.Copy(message1, 0, buffer, offset, message1.Length);
                        return message1.Length;
                    }
                    else if (readCallCount == 2)
                    {
                        Array.Copy(message2, 0, buffer, offset, message2.Length);
                        return message2.Length;
                    }
                    return 0;
                });

            mockStream.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((buffer, offset, count, token) =>
                {
                    byte[] data = new byte[count];
                    Array.Copy(buffer, offset, data, 0, count);
                    echoedMessages.Add(data);
                })
                .Returns(Task.CompletedTask);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServer.EchoServer(mockListener.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            echoedMessages.Should().HaveCount(2);
            echoedMessages[0].Should().Equal(message1);
            echoedMessages[1].Should().Equal(message2);
        }

        [Fact]
        public async Task HandleClientAsync_ShouldStopOnCancellation()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            var cts = new CancellationTokenSource();
            var readCalledTcs = new TaskCompletionSource<bool>();

            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(async (byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    readCalledTcs.SetResult(true);
                    cts.Cancel(); // Cancel after first read is initiated
                    await Task.Delay(100, CancellationToken.None);
                    return 0;
                });

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServer.EchoServer(mockListener.Object);

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            await readCalledTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            mockClient.Verify(c => c.Close(), Times.Once);
        }

        [Fact]
        public async Task HandleClientAsync_ShouldHandleExceptions()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServer.EchoServer(mockListener.Object);
            var cts = new CancellationTokenSource();

            // Act
            Func<Task> act = async () => await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert - should not throw, should handle exception gracefully
            await act.Should().NotThrowAsync();
            mockClient.Verify(c => c.Close(), Times.Once);
        }

        [Fact]
        public async Task Stop_ShouldCancelTokenAndStopListener()
        {
            // Arrange
            var mockListener = new Mock<ITcpListenerWrapper>();
            mockListener.Setup(l => l.AcceptTcpClientAsync())
                .Returns(async () =>
                {
                    await Task.Delay(10000); // Long delay
                    throw new OperationCanceledException();
                });

            var server = new EchoServer.EchoServer(mockListener.Object);

            // Act
            var startTask = Task.Run(() => server.StartAsync());
            await Task.Delay(100); // Give server time to start
            server.Stop();

            // Assert
            mockListener.Verify(l => l.Stop(), Times.Once);
        }

        [Fact]
        public void Constructor_WithPort_ShouldCreateInstance()
        {
            // Arrange & Act
            var server = new EchoServer.EchoServer(5000);

            // Assert
            server.Should().NotBeNull();
        }
    }
}

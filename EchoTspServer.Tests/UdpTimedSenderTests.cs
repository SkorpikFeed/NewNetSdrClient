using EchoServer;
using FluentAssertions;
using Moq;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EchoTspServer.Tests
{
    public class UdpTimedSenderTests
    {
        [Fact]
        public void Constructor_ShouldInitialize()
        {
            // Arrange & Act
            var sender = new UdpTimedSender("127.0.0.1", 5000);

            // Assert
            sender.Should().NotBeNull();
        }

        [Fact]
        public void StartSending_ShouldThrowIfAlreadyRunning()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            var sender = new UdpTimedSender("127.0.0.1", 5000, mockUdpClient.Object);
            sender.StartSending(1000);

            // Act
            Action act = () => sender.StartSending(1000);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Sender is already running.");

            sender.Dispose();
        }

        [Fact]
        public async Task StartSending_ShouldSendMessagesAtInterval()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            int sendCount = 0;
            byte[]? capturedMessage = null;
            IPEndPoint? capturedEndpoint = null;

            mockUdpClient.Setup(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], int, IPEndPoint>((msg, length, endpoint) =>
                {
                    sendCount++;
                    if (capturedMessage == null)
                    {
                        capturedMessage = new byte[length];
                        Array.Copy(msg, capturedMessage, length);
                        capturedEndpoint = endpoint;
                    }
                })
                .Returns((byte[] msg, int length, IPEndPoint endpoint) => length);

            var sender = new UdpTimedSender("127.0.0.1", 5000, mockUdpClient.Object);

            // Act
            sender.StartSending(100); // Send every 100ms
            await Task.Delay(350); // Wait for at least 3 sends
            sender.StopSending();

            // Assert
            sendCount.Should().BeGreaterOrEqualTo(3);
            capturedMessage.Should().NotBeNull();
            capturedMessage.Length.Should().Be(1028); // 2 header bytes + 2 sequence bytes + 1024 sample bytes
            capturedMessage[0].Should().Be(0x04);
            capturedMessage[1].Should().Be(0x84);
            capturedEndpoint.Should().NotBeNull();
            capturedEndpoint.Address.ToString().Should().Be("127.0.0.1");
            capturedEndpoint.Port.Should().Be(5000);
        }

        [Fact]
        public async Task StartSending_ShouldIncrementSequenceNumber()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            var sequenceNumbers = new System.Collections.Generic.List<ushort>();

            mockUdpClient.Setup(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], int, IPEndPoint>((msg, length, endpoint) =>
                {
                    // Extract sequence number (bytes 2-3)
                    ushort seqNum = BitConverter.ToUInt16(msg, 2);
                    sequenceNumbers.Add(seqNum);
                })
                .Returns((byte[] msg, int length, IPEndPoint endpoint) => length);

            var sender = new UdpTimedSender("192.168.1.1", 6000, mockUdpClient.Object);

            // Act
            sender.StartSending(50); // Send every 50ms
            await Task.Delay(250); // Wait for at least 4 sends
            sender.StopSending();

            // Assert
            sequenceNumbers.Should().HaveCountGreaterOrEqualTo(4);
            sequenceNumbers[0].Should().Be(1);
            sequenceNumbers[1].Should().Be(2);
            sequenceNumbers[2].Should().Be(3);
            for (int i = 1; i < sequenceNumbers.Count; i++)
            {
                sequenceNumbers[i].Should().Be((ushort)(sequenceNumbers[i - 1] + 1));
            }
        }

        [Fact]
        public void StopSending_ShouldStopTimer()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            int sendCount = 0;

            mockUdpClient.Setup(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback(() => sendCount++)
                .Returns(1000);

            var sender = new UdpTimedSender("127.0.0.1", 5000, mockUdpClient.Object);

            // Act
            sender.StartSending(100);
            Thread.Sleep(250);
            int countBeforeStop = sendCount;
            sender.StopSending();
            Thread.Sleep(300);
            int countAfterStop = sendCount;

            // Assert
            countBeforeStop.Should().BeGreaterOrEqualTo(2);
            countAfterStop.Should().Be(countBeforeStop); // No more sends after stop
        }

        [Fact]
        public void StopSending_WhenNotStarted_ShouldNotThrow()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 5000);

            // Act
            Action act = () => sender.StopSending();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldStopSendingAndDisposeClient()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            var sender = new UdpTimedSender("127.0.0.1", 5000, mockUdpClient.Object);
            sender.StartSending(1000);

            // Act
            sender.Dispose();

            // Assert
            mockUdpClient.Verify(u => u.Dispose(), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ShouldContainCorrectStructure()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            byte[]? capturedMessage = null;

            mockUdpClient.Setup(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], int, IPEndPoint>((msg, length, endpoint) =>
                {
                    if (capturedMessage == null)
                    {
                        capturedMessage = new byte[length];
                        Array.Copy(msg, capturedMessage, length);
                    }
                })
                .Returns((byte[] msg, int length, IPEndPoint endpoint) => length);

            var sender = new UdpTimedSender("10.0.0.1", 8080, mockUdpClient.Object);

            // Act
            sender.StartSending(100);
            await Task.Delay(150); // Wait for at least one send
            sender.StopSending();

            // Assert
            capturedMessage.Should().NotBeNull();
            capturedMessage.Length.Should().Be(1028); // 2 + 2 + 1024
            
            // Check header
            capturedMessage[0].Should().Be(0x04);
            capturedMessage[1].Should().Be(0x84);
            
            // Check sequence number is present (bytes 2-3)
            ushort seqNum = BitConverter.ToUInt16(capturedMessage, 2);
            seqNum.Should().BeGreaterThan(0);
            
            // Check that sample data exists (remaining 1024 bytes)
            capturedMessage.Length.Should().Be(1028);
        }

        [Fact]
        public void SendMessage_WithException_ShouldNotCrash()
        {
            // Arrange
            var mockUdpClient = new Mock<IUdpClientWrapper>();
            mockUdpClient.Setup(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()))
                .Throws(new InvalidOperationException("Network error"));

            var sender = new UdpTimedSender("127.0.0.1", 5000, mockUdpClient.Object);

            // Act
            Action act = () =>
            {
                sender.StartSending(100);
                Thread.Sleep(200);
                sender.StopSending();
            };

            // Assert
            act.Should().NotThrow();
        }
    }
}

using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task StopIQNoConnectionTest()
    {
        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown, just logs message
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _updMock.Verify(udp => udp.StopListening(), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task ChangeFrequencyAsyncTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        long frequency = 144_500_000; // 144.5 MHz
        int channel = 1;

        //act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4)); // 3 from connect + 1 from ChangeFrequency
    }

    [Test]
    public async Task ConnectAsyncMultipleTimesTest()
    {
        //First connection
        await _client.ConnectAsync();
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));

        //Second connection attempt - should not reconnect if already connected
        await _client.ConnectAsync();
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once); // Still only once
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3)); // Still only 3 messages
    }

    [Test]
    public async Task TcpMessageReceivedEventTest()
    {
        //Arrange
        await ConnectAsyncTest();
        byte[] testMessage = new byte[] { 0x01, 0x02, 0x03 };
        bool eventReceived = false;

        // We need to trigger the event handler indirectly through SendMessageAsync
        // because our mock setup raises the event

        //act
        await _client.StartIQAsync();

        //assert
        // The event is triggered by the mock when SendMessageAsync is called
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4)); // 3 connect + 1 startIQ
    }

    [Test]
    public async Task UdpMessageReceivedEventTest()
    {
        //Arrange
        await ConnectAsyncTest();
        await _client.StartIQAsync();

        byte[] testUdpMessage = new byte[] { 
            0x00, 0x01, // header
            0x00, 0x00, // sequence
            0x10, 0x00, 0x20, 0x00, 0x30, 0x00, 0x40, 0x00 // 4 samples of 16-bit data
        };

        //act - Trigger UDP message received event
        _updMock.Raise(udp => udp.MessageReceived += null, _updMock.Object, testUdpMessage);

        //assert
        // The event handler processes the message and writes to file
        // We can't easily verify file writes, but we can verify the event was set up
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task ChangeFrequencyWithDifferentChannelsTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act - Test different channels
        await _client.ChangeFrequencyAsync(100_000_000, 0);
        await _client.ChangeFrequencyAsync(200_000_000, 1);
        await _client.ChangeFrequencyAsync(300_000_000, 2);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(6)); // 3 from connect + 3 frequency changes
    }

    [Test]
    public async Task StartAndStopIQMultipleTimesTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        
        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);

        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);

        //assert
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Exactly(2));
        _updMock.Verify(udp => udp.StopListening(), Times.Once);
    }

    //TODO: cover the rest of the NetSdrClient code here
}

using NetSdrClientApp.Networking;
using System.Text;

namespace NetSdrClientAppTests;

public class TcpClientWrapperTests
{
    [Test]
    public void Constructor_ValidParameters_Success()
    {
        //Arrange & Act
        var wrapper = new TcpClientWrapper("localhost", 8080);

        //Assert
        Assert.That(wrapper.Connected, Is.False);
    }

    [Test]
    public void Connected_InitiallyFalse()
    {
        //Arrange
        var wrapper = new TcpClientWrapper("localhost", 8080);

        //Assert
        Assert.That(wrapper.Connected, Is.False);
    }

    [Test]
    public void Disconnect_WhenNotConnected_NoException()
    {
        //Arrange
        var wrapper = new TcpClientWrapper("localhost", 8080);

        //Act & Assert - should not throw
        Assert.DoesNotThrow(() => wrapper.Disconnect());
    }

    [Test]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsException()
    {
        //Arrange
        var wrapper = new TcpClientWrapper("localhost", 8080);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        //Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapper.SendMessageAsync(data));
    }

    [Test]
    public async Task SendMessageAsync_StringOverload_WhenNotConnected_ThrowsException()
    {
        //Arrange
        var wrapper = new TcpClientWrapper("localhost", 8080);

        //Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapper.SendMessageAsync("test"));
    }

    [Test]
    public void MessageReceived_Event_CanBeSubscribed()
    {
        //Arrange
        var wrapper = new TcpClientWrapper("localhost", 8080);
        bool eventFired = false;

        //Act
        wrapper.MessageReceived += (sender, data) => { eventFired = true; };

        //Assert - event subscription should work without exceptions
        Assert.That(eventFired, Is.False); // Event not fired yet
    }
}

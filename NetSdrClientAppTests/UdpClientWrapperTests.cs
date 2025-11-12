using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class UdpClientWrapperTests
{
    [Test]
    public void Constructor_ValidPort_Success()
    {
        //Arrange & Act
        var wrapper = new UdpClientWrapper(5000);

        //Assert
        Assert.That(wrapper, Is.Not.Null);
    }

    [Test]
    public void StopListening_WhenNotStarted_NoException()
    {
        //Arrange
        var wrapper = new UdpClientWrapper(5001);

        //Act & Assert - should not throw
        Assert.DoesNotThrow(() => wrapper.StopListening());
    }

    [Test]
    public void Exit_WhenNotStarted_NoException()
    {
        //Arrange
        var wrapper = new UdpClientWrapper(5002);

        //Act & Assert - should not throw
        Assert.DoesNotThrow(() => wrapper.Exit());
    }

    [Test]
    public void MessageReceived_Event_CanBeSubscribed()
    {
        //Arrange
        var wrapper = new UdpClientWrapper(5003);
        bool eventFired = false;

        //Act
        wrapper.MessageReceived += (sender, data) => { eventFired = true; };

        //Assert - event subscription should work without exceptions
        Assert.That(eventFired, Is.False); // Event not fired yet
    }

    [Test]
    public void GetHashCode_SamePortAndAddress_SameHashCode()
    {
        //Arrange
        var wrapper1 = new UdpClientWrapper(5004);
        var wrapper2 = new UdpClientWrapper(5004);

        //Act
        var hash1 = wrapper1.GetHashCode();
        var hash2 = wrapper2.GetHashCode();

        //Assert
        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void GetHashCode_DifferentPort_DifferentHashCode()
    {
        //Arrange
        var wrapper1 = new UdpClientWrapper(5005);
        var wrapper2 = new UdpClientWrapper(5006);

        //Act
        var hash1 = wrapper1.GetHashCode();
        var hash2 = wrapper2.GetHashCode();

        //Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public void Equals_SamePort_ReturnsTrue()
    {
        //Arrange
        var wrapper1 = new UdpClientWrapper(5007);
        var wrapper2 = new UdpClientWrapper(5007);

        //Act
        bool result = wrapper1.Equals(wrapper2);

        //Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Equals_DifferentPort_ReturnsFalse()
    {
        //Arrange
        var wrapper1 = new UdpClientWrapper(5008);
        var wrapper2 = new UdpClientWrapper(5009);

        //Act
        bool result = wrapper1.Equals(wrapper2);

        //Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Equals_NullObject_ReturnsFalse()
    {
        //Arrange
        var wrapper = new UdpClientWrapper(5010);

        //Act
        bool result = wrapper.Equals(null);

        //Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Equals_DifferentType_ReturnsFalse()
    {
        //Arrange
        var wrapper = new UdpClientWrapper(5011);
        var otherObject = "not a UdpClientWrapper";

        //Act
        bool result = wrapper.Equals(otherObject);

        //Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Equals_SameInstance_ReturnsTrue()
    {
        //Arrange
        var wrapper = new UdpClientWrapper(5012);

        //Act
        bool result = wrapper.Equals(wrapper);

        //Assert
        Assert.That(result, Is.True);
    }
}

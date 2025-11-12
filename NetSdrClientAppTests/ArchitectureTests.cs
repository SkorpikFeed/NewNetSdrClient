using NetArchTest.Rules;
using NUnit.Framework;
using System.Reflection;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class ArchitectureTests
    {
        private static readonly Assembly Assembly = typeof(NetSdrClientApp.NetSdrClient).Assembly;

        [Test]
        public void Presentation_Should_Not_HaveDependencyOnNetworkingInternals()
        {
            // Arrange
            var types = Types.InAssembly(Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp")
                .And()
                .DoNotResideInNamespace("NetSdrClientApp.Networking");

            // Act
            var result = types
                .ShouldNot()
                .HaveDependencyOn("System.Net.Sockets")
                .GetResult();

            // Assert
            Assert.That(result.IsSuccessful, Is.True);
        }

        [Test]
        public void Networking_Should_BeSealed()
        {
            // Arrange
            var types = Types.InAssembly(Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.Networking")
                .And()
                .AreClasses();

            // Act
            var result = types
                .Should()
                .BeSealed()
                .GetResult();

            // Assert
            Assert.That(result.IsSuccessful, Is.True);
        }
    }
}
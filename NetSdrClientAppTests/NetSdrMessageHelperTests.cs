using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void TranslateMessage_ControlItem_Success()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var parameters = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);

            //Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var actualType, out var actualCode, out var sequenceNumber, out var body);

            //Assert
            Assert.That(success, Is.True);
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualCode, Is.EqualTo(code));
            Assert.That(body, Is.EqualTo(parameters));
            Assert.That(sequenceNumber, Is.EqualTo(0)); // Control items don't have sequence number
        }

        [Test]
        public void TranslateMessage_DataItem_Success()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            var parameters = new byte[] { 0x01, 0x00, 0xAA, 0xBB, 0xCC, 0xDD };
            var msg = NetSdrMessageHelper.GetDataItemMessage(type, parameters);

            //Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var actualType, out var itemCode, out var sequenceNumber, out var body);

            //Assert
            Assert.That(success, Is.True);
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(sequenceNumber, Is.EqualTo(BitConverter.ToUInt16(new byte[] { 0x01, 0x00 })));
            Assert.That(body, Is.EqualTo(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }));
        }

        [Test]
        public void GetSamples_16BitSamples_Success()
        {
            //Arrange
            ushort sampleSize = 16; // 16 bits = 2 bytes per sample
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }; // 3 samples

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(3));
            Assert.That(samples[0], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x01, 0x02, 0x00, 0x00 })));
            Assert.That(samples[1], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x03, 0x04, 0x00, 0x00 })));
            Assert.That(samples[2], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x05, 0x06, 0x00, 0x00 })));
        }

        [Test]
        public void GetSamples_24BitSamples_Success()
        {
            //Arrange
            ushort sampleSize = 24; // 24 bits = 3 bytes per sample
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }; // 2 samples

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x01, 0x02, 0x03, 0x00 })));
            Assert.That(samples[1], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x04, 0x05, 0x06, 0x00 })));
        }

        [Test]
        public void GetSamples_InvalidSampleSize_ThrowsException()
        {
            //Arrange
            ushort sampleSize = 40; // 40 bits = 5 bytes (exceeds 4 bytes max)
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

            //Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();
            });
        }

        [Test]
        public void GetControlItemMessage_DifferentMessageTypes_Success()
        {
            //Arrange
            var types = new[] {
                NetSdrMessageHelper.MsgTypes.SetControlItem,
                NetSdrMessageHelper.MsgTypes.CurrentControlItem,
                NetSdrMessageHelper.MsgTypes.ControlItemRange,
                NetSdrMessageHelper.MsgTypes.Ack
            };
            var code = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            var parameters = new byte[] { 0x01, 0x02, 0x03 };

            //Act & Assert
            foreach (var type in types)
            {
                var msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);
                
                // Verify message can be translated back correctly
                bool success = NetSdrMessageHelper.TranslateMessage(msg, out var actualType, out var actualCode, out _, out var body);
                
                Assert.That(success, Is.True, $"Failed for type: {type}");
                Assert.That(actualType, Is.EqualTo(type), $"Type mismatch for: {type}");
                Assert.That(actualCode, Is.EqualTo(code), $"Code mismatch for: {type}");
                Assert.That(body, Is.EqualTo(parameters), $"Body mismatch for: {type}");
            }
        }
    }
}
using System.Collections.Generic;
using NUnit.Framework;
using TCAMultiplayer.Tests.TestDoubles;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class FakeNetworkHarnessTests
    {
        [Test]
        public void DelayedPackets_ArriveOnlyAfterClockAdvanceAndUpdate()
        {
            var network = new FakeNetworkHarness(new PacketChaosSettings
            {
                MinimumLatencySeconds = 0.10,
                MaximumLatencySeconds = 0.10
            });
            var host = network.CreateHost();
            var client = network.CreateClient();
            var received = new List<byte[]>();

            network.Connect(host, client);
            client.Update();
            client.OnDataReceived += (_, data) => received.Add(data);

            host.Send(client.LocalPeerId, new byte[] { 1, 2, 3 }, reliable: false);

            network.Advance(0.099);
            client.Update();
            Assert.AreEqual(0, received.Count);

            network.Advance(0.001);
            Assert.AreEqual(0, received.Count, "Delivery is frame-driven and should wait for Update.");

            client.Update();
            Assert.AreEqual(1, received.Count);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received[0]);
        }

        [Test]
        public void ChaosDecision_CanDuplicateAndReorderPacketsDeterministically()
        {
            var network = new FakeNetworkHarness(new PacketChaosSettings
            {
                DecidePacket = packet =>
                {
                    byte id = packet.Data[0];
                    return new PacketChaosDecision
                    {
                        ExtraCopies = id == 1 ? 1 : 0,
                        DelaySeconds = id == 1 ? 0.20 : 0.05
                    };
                }
            });
            var host = network.CreateHost();
            var client = network.CreateClient();
            var receivedIds = new List<byte>();

            network.Connect(host, client);
            client.Update();
            client.OnDataReceived += (_, data) => receivedIds.Add(data[0]);

            host.Send(client.LocalPeerId, new byte[] { 1 }, reliable: false);
            host.Send(client.LocalPeerId, new byte[] { 2 }, reliable: false);

            network.Advance(0.05);
            client.Update();
            CollectionAssert.AreEqual(new byte[] { 2 }, receivedIds);

            network.Advance(0.15);
            client.Update();
            CollectionAssert.AreEqual(new byte[] { 2, 1, 1 }, receivedIds);
        }

        [Test]
        public void LossRate_DropsPacketsDeterministically()
        {
            var network = new FakeNetworkHarness(new PacketChaosSettings
            {
                LossRate = 1.0,
                MinimumLatencySeconds = 0.0,
                MaximumLatencySeconds = 0.0
            });
            var host = network.CreateHost();
            var client = network.CreateClient();
            int receivedCount = 0;

            network.Connect(host, client);
            client.Update();
            client.OnDataReceived += (_, __) => receivedCount++;

            host.Send(client.LocalPeerId, new byte[] { 9 }, reliable: false);
            network.AdvanceFrame();
            client.Update();

            Assert.AreEqual(0, receivedCount);
        }
    }
}

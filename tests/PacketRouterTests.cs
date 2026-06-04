using NUnit.Framework;
using TCAMultiplayer.Protocol;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class PacketRouterTests
    {
        [Test]
        public void Register_AllowsMultipleHandlersForSamePacketType()
        {
            var router = new PacketRouter();
            int firstCalls = 0;
            int secondCalls = 0;
            byte[] packet = PacketSerializer.Serialize(PacketType.KillConfirm);

            router.Register(PacketType.KillConfirm, (_, __) => firstCalls++);
            router.Register(PacketType.KillConfirm, (_, __) => secondCalls++);

            router.Route(42, packet);

            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
            Assert.AreEqual(2, router.GetHandlerCount(PacketType.KillConfirm));
        }

        [Test]
        public void UnregisterSpecificHandler_KeepsOtherHandlers()
        {
            var router = new PacketRouter();
            int firstCalls = 0;
            int secondCalls = 0;
            byte[] packet = PacketSerializer.Serialize(PacketType.KillConfirm);
            System.Action<ulong, byte[]> first = (_, __) => firstCalls++;
            System.Action<ulong, byte[]> second = (_, __) => secondCalls++;

            router.Register(PacketType.KillConfirm, first);
            router.Register(PacketType.KillConfirm, second);
            router.Unregister(PacketType.KillConfirm, first);

            router.Route(42, packet);

            Assert.AreEqual(0, firstCalls);
            Assert.AreEqual(1, secondCalls);
            Assert.AreEqual(1, router.GetHandlerCount(PacketType.KillConfirm));
        }
    }
}

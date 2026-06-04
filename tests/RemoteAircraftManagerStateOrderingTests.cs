using System.Reflection;
using NUnit.Framework;
using TCAMultiplayer.Sync;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class RemoteAircraftManagerStateOrderingTests
    {
        private static readonly MethodInfo ShouldAcceptSequenceMethod =
            typeof(RemoteAircraftManager).GetMethod(
                "ShouldAcceptSequence",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo ResetSequenceOrderingMethod =
            typeof(RemoteAircraftManager).GetMethod(
                "ResetSequenceOrdering",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly System.Type RemotePeerStateType =
            typeof(RemoteAircraftManager).Assembly.GetType("TCAMultiplayer.Sync.RemotePeerState");

        [Test]
        public void ShouldAcceptSequence_AcceptsFirstPacket()
        {
            Assert.IsTrue(ShouldAcceptSequence(false, 0u, 0u));
            Assert.IsTrue(ShouldAcceptSequence(false, uint.MaxValue, 7u));
        }

        [Test]
        public void ShouldAcceptSequence_RejectsDuplicateAndOlderPackets()
        {
            Assert.IsFalse(ShouldAcceptSequence(true, 10u, 10u));
            Assert.IsFalse(ShouldAcceptSequence(true, 10u, 9u));
            Assert.IsFalse(ShouldAcceptSequence(true, 10u, 0u));
        }

        [Test]
        public void ShouldAcceptSequence_AcceptsNewerPackets()
        {
            Assert.IsTrue(ShouldAcceptSequence(true, 10u, 11u));
            Assert.IsTrue(ShouldAcceptSequence(true, 10u, 999u));
        }

        [Test]
        public void ShouldAcceptSequence_HandlesUintWraparound()
        {
            Assert.IsTrue(ShouldAcceptSequence(true, uint.MaxValue, 0u));
            Assert.IsTrue(ShouldAcceptSequence(true, uint.MaxValue - 1u, 0u));
            Assert.IsFalse(ShouldAcceptSequence(true, 0u, uint.MaxValue));
        }

        [Test]
        public void ResetSequenceOrdering_AllowsRespawnLifeToStartAtAnySequence()
        {
            var peer = System.Activator.CreateInstance(RemotePeerStateType);
            RemotePeerStateType.GetField("LastSequenceNumber").SetValue(peer, 42u);
            RemotePeerStateType.GetField("HasReceivedSequence").SetValue(peer, true);

            ResetSequenceOrderingMethod.Invoke(null, new[] { peer });

            Assert.AreEqual(0u, RemotePeerStateType.GetField("LastSequenceNumber").GetValue(peer));
            Assert.AreEqual(false, RemotePeerStateType.GetField("HasReceivedSequence").GetValue(peer));
            Assert.IsTrue(ShouldAcceptSequence(
                (bool)RemotePeerStateType.GetField("HasReceivedSequence").GetValue(peer),
                (uint)RemotePeerStateType.GetField("LastSequenceNumber").GetValue(peer),
                1u));
        }

        private static bool ShouldAcceptSequence(bool hasReceivedSequence, uint lastSequenceNumber, uint incomingSequenceNumber)
        {
            return (bool)ShouldAcceptSequenceMethod.Invoke(
                null,
                new object[] { hasReceivedSequence, lastSequenceNumber, incomingSequenceNumber });
        }
    }
}

using NUnit.Framework;
using TCAMultiplayer.Protocol;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class ApplicationEventDedupCacheTests
    {
        [Test]
        public void EventId_EqualityIncludesPeerLifeKindChannelAndSequence()
        {
            var original = new ApplicationEventId(7, 2, ApplicationEventKind.Explosion, 15, channel: 1);

            Assert.AreEqual(original, new ApplicationEventId(7, 2, ApplicationEventKind.Explosion, 15, channel: 1));
            Assert.AreNotEqual(original, new ApplicationEventId(8, 2, ApplicationEventKind.Explosion, 15, channel: 1));
            Assert.AreNotEqual(original, new ApplicationEventId(7, 3, ApplicationEventKind.Explosion, 15, channel: 1));
            Assert.AreNotEqual(original, new ApplicationEventId(7, 2, ApplicationEventKind.Damage, 15, channel: 1));
            Assert.AreNotEqual(original, new ApplicationEventId(7, 2, ApplicationEventKind.Explosion, 15, channel: 2));
            Assert.AreNotEqual(original, new ApplicationEventId(7, 2, ApplicationEventKind.Explosion, 16, channel: 1));
        }

        [Test]
        public void Sequencer_AllocatesPerStreamAndWrapsNaturally()
        {
            var sequencer = new ApplicationEventSequencer();
            var damage = new ApplicationEventStream(1, 10, ApplicationEventKind.Damage);
            var explosion = new ApplicationEventStream(1, 10, ApplicationEventKind.Explosion);

            Assert.AreEqual(0u, sequencer.Next(damage).Sequence);
            Assert.AreEqual(1u, sequencer.Next(damage).Sequence);
            Assert.AreEqual(0u, sequencer.Next(explosion).Sequence);

            sequencer.SetNextSequence(damage, uint.MaxValue);

            Assert.AreEqual(uint.MaxValue, sequencer.Next(damage).Sequence);
            Assert.AreEqual(0u, sequencer.Next(damage).Sequence);
        }

        [Test]
        public void TryAccept_DuplicateInSameStream_ReturnsFalse()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 8);
            var id = new ApplicationEventId(1, 1, ApplicationEventKind.Death, 3);

            Assert.IsTrue(cache.TryAccept(id));
            Assert.IsFalse(cache.TryAccept(id));
            Assert.IsTrue(cache.HasSeen(id));
        }

        [Test]
        public void TryAccept_SameSequenceInDifferentLifeOrKind_ReturnsTrue()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 8);

            Assert.IsTrue(cache.TryAccept(new ApplicationEventId(1, 1, ApplicationEventKind.Munition, 5)));
            Assert.IsTrue(cache.TryAccept(new ApplicationEventId(1, 2, ApplicationEventKind.Munition, 5)));
            Assert.IsTrue(cache.TryAccept(new ApplicationEventId(1, 1, ApplicationEventKind.Explosion, 5)));
        }

        [Test]
        public void TryAccept_OutOfOrderWithinWindow_ReturnsTrueOnce()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 8);
            var stream = new ApplicationEventStream(1, 1, ApplicationEventKind.Damage);

            Assert.IsTrue(cache.TryAccept(stream.WithSequence(10)));
            Assert.IsTrue(cache.TryAccept(stream.WithSequence(8)));
            Assert.IsFalse(cache.TryAccept(stream.WithSequence(8)));
        }

        [Test]
        public void TryAccept_SequenceBelowWindow_ReturnsFalseAsStale()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 4);
            var stream = new ApplicationEventStream(1, 1, ApplicationEventKind.ProjectileImpact);

            Assert.IsTrue(cache.TryAccept(stream.WithSequence(13)));

            Assert.IsFalse(cache.TryAccept(stream.WithSequence(9)));
            Assert.IsTrue(cache.TryAccept(stream.WithSequence(10)));
            Assert.IsFalse(cache.TryAccept(stream.WithSequence(10)));
        }

        [Test]
        public void TryAccept_HandlesUintWraparound()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 8);
            var stream = new ApplicationEventStream(1, 1, ApplicationEventKind.Explosion);

            Assert.IsTrue(cache.TryAccept(stream.WithSequence(uint.MaxValue - 1)));
            Assert.IsTrue(cache.TryAccept(stream.WithSequence(uint.MaxValue)));
            Assert.IsTrue(cache.TryAccept(stream.WithSequence(0)));
            Assert.IsTrue(cache.TryAccept(stream.WithSequence(1)));

            Assert.IsFalse(cache.TryAccept(stream.WithSequence(uint.MaxValue)));
        }

        [Test]
        public void TryAccept_EvictsLeastRecentlyUsedStreams()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 8, maxStreams: 2);
            var first = new ApplicationEventStream(1, 1, ApplicationEventKind.Damage);
            var second = new ApplicationEventStream(2, 1, ApplicationEventKind.Damage);
            var third = new ApplicationEventStream(3, 1, ApplicationEventKind.Damage);

            Assert.IsTrue(cache.TryAccept(first.WithSequence(1)));
            Assert.IsTrue(cache.TryAccept(second.WithSequence(1)));
            Assert.IsTrue(cache.TryAccept(first.WithSequence(2)));
            Assert.IsTrue(cache.TryAccept(third.WithSequence(1)));

            Assert.AreEqual(2, cache.StreamCount);
            Assert.IsTrue(cache.HasSeen(first.WithSequence(1)));
            Assert.IsFalse(cache.HasSeen(second.WithSequence(1)));
            Assert.IsTrue(cache.HasSeen(third.WithSequence(1)));
        }

        [Test]
        public void RemovePeer_ClearsAllStreamsForPeer()
        {
            var cache = new ApplicationEventDedupCache(windowSize: 8);
            var peerOneDamage = new ApplicationEventId(1, 1, ApplicationEventKind.Damage, 1);
            var peerOneDeath = new ApplicationEventId(1, 1, ApplicationEventKind.Death, 1);
            var peerTwoDamage = new ApplicationEventId(2, 1, ApplicationEventKind.Damage, 1);

            cache.TryAccept(peerOneDamage);
            cache.TryAccept(peerOneDeath);
            cache.TryAccept(peerTwoDamage);

            cache.RemovePeer(1);

            Assert.IsFalse(cache.HasSeen(peerOneDamage));
            Assert.IsFalse(cache.HasSeen(peerOneDeath));
            Assert.IsTrue(cache.HasSeen(peerTwoDamage));
            Assert.AreEqual(1, cache.StreamCount);
        }
    }
}

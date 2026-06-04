using System;
using System.Collections.Generic;

namespace TCAMultiplayer.Protocol
{
    /// <summary>
    /// Semantic event family for application-level one-shot events.
    /// </summary>
    public enum ApplicationEventKind : byte
    {
        Unspecified = 0,
        Damage = 1,
        Explosion = 2,
        Munition = 3,
        Death = 4,
        Destruction = 5,
        ProjectileImpact = 6,
        Collision = 7,
        Crater = 8,
        BuildingDestroy = 9
    }

    /// <summary>
    /// Identifies one ordered event stream owned by a peer for one aircraft life.
    /// </summary>
    public readonly struct ApplicationEventStream : IEquatable<ApplicationEventStream>
    {
        public ApplicationEventStream(
            ulong sourcePeerId,
            uint lifeId,
            ApplicationEventKind kind,
            byte channel = 0)
        {
            SourcePeerId = sourcePeerId;
            LifeId = lifeId;
            Kind = kind;
            Channel = channel;
        }

        public ulong SourcePeerId { get; }
        public uint LifeId { get; }
        public ApplicationEventKind Kind { get; }
        public byte Channel { get; }

        public ApplicationEventId WithSequence(uint sequence)
        {
            return new ApplicationEventId(this, sequence);
        }

        public bool Equals(ApplicationEventStream other)
        {
            return SourcePeerId == other.SourcePeerId
                && LifeId == other.LifeId
                && Kind == other.Kind
                && Channel == other.Channel;
        }

        public override bool Equals(object obj)
        {
            return obj is ApplicationEventStream other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + SourcePeerId.GetHashCode();
                hash = (hash * 31) + LifeId.GetHashCode();
                hash = (hash * 31) + Kind.GetHashCode();
                hash = (hash * 31) + Channel.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{SourcePeerId}/{LifeId}/{Kind}/{Channel}";
        }

        public static bool operator ==(ApplicationEventStream left, ApplicationEventStream right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ApplicationEventStream left, ApplicationEventStream right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Stable identity for one semantic network event.
    /// </summary>
    public readonly struct ApplicationEventId : IEquatable<ApplicationEventId>
    {
        public ApplicationEventId(ApplicationEventStream stream, uint sequence)
        {
            Stream = stream;
            Sequence = sequence;
        }

        public ApplicationEventId(
            ulong sourcePeerId,
            uint lifeId,
            ApplicationEventKind kind,
            uint sequence,
            byte channel = 0)
            : this(new ApplicationEventStream(sourcePeerId, lifeId, kind, channel), sequence)
        {
        }

        public ApplicationEventStream Stream { get; }
        public uint Sequence { get; }

        public ulong SourcePeerId => Stream.SourcePeerId;
        public uint LifeId => Stream.LifeId;
        public ApplicationEventKind Kind => Stream.Kind;
        public byte Channel => Stream.Channel;

        public bool Equals(ApplicationEventId other)
        {
            return Stream.Equals(other.Stream) && Sequence == other.Sequence;
        }

        public override bool Equals(object obj)
        {
            return obj is ApplicationEventId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Stream.GetHashCode() * 31) + Sequence.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Stream}#{Sequence}";
        }

        public static bool operator ==(ApplicationEventId left, ApplicationEventId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ApplicationEventId left, ApplicationEventId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Allocates naturally wrapping uint event sequences per semantic stream.
    /// </summary>
    public sealed class ApplicationEventSequencer
    {
        private readonly Dictionary<ApplicationEventStream, uint> _nextByStream =
            new Dictionary<ApplicationEventStream, uint>();

        public ApplicationEventId Next(ApplicationEventStream stream)
        {
            if (!_nextByStream.TryGetValue(stream, out uint sequence))
            {
                sequence = 0;
            }

            _nextByStream[stream] = sequence + 1;
            return stream.WithSequence(sequence);
        }

        public ApplicationEventId Next(
            ulong sourcePeerId,
            uint lifeId,
            ApplicationEventKind kind,
            byte channel = 0)
        {
            return Next(new ApplicationEventStream(sourcePeerId, lifeId, kind, channel));
        }

        public void SetNextSequence(ApplicationEventStream stream, uint nextSequence)
        {
            _nextByStream[stream] = nextSequence;
        }

        public void Reset(ApplicationEventStream stream)
        {
            _nextByStream.Remove(stream);
        }

        public void Clear()
        {
            _nextByStream.Clear();
        }
    }
}

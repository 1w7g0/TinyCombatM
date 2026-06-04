using System;
using System.Collections.Generic;

namespace TCAMultiplayer.Protocol
{
    /// <summary>
    /// Bounded deduplication window for semantic application events.
    /// </summary>
    public sealed class ApplicationEventDedupCache
    {
        public const int DefaultWindowSize = 1024;
        public const int DefaultMaxStreams = 256;

        private readonly int _windowSize;
        private readonly int _maxStreams;
        private readonly Dictionary<ApplicationEventStream, ReceiveWindow> _windows =
            new Dictionary<ApplicationEventStream, ReceiveWindow>();
        private readonly LinkedList<ApplicationEventStream> _streamLru =
            new LinkedList<ApplicationEventStream>();
        private readonly Dictionary<ApplicationEventStream, LinkedListNode<ApplicationEventStream>> _streamNodes =
            new Dictionary<ApplicationEventStream, LinkedListNode<ApplicationEventStream>>();

        public ApplicationEventDedupCache(
            int windowSize = DefaultWindowSize,
            int maxStreams = DefaultMaxStreams)
        {
            if (windowSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be positive.");
            }

            if (maxStreams <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStreams), "Max streams must be positive.");
            }

            _windowSize = windowSize;
            _maxStreams = maxStreams;
        }

        public int StreamCount => _windows.Count;

        public int WindowSize => _windowSize;

        public int MaxStreams => _maxStreams;

        /// <summary>
        /// Returns true once for a fresh event; returns false for duplicates or stale events.
        /// </summary>
        public bool TryAccept(ApplicationEventId id)
        {
            ReceiveWindow window = GetOrCreateWindow(id.Stream);
            Touch(id.Stream);

            if (window.IsDuplicateOrStale(id.Sequence))
            {
                return false;
            }

            window.MarkReceived(id.Sequence);
            return true;
        }

        public bool HasSeen(ApplicationEventId id)
        {
            if (!_windows.TryGetValue(id.Stream, out ReceiveWindow window))
            {
                return false;
            }

            return window.IsDuplicateOrStale(id.Sequence);
        }

        public void RemoveStream(ApplicationEventStream stream)
        {
            _windows.Remove(stream);

            if (_streamNodes.TryGetValue(stream, out LinkedListNode<ApplicationEventStream> node))
            {
                _streamLru.Remove(node);
                _streamNodes.Remove(stream);
            }
        }

        public void RemovePeer(ulong sourcePeerId)
        {
            List<ApplicationEventStream> toRemove = null;
            foreach (ApplicationEventStream stream in _windows.Keys)
            {
                if (stream.SourcePeerId == sourcePeerId)
                {
                    if (toRemove == null)
                    {
                        toRemove = new List<ApplicationEventStream>();
                    }

                    toRemove.Add(stream);
                }
            }

            if (toRemove == null)
            {
                return;
            }

            foreach (ApplicationEventStream stream in toRemove)
            {
                RemoveStream(stream);
            }
        }

        public void Clear()
        {
            _windows.Clear();
            _streamLru.Clear();
            _streamNodes.Clear();
        }

        private ReceiveWindow GetOrCreateWindow(ApplicationEventStream stream)
        {
            if (_windows.TryGetValue(stream, out ReceiveWindow window))
            {
                return window;
            }

            window = new ReceiveWindow(_windowSize);
            _windows.Add(stream, window);
            AddStreamNode(stream);
            EvictStreamsIfNeeded();
            return window;
        }

        private void AddStreamNode(ApplicationEventStream stream)
        {
            LinkedListNode<ApplicationEventStream> node = _streamLru.AddLast(stream);
            _streamNodes.Add(stream, node);
        }

        private void Touch(ApplicationEventStream stream)
        {
            if (!_streamNodes.TryGetValue(stream, out LinkedListNode<ApplicationEventStream> node))
            {
                return;
            }

            _streamLru.Remove(node);
            _streamLru.AddLast(node);
        }

        private void EvictStreamsIfNeeded()
        {
            while (_windows.Count > _maxStreams && _streamLru.First != null)
            {
                ApplicationEventStream oldest = _streamLru.First.Value;
                _streamLru.RemoveFirst();
                _streamNodes.Remove(oldest);
                _windows.Remove(oldest);
            }
        }

        private sealed class ReceiveWindow
        {
            private readonly int _windowSize;
            private readonly HashSet<uint> _received = new HashSet<uint>();
            private uint _highest;
            private bool _initialized;

            public ReceiveWindow(int windowSize)
            {
                _windowSize = windowSize;
            }

            public bool IsDuplicateOrStale(uint sequence)
            {
                if (!_initialized)
                {
                    return false;
                }

                int distance = SequenceDistance(_highest, sequence);
                if (distance < -(_windowSize - 1))
                {
                    return true;
                }

                return _received.Contains(sequence);
            }

            public void MarkReceived(uint sequence)
            {
                if (!_initialized)
                {
                    _highest = sequence;
                    _initialized = true;
                    _received.Add(sequence);
                    return;
                }

                int distance = SequenceDistance(_highest, sequence);
                if (distance > 0)
                {
                    _highest = sequence;
                    PruneWindow();
                }

                _received.Add(sequence);
            }

            private void PruneWindow()
            {
                if (_received.Count <= _windowSize)
                {
                    return;
                }

                List<uint> toRemove = null;
                foreach (uint sequence in _received)
                {
                    int distance = SequenceDistance(_highest, sequence);
                    if (distance < -(_windowSize - 1))
                    {
                        if (toRemove == null)
                        {
                            toRemove = new List<uint>();
                        }

                        toRemove.Add(sequence);
                    }
                }

                if (toRemove == null)
                {
                    return;
                }

                foreach (uint sequence in toRemove)
                {
                    _received.Remove(sequence);
                }
            }

            private static int SequenceDistance(uint from, uint to)
            {
                return (int)(to - from);
            }
        }
    }
}

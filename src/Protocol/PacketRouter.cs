using System;
using System.Collections.Generic;

namespace TCAMultiplayer.Protocol
{
    /// <summary>
    /// Dispatches received packets to registered handlers by PacketType.
    /// First byte of data = PacketType enum value.
    /// </summary>
    public class PacketRouter
    {
        private readonly Dictionary<PacketType, List<Action<ulong, byte[]>>> _handlers
            = new Dictionary<PacketType, List<Action<ulong, byte[]>>>();

        /// <summary>
        /// Register a handler for a specific packet type.
        /// Multiple systems may observe the same packet type.
        /// </summary>
        public void Register(PacketType type, Action<ulong, byte[]> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(type, out var handlers))
            {
                handlers = new List<Action<ulong, byte[]>>();
                _handlers[type] = handlers;
            }
            if (!handlers.Contains(handler))
                handlers.Add(handler);
        }

        /// <summary>
        /// Unregister all handlers for a specific packet type.
        /// </summary>
        public void Unregister(PacketType type)
        {
            _handlers.Remove(type);
        }

        /// <summary>
        /// Unregister a specific handler for a packet type.
        /// </summary>
        public void Unregister(PacketType type, Action<ulong, byte[]> handler)
        {
            if (handler == null) return;
            if (!_handlers.TryGetValue(type, out var handlers)) return;
            handlers.Remove(handler);
            if (handlers.Count == 0)
                _handlers.Remove(type);
        }

        /// <summary>
        /// Route a received packet to its registered handler.
        /// First byte of data is the PacketType enum value.
        /// Unknown types are logged and skipped (never crash).
        /// </summary>
        public void Route(ulong fromPeerId, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Core.Log.Warning("[PacketRouter] Received null/empty data, skipping");
                return;
            }

            var packetType = (PacketType)data[0];

            if (_handlers.TryGetValue(packetType, out var handlers) && handlers.Count > 0)
            {
                var snapshot = handlers.ToArray();
                foreach (var handler in snapshot)
                {
                    try
                    {
                        handler(fromPeerId, data);
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Error($"[PacketRouter] Handler for {packetType} threw: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            else
            {
                Core.Log.Warning($"[PacketRouter] No handler registered for packet type {packetType} (0x{(byte)packetType:X2})");
            }
        }

        /// <summary>
        /// Check if a handler is registered for a packet type.
        /// </summary>
        public bool HasHandler(PacketType type)
        {
            return _handlers.TryGetValue(type, out var handlers) && handlers.Count > 0;
        }

        /// <summary>
        /// Clear all registered handlers.
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }

        /// <summary>
        /// Get the number of packet types with at least one registered handler.
        /// </summary>
        public int HandlerCount => _handlers.Count;

        /// <summary>
        /// Get the number of handlers registered for a specific packet type.
        /// </summary>
        public int GetHandlerCount(PacketType type)
        {
            return _handlers.TryGetValue(type, out var handlers) ? handlers.Count : 0;
        }
    }
}

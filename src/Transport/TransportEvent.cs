namespace TCAMultiplayer.Transport
{
    /// <summary>
    /// Types of events produced by the transport layer.
    /// Used internally to queue events for main-thread dispatch.
    /// </summary>
    public enum TransportEventType : byte
    {
        /// <summary>Data received from a peer</summary>
        DataReceived,

        /// <summary>A new peer connected</summary>
        PeerConnected,

        /// <summary>A peer disconnected</summary>
        PeerDisconnected
    }

    /// <summary>
    /// A queued transport event for main-thread dispatch.
    /// Concrete transports enqueue these on the receive thread;
    /// Update() dequeues and fires the corresponding ITransport events.
    /// </summary>
    public struct TransportEvent
    {
        /// <summary>What happened</summary>
        public TransportEventType Type;

        /// <summary>Which peer this event relates to</summary>
        public ulong PeerId;

        /// <summary>
        /// Payload bytes (only set for DataReceived events).
        /// Null for connection/disconnection events.
        /// </summary>
        public byte[] Data;

        /// <summary>Create a data-received event</summary>
        public static TransportEvent DataReceived(ulong peerId, byte[] data)
        {
            return new TransportEvent
            {
                Type = TransportEventType.DataReceived,
                PeerId = peerId,
                Data = data
            };
        }

        /// <summary>Create a peer-connected event</summary>
        public static TransportEvent PeerConnected(ulong peerId)
        {
            return new TransportEvent
            {
                Type = TransportEventType.PeerConnected,
                PeerId = peerId,
                Data = null
            };
        }

        /// <summary>Create a peer-disconnected event</summary>
        public static TransportEvent PeerDisconnected(ulong peerId)
        {
            return new TransportEvent
            {
                Type = TransportEventType.PeerDisconnected,
                PeerId = peerId,
                Data = null
            };
        }
    }
}

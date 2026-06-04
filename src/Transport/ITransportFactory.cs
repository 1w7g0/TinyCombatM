namespace TCAMultiplayer.Transport
{
    /// <summary>
    /// Factory for creating transport instances.
    /// Implementations select the appropriate transport (Steam P2P, Direct UDP, etc.)
    /// based on configuration and runtime availability.
    /// </summary>
    public interface ITransportFactory
    {
        /// <summary>
        /// Create a transport instance with the given configuration.
        /// </summary>
        /// <param name="config">Transport settings</param>
        /// <returns>A ready-to-use transport (not yet connected)</returns>
        ITransport Create(TransportConfig config);
    }
}

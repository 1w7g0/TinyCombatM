using System;
using UnityEngine;
using Falcon.Damage;
using Falcon.Targeting;
using Falcon.UniversalAircraft;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Game
{
    /// <summary>
    /// Subscribes to game events and re-exposes them for network sync.
    /// All subscriptions tracked for cleanup.
    /// </summary>
    public class GameEventBridge : IDisposable
    {
        private const string Tag = "EVT-BRIDGE";

        // Re-exposed events for network sync
        public event Action<ExplosionEventParams> OnExplosion;
        public event Action<UniAircraft> OnAircraftDestroyed;
        public event Action<Target> OnTargetAdded;
        public event Action<Target> OnTargetRemoved;
        public event Action<DestroyedEvent> OnAnythingDestroyed;

        private bool _subscribed;

        public void Subscribe()
        {
            if (_subscribed) return;

            // Explosion.OnExplosion is a C# event — += works
            Explosion.OnExplosion += HandleExplosion;

            // TargetManagement delegates — += works
            TargetManagement.OnTargetAdded += HandleTargetAdded;
            TargetManagement.OnTargetRemoved += HandleTargetRemoved;

            // Damageable.OnAnythingDestroyed is a static event
            Damageable.OnAnythingDestroyed += HandleAnythingDestroyed;

            _subscribed = true;
            Log.Info(Tag, "Subscribed to game events");
        }

        // For UniAircraft.OnAircraftDestroyed — this is per-instance.
        // Must be called when we get a reference to the local player's aircraft.
        public void SubscribeToAircraft(UniAircraft aircraft)
        {
            if (aircraft != null)
            {
                aircraft.OnAircraftDestroyed += HandleAircraftDestroyed;
            }
        }

        public void UnsubscribeFromAircraft(UniAircraft aircraft)
        {
            if (aircraft != null)
            {
                aircraft.OnAircraftDestroyed -= HandleAircraftDestroyed;
            }
        }

        private void HandleExplosion(ExplosionEventParams p) => OnExplosion?.Invoke(p);
        private void HandleAircraftDestroyed(UniAircraft a) => OnAircraftDestroyed?.Invoke(a);
        private void HandleTargetAdded(Target t) => OnTargetAdded?.Invoke(t);
        private void HandleTargetRemoved(Target t) => OnTargetRemoved?.Invoke(t);
        private void HandleAnythingDestroyed(DestroyedEvent e) => OnAnythingDestroyed?.Invoke(e);

        public void Dispose()
        {
            if (_subscribed)
            {
                Explosion.OnExplosion -= HandleExplosion;
                TargetManagement.OnTargetAdded -= HandleTargetAdded;
                TargetManagement.OnTargetRemoved -= HandleTargetRemoved;
                Damageable.OnAnythingDestroyed -= HandleAnythingDestroyed;
                _subscribed = false;
                Log.Info(Tag, "Unsubscribed from game events");
            }
        }
    }
}

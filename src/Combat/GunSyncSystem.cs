using System;
using System.Collections.Generic;
using UnityEngine;
using Falcon;
using Falcon.Damage;
using Falcon.Weapons;
using Falcon.Targeting;
using Falcon.UniversalAircraft;
using Falcon.Vehicles;
using TCAMultiplayer.Core;
using TCAMultiplayer.Protocol;
using TCAMultiplayer.Sync;

namespace TCAMultiplayer.Combat
{
    /// <summary>
    /// Synchronizes gun firing state and bullet spawning across multiplayer peers.
    /// Detects local gun fire via Gun2.IsFiring, sends state-change packets,
    /// and spawns remote bullets via Bullet2Manager for visual tracer sync.
    /// </summary>
    public class GunSyncSystem : IDisposable
    {
        private const string Tag = "GUN-SYNC";
        private const float FireSyncInterval = 0.05f; // 20Hz for continuous fire updates

        private readonly GameSession _session;
        private readonly ConnectionManager _connection;
        private readonly PacketRouter _router;
        private readonly RemoteAircraftManager _remoteManager;

        // Local gun state tracking
        private bool _lastFiringState;

        // Per-peer remote gun state
        private readonly Dictionary<ulong, bool> _remoteFiringState = new Dictionary<ulong, bool>();
        private readonly Dictionary<int, float> _lastGunSafetyRefresh = new Dictionary<int, float>();

        private bool _disposed;
        private bool _loggedGunCheck;
        private readonly HashSet<ulong> _loggedRemoteGunCheck = new HashSet<ulong>();

        public GunSyncSystem(
            GameSession session,
            ConnectionManager connection,
            PacketRouter router,
            RemoteAircraftManager remoteManager)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _remoteManager = remoteManager ?? throw new ArgumentNullException(nameof(remoteManager));

            _router.Register(PacketType.GunFiring, HandleGunFiringRaw);
            _router.Register(PacketType.GunStopped, HandleGunStoppedRaw);

            Log.Info(Tag, "Gun sync system initialized");
        }

        // ── Local State Detection ────────────────────────────────────────

        /// <summary>
        /// Called every frame. Detects local gun firing state changes
        /// and sends GunFiring/GunStopped packets to all peers.
        /// </summary>
        public void Update(UniAircraft localAircraft)
        {
            if (_disposed || localAircraft == null) return;

            var gun = localAircraft.FireControl?.Gun;
            if (!_loggedGunCheck)
            {
                _loggedGunCheck = true;
                Log.Info(Tag, $"Local Gun2: found={gun != null}, barrels={gun?.Barrels?.Count ?? 0} (via FireControl)");
            }
            if (gun == null) return;

            bool currentFiring = gun.IsFiring && gun.HasAmmo();

            if (currentFiring != _lastFiringState)
            {
                _lastFiringState = currentFiring;

                if (currentFiring)
                {
                    SendGunFiring();
                }
                else
                {
                    SendGunStopped();
                }
            }
        }

        /// <summary>
        /// Called at physics rate. Spawns bullets for all remotely-firing peers
        /// to create visible tracers on the local client.
        /// </summary>
        public void FixedUpdate()
        {
            // Remote Gun2 is driven by FireControlPatch through the game's native
            // Gun2.Update path. Do not spawn extra manual Bullet2Manager streams here.
        }

        // ── Sending ──────────────────────────────────────────────────────

        private void SendGunFiring()
        {
            var packet = new WeaponFirePacket
            {
                PlayerId = _session.LocalPeerId,
                WeaponType = 0, // Gun
                WeaponIndex = 0,
                TargetId = 0
            };

            var payload = PacketSerializer.SerializeWeaponFire(packet);
            var frame = PacketSerializer.Serialize(PacketType.GunFiring, payload);
            _connection.BroadcastReliable(frame);

            Log.Debug(Tag, $"Sent GunFiring for local peer {_session.LocalPeerId}");
        }

        private void SendGunStopped()
        {
            var payload = BitConverter.GetBytes(_session.LocalPeerId);
            var frame = PacketSerializer.Serialize(PacketType.GunStopped, payload);
            _connection.BroadcastReliable(frame);

            Log.Debug(Tag, $"Sent GunStopped for local peer {_session.LocalPeerId}");
        }

        // ── Receiving ────────────────────────────────────────────────────

        private void HandleGunFiringRaw(ulong fromPeerId, byte[] data)
        {
            var (_, payload) = PacketSerializer.Deserialize(data);
            if (payload == null) return;

            var packet = PacketSerializer.DeserializeWeaponFire(payload);
            ulong peerId = packet.PlayerId;
            if (_session.IsHost && fromPeerId != _session.LocalPeerId && peerId != fromPeerId)
            {
                Log.Warning(Tag, $"Rejected GunFiring from peer {fromPeerId} for peer {peerId}");
                return;
            }

            // Ignore our own packets
            if (peerId == _session.LocalPeerId) return;

            _remoteFiringState[peerId] = true;

            Log.Info(Tag, $"Remote peer {peerId} started firing");
        }

        private void HandleGunStoppedRaw(ulong fromPeerId, byte[] data)
        {
            var (_, payload) = PacketSerializer.Deserialize(data);
            if (payload == null || payload.Length < 8) return;

            ulong peerId = BitConverter.ToUInt64(payload, 0);
            if (_session.IsHost && fromPeerId != _session.LocalPeerId && peerId != fromPeerId)
            {
                Log.Warning(Tag, $"Rejected GunStopped from peer {fromPeerId} for peer {peerId}");
                return;
            }

            // Ignore our own packets
            if (peerId == _session.LocalPeerId) return;

            _remoteFiringState[peerId] = false;

            Log.Info(Tag, $"Remote peer {peerId} stopped firing");
        }

        /// <summary>
        /// Called by FireControlPatch before native Gun2.Update on a remote clone.
        /// This keeps native tracers/muzzle effects while preventing receiver-side
        /// bullets from becoming a second damage authority.
        /// </summary>
        public void ConfigureRemoteGunSafety(FireControl fireControl)
        {
            if (_disposed || fireControl?.Gun == null) return;

            var gun = fireControl.Gun;
            int gunId = fireControl.GetInstanceID();
            float now = Time.time;
            if (_lastGunSafetyRefresh.TryGetValue(gunId, out float lastRefresh)
                && now - lastRefresh < 1f)
                return;
            _lastGunSafetyRefresh[gunId] = now;

            try
            {
                var shooter = fireControl.GetComponentInParent<UniAircraft>();
                foreach (var collider in BuildVisualOnlyIgnoredColliders(shooter))
                    gun.AddIgnoredCollider(collider);
                foreach (var rb in BuildVisualOnlyIgnoredRigidbodies(shooter))
                    gun.AddIgnoredRigidbody(rb);
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"Remote gun safety refresh failed: {ex.Message}");
            }
        }

        private static List<Collider> BuildVisualOnlyIgnoredColliders(UniAircraft shooter)
        {
            var ignored = new HashSet<Collider>();
            AddColliders(ignored, shooter?.GetComponentsInChildren<Collider>(true));

            var damageables = UnityEngine.Object.FindObjectsByType<Damageable>(FindObjectsSortMode.None);
            foreach (var damageable in damageables)
                AddColliders(ignored, damageable?.GetComponentsInChildren<Collider>(true));

            return new List<Collider>(ignored);
        }

        private static List<Rigidbody> BuildVisualOnlyIgnoredRigidbodies(UniAircraft shooter)
        {
            var ignored = new HashSet<Rigidbody>();
            AddRigidbodies(ignored, shooter?.GetComponentsInChildren<Rigidbody>(true));

            var damageables = UnityEngine.Object.FindObjectsByType<Damageable>(FindObjectsSortMode.None);
            foreach (var damageable in damageables)
            {
                var rb = damageable != null ? damageable.GetComponentInParent<Rigidbody>() : null;
                if (rb != null)
                    ignored.Add(rb);
            }

            return new List<Rigidbody>(ignored);
        }

        private static void AddColliders(HashSet<Collider> set, Collider[] colliders)
        {
            if (colliders == null) return;
            foreach (var collider in colliders)
                if (collider != null)
                    set.Add(collider);
        }

        private static void AddRigidbodies(HashSet<Rigidbody> set, Rigidbody[] rigidbodies)
        {
            if (rigidbodies == null) return;
            foreach (var rb in rigidbodies)
                if (rb != null)
                    set.Add(rb);
        }

        // ── Cleanup ──────────────────────────────────────────────────────

        /// <summary>
        /// Clean up a disconnected peer's firing state.
        /// </summary>
        public void RemovePeer(ulong peerId)
        {
            _remoteFiringState.Remove(peerId);
        }

        public void SetPeerFiring(ulong peerId, bool isFiring)
        {
            if (peerId == 0 || peerId == _session.LocalPeerId) return;
            _remoteFiringState[peerId] = isFiring;
        }

        /// <summary>Check if a remote Target's gun is currently firing (used by FireControlPatch).</summary>
        public bool IsRemoteFiring(Falcon.Targeting.Target target)
        {
            if (target == null) return false;
            var ownerAircraft = target.GetComponentInParent<UniAircraft>();
            foreach (var kvp in _remoteFiringState)
            {
                var aircraft = _remoteManager?.GetAircraft(kvp.Key);
                if (aircraft != null && ownerAircraft == aircraft)
                    return kvp.Value;
            }
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _router.Unregister(PacketType.GunFiring, HandleGunFiringRaw);
            _router.Unregister(PacketType.GunStopped, HandleGunStoppedRaw);

            _remoteFiringState.Clear();
            _lastGunSafetyRefresh.Clear();

            Log.Info(Tag, "Gun sync system disposed");
        }
    }
}

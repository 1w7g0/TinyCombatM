using System;
using System.Collections.Generic;
using Falcon.Damage;
using Falcon.Targeting;
using Falcon.UniversalAircraft;
using TCAMultiplayer.Core;
using TCAMultiplayer.Game;
using TCAMultiplayer.Protocol;
using TCAMultiplayer.Sync;
using UnityEngine;

namespace TCAMultiplayer.Combat
{
    /// <summary>
    /// Host-authority aircraft collision detection and sync for N-player.
    /// Only the host runs collision checks; results are broadcast to all peers.
    /// Clients apply damage upon receiving the collision packet.
    /// </summary>
    public class CollisionSyncManager : IDisposable
    {
        private const string Tag = "COLLISION-SYNC";
        private const float CollisionCheckInterval = 0.1f; // 10Hz
        private const float CollisionRadius = 15f; // meters
        private const float MinRelativeSpeed = 10f; // m/s threshold
        private const float DamagePerMeterPerSecond = 5f;
        private const float RecentCollisionCooldown = 2f; // seconds

        private readonly GameSession _session;
        private readonly ConnectionManager _connection;
        private readonly PacketRouter _router;
        private readonly RemoteAircraftManager _remoteManager;
        private readonly FloatingOriginService _originService;
        private readonly Func<UniAircraft> _localAircraftProvider;
        private UniAircraft _localAircraft;

        private float _lastCheckTime;
        private readonly HashSet<(ulong, ulong)> _recentCollisions = new HashSet<(ulong, ulong)>();
        private float _recentCollisionClearTime;
        private bool _disposed;

        public CollisionSyncManager(
            GameSession session,
            ConnectionManager connection,
            PacketRouter router,
            RemoteAircraftManager remoteManager,
            FloatingOriginService originService,
            Func<UniAircraft> localAircraftProvider = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _remoteManager = remoteManager ?? throw new ArgumentNullException(nameof(remoteManager));
            _originService = originService ?? throw new ArgumentNullException(nameof(originService));
            _localAircraftProvider = localAircraftProvider;

            _router.Register(PacketType.AircraftCollision, HandleCollisionRaw);
            Log.Info(Tag, "Initialized");
        }

        /// <summary>
        /// Called every frame from the main update loop.
        /// Only the host performs collision checks.
        /// </summary>
        public void Update(UniAircraft localAircraft)
        {
            if (_disposed) return;
            if (!_session.IsHost || !_session.AircraftCollisionsEnabled) return;
            if (localAircraft == null) return;
            _localAircraft = localAircraft;

            float now = Time.time;

            // Clear stale collision cooldowns
            if (now - _recentCollisionClearTime > RecentCollisionCooldown)
            {
                _recentCollisions.Clear();
                _recentCollisionClearTime = now;
            }

            // Throttle collision checks to 10Hz
            if (now - _lastCheckTime < CollisionCheckInterval) return;
            _lastCheckTime = now;

            CheckCollisions(localAircraft);
        }

        /// <summary>
        /// Host iterates all aircraft pairs and checks for collisions via distance.
        /// </summary>
        private void CheckCollisions(UniAircraft localAircraft)
        {
            var localRb = localAircraft.GetComponent<Rigidbody>();
            if (localRb == null) return;

            Vector3 localPos = localAircraft.transform.position;
            Vector3 localVel = localRb.velocity;
            ulong localId = _session.LocalPeerId;

            // Collect all remote peer IDs into a list for indexed iteration
            var peerIds = new List<ulong>(_remoteManager.GetAllPeerIds());

            // Check local vs each remote
            for (int i = 0; i < peerIds.Count; i++)
            {
                ulong remoteId = peerIds[i];
                var remoteAircraft = _remoteManager.GetAircraft(remoteId);
                if (remoteAircraft == null) continue;

                var remoteRb = remoteAircraft.GetComponent<Rigidbody>();
                if (remoteRb == null) continue;

                TryDetectCollision(
                    localId, localPos, localVel,
                    remoteId, remoteAircraft.transform.position, remoteRb.velocity);
            }

            // Check remote vs remote pairs
            for (int i = 0; i < peerIds.Count; i++)
            {
                var aircraftA = _remoteManager.GetAircraft(peerIds[i]);
                if (aircraftA == null) continue;
                var rbA = aircraftA.GetComponent<Rigidbody>();
                if (rbA == null) continue;

                for (int j = i + 1; j < peerIds.Count; j++)
                {
                    var aircraftB = _remoteManager.GetAircraft(peerIds[j]);
                    if (aircraftB == null) continue;
                    var rbB = aircraftB.GetComponent<Rigidbody>();
                    if (rbB == null) continue;

                    TryDetectCollision(
                        peerIds[i], aircraftA.transform.position, rbA.velocity,
                        peerIds[j], aircraftB.transform.position, rbB.velocity);
                }
            }
        }

        /// <summary>
        /// Check if two aircraft are within collision radius and moving fast enough
        /// relative to each other. If so, broadcast a collision packet.
        /// </summary>
        private void TryDetectCollision(
            ulong idA, Vector3 posA, Vector3 velA,
            ulong idB, Vector3 posB, Vector3 velB)
        {
            // Ensure consistent pair ordering for dedup
            ulong lo = idA < idB ? idA : idB;
            ulong hi = idA < idB ? idB : idA;

            if (_recentCollisions.Contains((lo, hi))) return;

            float distance = Vector3.Distance(posA, posB);
            if (distance > CollisionRadius) return;

            float relativeSpeed = (velA - velB).magnitude;
            if (relativeSpeed < MinRelativeSpeed) return;

            // Collision detected
            _recentCollisions.Add((lo, hi));

            int damageA = Mathf.CeilToInt(relativeSpeed * DamagePerMeterPerSecond);
            int damageB = damageA; // symmetric damage

            Vector3 collisionPos = (posA + posB) * 0.5f;
            Vector3 normal = (posB - posA).normalized;

            _originService.LocalToAbsolute(collisionPos, out double absX, out double absY, out double absZ);

            var packet = new AircraftCollisionPacket
            {
                PlayerA = idA,
                PlayerB = idB,
                PosX = absX,
                PosY = absY,
                PosZ = absZ,
                NormalX = normal.x,
                NormalY = normal.y,
                NormalZ = normal.z,
                DamageA = damageA,
                DamageB = damageB,
                RelativeSpeed = relativeSpeed
            };

            Log.Info(Tag, $"Collision: {idA} <-> {idB}, speed={relativeSpeed:F1}m/s, damage={damageA}");

            // Broadcast to all peers
            byte[] payload = PacketSerializer.SerializeAircraftCollision(packet);
            byte[] data = PacketSerializer.Serialize(PacketType.AircraftCollision, payload);
            _connection.BroadcastReliable(data);

            // Apply damage locally on host as well
            ApplyCollisionDamage(packet);
        }

        /// <summary>
        /// Raw packet handler registered with the router.
        /// Strips the packet type byte and deserializes.
        /// </summary>
        private void HandleCollisionRaw(ulong fromPeerId, byte[] data)
        {
            if (_disposed) return;

            // Host already applied damage when it sent the packet
            if (_session.IsHost) return;

            var (_, payload) = PacketSerializer.Deserialize(data);
            if (payload == null) return;

            var packet = PacketSerializer.DeserializeAircraftCollision(payload);
            if (packet.PlayerA == 0 && packet.PlayerB == 0) return; // deserialization failed

            ApplyCollisionDamage(packet);
        }

        /// <summary>
        /// Apply collision damage to both aircraft involved.
        /// Finds each aircraft (local or remote) and applies a DamageSource impact.
        /// </summary>
        private void ApplyCollisionDamage(AircraftCollisionPacket packet)
        {
            ApplyDamageToAircraft(packet.PlayerA, packet.DamageA, packet);
            ApplyDamageToAircraft(packet.PlayerB, packet.DamageB, packet);
        }

        /// <summary>
        /// Apply collision damage to a single aircraft identified by peer ID.
        /// </summary>
        private void ApplyDamageToAircraft(ulong peerId, int damage, AircraftCollisionPacket packet)
        {
            GameObject aircraftGo = null;

            if (peerId == _session.LocalPeerId)
            {
                var localAircraft = _localAircraft ?? _localAircraftProvider?.Invoke();
                if (localAircraft == null)
                {
                    var playerAircraft = UniAircraft.Player;
                    if (playerAircraft != null && !_remoteManager.IsRemoteTarget(
                            playerAircraft.GetComponentInChildren<Target>()))
                        localAircraft = playerAircraft;
                }
                aircraftGo = localAircraft != null ? localAircraft.gameObject : null;
            }
            else
            {
                Log.Debug(Tag, $"Skipping collision damage for non-local peer {peerId}");
                return;
            }

            if (aircraftGo == null)
            {
                Log.Warning(Tag, $"Cannot find aircraft for peer {peerId} to apply collision damage");
                return;
            }

            var damageable = aircraftGo.GetComponentInChildren<Damageable>();
            if (damageable == null)
            {
                Log.Warning(Tag, $"No Damageable component on aircraft for peer {peerId}");
                return;
            }
            if (damageable.IsDestroyed)
            {
                Log.Debug(Tag, $"Skipping collision damage for already-destroyed peer {peerId}");
                return;
            }

            Vector3 hitPos = _originService.AbsoluteToLocal(packet.PosX, packet.PosY, packet.PosZ);
            Target sourceTarget = ResolveCollisionSourceTarget(peerId, packet);

            // DamageSource: damage, penetration, critHitChance, maxCritHits,
            //   source (Target), hitCollider, hitPos, isCausedByWeapon, weapon
            var damageSource = new DamageSource(
                damage,
                0,          // penetration — N/A for collision
                0,          // critHitChance — no crits from collisions
                0,          // maxCritHits
                sourceTarget,
                null,       // hitCollider — no specific collider
                hitPos,
                false,      // not caused by weapon
                "Collision"
            );

            try
            {
                damageable.ApplyDamageFromImpact(damageSource);
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Failed to apply collision damage to peer {peerId}: {ex.Message}");
            }
        }

        private Target ResolveCollisionSourceTarget(ulong damagedPeerId, AircraftCollisionPacket packet)
        {
            ulong otherPeerId = GetOtherPeerId(damagedPeerId, packet);
            if (otherPeerId == 0 || otherPeerId == damagedPeerId) return null;

            if (otherPeerId == _session.LocalPeerId)
            {
                var localAircraft = _localAircraft ?? _localAircraftProvider?.Invoke() ?? UniAircraft.Player;
                return localAircraft != null ? localAircraft.GetComponentInChildren<Target>() : null;
            }

            var remoteAircraft = _remoteManager.GetAircraft(otherPeerId);
            return remoteAircraft != null ? remoteAircraft.GetComponentInChildren<Target>() : null;
        }

        private static ulong GetOtherPeerId(ulong peerId, AircraftCollisionPacket packet)
        {
            if (packet.PlayerA == peerId) return packet.PlayerB;
            if (packet.PlayerB == peerId) return packet.PlayerA;
            return 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _router.Unregister(PacketType.AircraftCollision, HandleCollisionRaw);
            _localAircraft = null;
            _recentCollisions.Clear();
            Log.Info(Tag, "Disposed");
        }
    }
}

using System;
using System.Reflection;
using UnityEngine;
using Falcon;
using Falcon.Damage;
using Falcon.Targeting;
using Falcon.UniversalAircraft;
using Falcon.Weapons;
using TCAMultiplayer.Core;
using TCAMultiplayer.Game;
using TCAMultiplayer.Protocol;
using TCAMultiplayer.Sync;
using TCAMultiplayer.Patches;

namespace TCAMultiplayer.Combat
{
    /// <summary>
    /// Synchronizes damage and impact VFX across N players.
    /// Per-shooter authority: each shooter is authoritative for their own hits.
    /// Native aircraft destruction is reported by DeathEventCoordinator.
    /// Created per GameSession, disposed with it. No static mutable state.
    /// </summary>
    public class DamageSyncSystem : IDisposable
    {
        private const string Tag = "DAMAGE-SYNC";

        private readonly GameSession _session;
        private readonly ConnectionManager _connection;
        private readonly PacketRouter _router;
        private readonly RemoteAircraftManager _remoteManager;
        private readonly FloatingOriginService _originService;
        private readonly Func<UniAircraft> _localAircraftProvider;
        private readonly FieldInfo _damageableMostRecentDamageField;
        private readonly ApplicationEventSequencer _damageSequencer = new ApplicationEventSequencer();
        private readonly ApplicationEventDedupCache _damageDedup = new ApplicationEventDedupCache();
        private float _lastForwardDiagnosticTime;
        private bool _disposed;

        public event Action<ulong, string> OnRemoteDamageApplied;

        public DamageSyncSystem(
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
            _damageableMostRecentDamageField = typeof(Damageable).GetField(
                "<MostRecentDamage>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            _router.Register(PacketType.DamageDealt, OnDamageDealtReceived);

            DamagePatch.OnCloneDamageBlocked += OnCloneDamageBlocked;
            Log.Info(Tag, "Initialized");
        }

        // ── Outbound: local shooter hits remote clone ────────────────

        /// <summary>Send DamagePacket when local weapon hits a remote clone.</summary>
        public void SendDamage(ulong victimPeerId, DamagePacket packet)
        {
            if (_disposed) return;
            var payload = PacketSerializer.SerializeDamage(packet);
            var data = PacketSerializer.Serialize(PacketType.DamageDealt, payload);
            _connection.BroadcastReliable(data);
            Log.Debug(Tag, $"Sent damage: {packet.Damage} to victim {victimPeerId} weapon={packet.WeaponName}");
        }

        // ── Inbound: receive damage from remote shooter ──────────────

        private void OnDamageDealtReceived(ulong fromPeerId, byte[] rawData)
        {
            var (_, payload) = PacketSerializer.Deserialize(rawData);
            if (payload == null) return;

            var packet = PacketSerializer.DeserializeDamage(payload);
            if (_session.IsHost && fromPeerId != _session.LocalPeerId && packet.AttackerId != fromPeerId)
            {
                Log.Warning(Tag, $"Rejected DamageDealt from peer {fromPeerId} for attacker {packet.AttackerId}");
                return;
            }
            if (packet.VictimId == 0 || packet.VictimId != _session.LocalPeerId)
                return;
            if (packet.AttackerId == _session.LocalPeerId)
                return;
            if (packet.AttackerLifeId != 0)
            {
                var id = new ApplicationEventId(
                    packet.AttackerId,
                    packet.AttackerLifeId,
                    ApplicationEventKind.Damage,
                    packet.DamageSequence,
                    packet.DamageType);
                if (!_damageDedup.TryAccept(id))
                {
                    Log.Debug(Tag, $"Ignoring duplicate/stale damage event {id} weapon={packet.WeaponName}");
                    return;
                }
            }

            var localPlayer = _session.GetLocalPlayer();
            if (localPlayer != null && !localPlayer.IsAlive)
            {
                Log.Debug(Tag, $"Ignoring damage while local player is dead weapon={packet.WeaponName}");
                return;
            }
            if (_session.ArePlayersOnSameTeam(packet.AttackerId, packet.VictimId))
            {
                Log.Debug(Tag, $"Ignoring friendly damage attacker={packet.AttackerId} victim={packet.VictimId}");
                return;
            }
            if (_session.StateMachine.CurrentState != GameState.InGame)
            {
                Log.Debug(Tag, $"Ignoring damage outside InGame state ({_session.StateMachine.CurrentState}) weapon={packet.WeaponName}");
                return;
            }

            var localDamageable = FindLocalDamageable();
            if (localDamageable == null)
            {
                if (localPlayer != null && !localPlayer.IsAlive)
                    Log.Debug(Tag, "Ignoring damage while local aircraft is not alive");
                else
                    Log.Warning(Tag, "Received damage but local Damageable not found");
                return;
            }
            if (localDamageable.IsDestroyed)
            {
                Log.Debug(Tag, "Ignoring damage for already-destroyed local aircraft");
                return;
            }
            Target attackerTarget = ResolveAttackerTarget(packet.AttackerId);
            Vector3 localHitPos = _originService.AbsoluteToLocal(
                packet.HitPosX, packet.HitPosY, packet.HitPosZ);

            // Public 9-param DamageSource constructor — no reflection
            var damageSource = new DamageSource(
                packet.Damage,
                packet.Penetration,
                0,                  // critHitChance
                0,                  // maxCritHits
                attackerTarget,
                null,               // hitCollider — no local reference
                localHitPos,
                true,               // isCausedByWeapon
                packet.WeaponName ?? "Unknown"
            );

            OnRemoteDamageApplied?.Invoke(packet.AttackerId, packet.WeaponName);

            DamagePatch.NetworkDamageDepth++;
            try
            {
                if (packet.DamageType == 2)
                    localDamageable.ApplyDamageFromExplosion(damageSource);
                else
                    localDamageable.ApplyDamageFromImpact(damageSource);
            }
            finally
            {
                DamagePatch.NetworkDamageDepth--;
            }

            Log.Info(Tag, $"Applied {packet.Damage} dmg from {packet.AttackerId} " +
                          $"(type={packet.DamageType}) HP={localDamageable.HitPoints}");
        }

        // ── Helpers ──────────────────────────────────────────────────

        /// <summary>Find local player's Damageable (non-remote UniAircraft).</summary>
        private Damageable FindLocalDamageable()
        {
            var providedAircraft = _localAircraftProvider?.Invoke();
            if (providedAircraft != null && !IsRemoteClone(providedAircraft))
            {
                var providedDamageable = providedAircraft.GetComponentInChildren<Damageable>();
                if (providedDamageable != null) return providedDamageable;
            }

            var player = UniAircraft.Player;
            if (player != null && !IsRemoteClone(player))
            {
                var playerDamageable = player.GetComponentInChildren<Damageable>();
                if (playerDamageable != null) return playerDamageable;
            }

            var allAircraft = UnityEngine.Object.FindObjectsByType<UniAircraft>(FindObjectsSortMode.None);
            foreach (var aircraft in allAircraft)
            {
                var damageable = aircraft.gameObject.GetComponentInChildren<Damageable>();
                if (damageable != null && !_remoteManager.IsRemoteCloneDamageable(damageable))
                    return damageable;
            }
            return null;
        }

        /// <summary>Resolve attacker peer ID → their aircraft's Target component.</summary>
        private Target ResolveAttackerTarget(ulong attackerPeerId)
        {
            var aircraft = _remoteManager.GetAircraft(attackerPeerId);
            if (aircraft == null) return null;
            return aircraft.gameObject.GetComponentInChildren<Target>();
        }

        private bool IsRemoteClone(UniAircraft aircraft)
        {
            if (aircraft == null) return false;
            foreach (var peerId in _remoteManager.GetAllPeerIds())
            {
                if (_remoteManager.GetAircraft(peerId) == aircraft)
                    return true;
            }
            return false;
        }

        private bool IsLocalAircraft(UniAircraft aircraft)
        {
            if (aircraft == null || IsRemoteClone(aircraft)) return false;

            var providedAircraft = _localAircraftProvider?.Invoke();
            if (providedAircraft != null)
                return ReferenceEquals(aircraft, providedAircraft);

            var player = UniAircraft.Player;
            if (player != null && !IsRemoteClone(player))
                return ReferenceEquals(aircraft, player);

            return true;
        }

        private void OnCloneDamageBlocked(Damageable cloneDamageable, DamageSource source, bool isExplosion)
        {
            if (_disposed) return;
            if (string.Equals(source.Weapon, "Collision", StringComparison.OrdinalIgnoreCase))
                return;

            var localPlayer = _session.GetLocalPlayer();
            if (localPlayer != null && !localPlayer.IsAlive)
            {
                Log.Debug(Tag, $"Ignoring clone damage while local player is dead weapon={source.Weapon}");
                return;
            }
            if (_session.StateMachine.CurrentState != GameState.InGame)
            {
                Log.Debug(Tag, $"Ignoring clone damage outside InGame state ({_session.StateMachine.CurrentState}) weapon={source.Weapon}");
                return;
            }
            if (IsEnvironmentWeapon(source.Weapon) && !IsRecentLocalWeaponDamage(source))
            {
                Log.Debug(Tag, $"Ignoring environment clone damage weapon={source.Weapon}");
                return;
            }

            // Find which peer owns this clone
            ulong victimPeerId = 0;
            foreach (var peerId in _remoteManager.GetAllPeerIds())
            {
                var aircraft = _remoteManager.GetAircraft(peerId);
                if (aircraft == null) continue;
                var damageable = aircraft.GetComponentInChildren<Damageable>();
                if (damageable == cloneDamageable)
                {
                    victimPeerId = peerId;
                    break;
                }
            }
            if (victimPeerId == 0) return;
            if (_session.ArePlayersOnSameTeam(_session.LocalPeerId, victimPeerId))
            {
                Log.Debug(Tag, $"Ignoring friendly clone damage victim={victimPeerId} weapon={source.Weapon}");
                return;
            }
            var victim = _session.GetPlayer(victimPeerId);
            if (victim != null
                && (victim.IsAwaitingRespawn || (victim.LifeId != 0 && !victim.IsAlive)))
            {
                Log.Debug(Tag, $"Ignoring clone damage for dead/respawning peer {victimPeerId} weapon={source.Weapon}");
                return;
            }

            // Get hit position in absolute coordinates
            Vector3 hitPos = source.HitPosition;
            _originService.LocalToAbsolute(hitPos, out double absX, out double absY, out double absZ);
            ApplyVisualDamageToRemoteClone(cloneDamageable, source);
            LogForwardDiagnostic(victimPeerId, source, isExplosion);
            var localLifeId = GetLocalLifeId();
            var eventId = _damageSequencer.Next(
                _session.LocalPeerId,
                localLifeId,
                ApplicationEventKind.Damage,
                isExplosion ? (byte)2 : (byte)0);

            var packet = new DamagePacket
            {
                VictimId = victimPeerId,
                AttackerId = _session.LocalPeerId,
                AttackerLifeId = eventId.LifeId,
                DamageSequence = eventId.Sequence,
                Damage = source.Damage,
                Penetration = source.Penetration,
                DamageType = isExplosion ? (byte)2 : (byte)0,
                HitPosX = absX,
                HitPosY = absY,
                HitPosZ = absZ,
                WeaponName = source.Weapon ?? "Unknown"
            };

            SendDamage(victimPeerId, packet);
            Log.Info(Tag, $"Forwarded {source.Damage} damage to peer {victimPeerId} " +
                          $"type={(isExplosion ? "explosion" : "impact")} weapon={source.Weapon} event={eventId}");
        }

        private void ApplyVisualDamageToRemoteClone(Damageable cloneDamageable, DamageSource source)
        {
            if (cloneDamageable == null || source.Damage <= 0) return;

            try
            {
                cloneDamageable.HitPoints = Math.Max(1, cloneDamageable.HitPoints - source.Damage);
                if (source.IsCausedByWeapon && _damageableMostRecentDamageField != null)
                    _damageableMostRecentDamageField.SetValue(cloneDamageable, source);
                cloneDamageable.OnDamaged?.Invoke(source);
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"Remote clone visual damage failed: {ex.Message}");
            }
        }

        // ── Dispose ──────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DamagePatch.OnCloneDamageBlocked -= OnCloneDamageBlocked;
            _router.Unregister(PacketType.DamageDealt, OnDamageDealtReceived);
            OnRemoteDamageApplied = null;

            Log.Info(Tag, "Disposed");
        }

        private static bool IsEnvironmentWeapon(string weaponName)
        {
            return string.Equals(weaponName, "World", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Ground", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Water", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Terrain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Unknown", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(weaponName);
        }

        private static bool IsRecentLocalWeaponDamage(DamageSource source)
        {
            return source.IsCausedByWeapon
                && source.SourceTarget != null;
        }

        private uint GetLocalLifeId()
        {
            var player = _session.GetLocalPlayer();
            if (player == null)
                return 0;
            return player.LifeId != 0
                ? player.LifeId
                : _session.BeginPlayerLife(player.PeerId);
        }

        private void LogForwardDiagnostic(ulong victimPeerId, DamageSource source, bool isExplosion)
        {
            if (Time.time - _lastForwardDiagnosticTime < 1f)
                return;
            _lastForwardDiagnosticTime = Time.time;

            var hitCollider = source.HitCollider;
            var sourceTarget = source.SourceTarget;
            Log.Info(
                Tag,
                $"Forward diag: victim={victimPeerId} damage={source.Damage} " +
                $"weapon={source.Weapon ?? "Unknown"} kind={(isExplosion ? "explosion" : "impact")} " +
                $"hitCollider={hitCollider?.name ?? "null"} sourceTarget={sourceTarget?.name ?? "null"}");
        }
    }
}

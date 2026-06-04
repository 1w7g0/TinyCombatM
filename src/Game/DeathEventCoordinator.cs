using System;
using Falcon.Damage;
using Falcon.Targeting;
using Falcon.UniversalAircraft;
using TCAMultiplayer.Core;
using TCAMultiplayer.Protocol;
using TCAMultiplayer.Sync;
using UnityEngine;

namespace TCAMultiplayer.Game
{
    /// <summary>
    /// Converts native local-aircraft destruction into one multiplayer death report.
    /// ScoreTracker owns the host ledger; damage systems only apply native damage.
    /// </summary>
    public sealed class DeathEventCoordinator : IDisposable
    {
        private const string Tag = "DEATH-COORD";
        private const float RecentRemoteDamageCreditWindow = 8f;

        private readonly GameSession _session;
        private readonly RemoteAircraftManager _remoteManager;
        private readonly GameEventBridge _eventBridge;
        private readonly Func<UniAircraft> _localAircraftProvider;
        private readonly Func<uint> _lifeIdProvider;
        private readonly Action<DeathReportPacket> _deathReporter;

        private Damageable _lastReportedDamageable;
        private UniAircraft _lastReportedAircraft;
        private ulong _recentRemoteAttackerId;
        private string _recentRemoteWeaponName;
        private float _recentRemoteDamageTime;
        private bool _disposed;

        public DeathEventCoordinator(
            GameSession session,
            RemoteAircraftManager remoteManager,
            GameEventBridge eventBridge,
            Func<UniAircraft> localAircraftProvider,
            Func<uint> lifeIdProvider,
            Action<DeathReportPacket> deathReporter)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _remoteManager = remoteManager ?? throw new ArgumentNullException(nameof(remoteManager));
            _eventBridge = eventBridge ?? throw new ArgumentNullException(nameof(eventBridge));
            _localAircraftProvider = localAircraftProvider ?? throw new ArgumentNullException(nameof(localAircraftProvider));
            _lifeIdProvider = lifeIdProvider ?? throw new ArgumentNullException(nameof(lifeIdProvider));
            _deathReporter = deathReporter ?? throw new ArgumentNullException(nameof(deathReporter));

            _eventBridge.OnAircraftDestroyed += HandleAircraftDestroyed;
            Damageable.OnAnythingDestroyed += HandleAnythingDestroyed;
            Log.Info(Tag, "Initialized");
        }

        public void RememberRemoteDamage(ulong attackerId, string weaponName)
        {
            if (_disposed) return;
            if (attackerId == 0 || attackerId == _session.LocalPeerId)
                return;

            _recentRemoteAttackerId = attackerId;
            _recentRemoteWeaponName = string.IsNullOrEmpty(weaponName) ? "Unknown" : weaponName;
            _recentRemoteDamageTime = Time.time;
        }

        public void MarkLocalRespawned()
        {
            _lastReportedDamageable = null;
            _lastReportedAircraft = null;
            _recentRemoteAttackerId = 0;
            _recentRemoteWeaponName = null;
            _recentRemoteDamageTime = 0f;
        }

        private void HandleAircraftDestroyed(UniAircraft aircraft)
        {
            if (_disposed) return;
            if (!IsLocalAircraft(aircraft)) return;

            var damageable = aircraft.Damage ?? aircraft.GetComponentInChildren<Damageable>();
            var damage = damageable != null ? damageable.MostRecentDamage : default(DamageSource);
            ReportLocalAircraftDeath(aircraft, damageable, damage, "critical");
        }

        private void HandleAnythingDestroyed(DestroyedEvent evt)
        {
            if (_disposed) return;

            var destroyed = evt.Destroyed;
            if (destroyed == null) return;
            if (_remoteManager.IsRemoteCloneDamageable(destroyed)) return;
            if (ReferenceEquals(destroyed, _lastReportedDamageable)) return;

            var aircraft = destroyed.GetComponentInParent<UniAircraft>();
            if (!IsLocalAircraft(aircraft)) return;
            if (!IsPrimaryAircraftDamageable(aircraft, destroyed)) return;

            ReportLocalAircraftDeath(aircraft, destroyed, evt.DamageSource, "final-destroy");
        }

        private void ReportLocalAircraftDeath(
            UniAircraft aircraft,
            Damageable damageable,
            DamageSource damage,
            string nativePhase)
        {
            if (aircraft == null) return;
            if (ReferenceEquals(aircraft, _lastReportedAircraft)) return;
            if (damageable != null && ReferenceEquals(damageable, _lastReportedDamageable)) return;

            _lastReportedAircraft = aircraft;
            if (damageable != null)
                _lastReportedDamageable = damageable;

            ulong killerId = ResolveKillerPeerId(damage.SourceTarget);
            string weapon = string.IsNullOrEmpty(damage.Weapon) ? "Unknown" : damage.Weapon;
            string reason = "terrain/self";

            if (killerId == 0 && TryUseRecentRemoteDamage(weapon, out var fallbackKiller, out var fallbackWeapon))
            {
                killerId = fallbackKiller;
                weapon = fallbackWeapon;
                reason = "recent-remote-damage";
            }
            if (_session.ArePlayersOnSameTeam(killerId, _session.LocalPeerId))
            {
                killerId = 0;
                reason = "friendly-fire";
            }
            else if (killerId != 0)
            {
                reason = "killed";
            }

            var report = new DeathReportPacket
            {
                VictimId = _session.LocalPeerId,
                KillerId = killerId,
                LifeId = _lifeIdProvider(),
                WeaponName = weapon,
                Reason = reason
            };

            Log.Info(Tag, $"Native death: phase={nativePhase} victim={report.VictimId} killer={report.KillerId} life={report.LifeId} weapon={report.WeaponName} reason={report.Reason}");
            _deathReporter(report);
        }

        private bool TryUseRecentRemoteDamage(string nativeWeapon, out ulong killerId, out string weaponName)
        {
            killerId = 0;
            weaponName = null;

            if (_recentRemoteAttackerId == 0)
                return false;
            if (Time.time - _recentRemoteDamageTime > RecentRemoteDamageCreditWindow)
                return false;

            killerId = _recentRemoteAttackerId;
            weaponName = !IsEnvironmentWeapon(nativeWeapon)
                ? nativeWeapon
                : (_recentRemoteWeaponName ?? "Unknown");
            return true;
        }

        private ulong ResolveKillerPeerId(Target sourceTarget)
        {
            if (sourceTarget == null)
                return 0;

            var ownerAircraft = sourceTarget.GetComponentInParent<UniAircraft>();
            foreach (var peerId in _remoteManager.GetAllPeerIds())
            {
                var peerAircraft = _remoteManager.GetAircraft(peerId);
                if (peerAircraft != null && ownerAircraft == peerAircraft)
                    return peerId;
            }

            return 0;
        }

        private bool IsLocalAircraft(UniAircraft aircraft)
        {
            if (aircraft == null)
                return false;
            if (IsRemoteClone(aircraft))
                return false;

            var localAircraft = _localAircraftProvider();
            if (localAircraft != null)
                return ReferenceEquals(aircraft, localAircraft);

            var player = UniAircraft.Player;
            return player != null && !IsRemoteClone(player) && ReferenceEquals(aircraft, player);
        }

        private bool IsRemoteClone(UniAircraft aircraft)
        {
            if (aircraft == null)
                return false;

            foreach (var peerId in _remoteManager.GetAllPeerIds())
            {
                if (_remoteManager.GetAircraft(peerId) == aircraft)
                    return true;
            }

            return false;
        }

        private static bool IsPrimaryAircraftDamageable(UniAircraft aircraft, Damageable damageable)
        {
            if (aircraft == null || damageable == null)
                return false;
            if (aircraft.Damage != null)
                return ReferenceEquals(damageable, aircraft.Damage);

            return ReferenceEquals(damageable, aircraft.GetComponentInChildren<Damageable>());
        }

        private static bool IsEnvironmentWeapon(string weaponName)
        {
            return string.IsNullOrEmpty(weaponName)
                || string.Equals(weaponName, "World", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Ground", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Water", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Terrain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(weaponName, "Unknown", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _eventBridge.OnAircraftDestroyed -= HandleAircraftDestroyed;
            Damageable.OnAnythingDestroyed -= HandleAnythingDestroyed;
            _lastReportedDamageable = null;
            _lastReportedAircraft = null;
            Log.Info(Tag, "Disposed");
        }
    }
}

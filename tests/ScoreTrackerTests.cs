using BepInEx.Logging;
using NUnit.Framework;
using TCAMultiplayer.Core;
using TCAMultiplayer.Game;
using TCAMultiplayer.Protocol;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class ScoreTrackerTests
    {
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            if (!Log.IsInitialized)
                Log.Init(new ManualLogSource("Test"));
        }

        [Test]
        public void HandleKillConfirm_DuplicateBeforeRespawn_CountsOnce()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            int confirmedEvents = 0;
            tracker.OnKillConfirmed += _ => confirmedEvents++;

            var packet = new KillConfirmPacket
            {
                KillerId = 1,
                VictimId = 2,
                DeathSequence = 1,
                WeaponName = "Collision"
            };

            tracker.HandleKillConfirm(packet);
            tracker.HandleKillConfirm(packet);

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.AreEqual(1, confirmedEvents);
        }

        [Test]
        public void HandleKillConfirm_AfterRespawn_CountsNextDeath()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            var packet = new KillConfirmPacket
            {
                KillerId = 1,
                VictimId = 2,
                DeathSequence = 1,
                WeaponName = "Collision"
            };

            tracker.HandleKillConfirm(packet);
            victim.IsAlive = true;
            packet.DeathSequence = 2;
            tracker.HandleKillConfirm(packet);

            Assert.AreEqual(2, killer.Kills);
            Assert.AreEqual(2, victim.Deaths);
            Assert.AreEqual(2, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleKillConfirm_SameSequenceAfterRespawn_Ignored()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            var packet = new KillConfirmPacket
            {
                KillerId = 1,
                VictimId = 2,
                DeathSequence = 7,
                WeaponName = "Fire"
            };

            tracker.HandleKillConfirm(packet);
            victim.IsAlive = true;
            tracker.MarkPlayerRespawned(2);
            tracker.HandleKillConfirm(packet);

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleDeathConfirm_CountsDeathWithoutKill()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var local = session.AddPlayer(1, "Pilot");
            var remote = session.AddPlayer(2, "Other");
            local.IsAlive = true;
            remote.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            int deathEvents = 0;
            tracker.OnDeathConfirmed += _ => deathEvents++;

            tracker.HandleDeathConfirm(new AircraftDestroyedPacket
            {
                VictimId = 1,
                DeathSequence = 3,
                Reason = "terrain/self"
            });

            Assert.AreEqual(0, local.Kills);
            Assert.AreEqual(1, local.Deaths);
            Assert.AreEqual(0, remote.Kills);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.AreEqual(1, deathEvents);
        }

        [Test]
        public void HandleDeathConfirm_DuplicateSequence_CountsOnce()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var victim = session.AddPlayer(2, "Victim");
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            var packet = new AircraftDestroyedPacket
            {
                VictimId = 2,
                DeathSequence = 4,
                Reason = "terrain/self"
            };

            tracker.HandleDeathConfirm(packet);
            victim.IsAlive = true;
            tracker.MarkPlayerRespawned(2);
            tracker.HandleDeathConfirm(packet);

            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleDeathConfirm_DifferentSequenceWhileStillDead_CountsOnce()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var victim = session.AddPlayer(2, "Victim");
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleDeathConfirm(new AircraftDestroyedPacket
            {
                VictimId = 2,
                DeathSequence = 4,
                Reason = "terrain/self"
            });
            tracker.HandleDeathConfirm(new AircraftDestroyedPacket
            {
                VictimId = 2,
                DeathSequence = 5,
                Reason = "terrain/self"
            });

            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleKillConfirm_AfterUncreditedDeath_UpgradesWithoutExtraDeath()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleDeathConfirm(new AircraftDestroyedPacket
            {
                VictimId = 2,
                DeathSequence = 4,
                Reason = "terrain/self"
            });
            tracker.HandleKillConfirm(new KillConfirmPacket
            {
                KillerId = 1,
                VictimId = 2,
                DeathSequence = 5,
                WeaponName = "AIM-120B"
            });

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.AreEqual("Killer", tracker.KillFeed[0].KillerName);
            Assert.AreEqual("AIM-120B", tracker.KillFeed[0].WeaponName);
        }

        [Test]
        public void HandleKillConfirm_DifferentSequenceWhileStillDead_CountsOnce()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleKillConfirm(new KillConfirmPacket
            {
                KillerId = 1,
                VictimId = 2,
                DeathSequence = 4,
                WeaponName = "Fire"
            });
            tracker.HandleKillConfirm(new KillConfirmPacket
            {
                KillerId = 1,
                VictimId = 2,
                DeathSequence = 5,
                WeaponName = "Fire"
            });

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleLocalDeathReport_HostRecordsAuthoritativeKill()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = 12,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleLocalDeathReport_UnknownVictim_Ignored()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            killer.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 404,
                LifeId = 12,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(0, killer.Kills);
            Assert.AreEqual(0, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleLocalDeathReport_StaleLifeId_Ignored()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            uint currentLife = session.BeginPlayerLife(victim.PeerId);

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = currentLife + 1,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(0, killer.Kills);
            Assert.AreEqual(0, victim.Deaths);
            Assert.AreEqual(0, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleLocalDeathReport_DeadKillerStillGetsCredit()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = false;
            killer.IsAwaitingRespawn = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = 12,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.AreEqual("AIM-9L", tracker.KillFeed[0].WeaponName);
        }

        [Test]
        public void HandleLocalDeathReport_DuplicateLifeId_CountsOnce()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            uint lifeId = session.BeginPlayerLife(victim.PeerId);

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            var packet = new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = lifeId,
                WeaponName = "AIM-9L",
                Reason = "killed"
            };

            tracker.HandleLocalDeathReport(packet);
            tracker.HandleLocalDeathReport(packet);

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleLocalDeathReport_FriendlyKill_CountsUncreditedDeath()
        {
            var session = new GameSession(isHost: true)
            {
                LocalPeerId = 1,
                GameMode = Core.MultiplayerGameMode.TeamDogfight
            };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.Team = Core.MultiplayerTeam.Team1;
            victim.Team = Core.MultiplayerTeam.Team1;
            killer.IsAlive = true;
            uint lifeId = session.BeginPlayerLife(victim.PeerId);

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = lifeId,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(0, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.IsFalse(tracker.KillFeed[0].HasKillCredit);
            Assert.AreEqual("friendly-fire", tracker.KillFeed[0].KillerName);
        }

        [Test]
        public void HandleLocalDeathReport_SameLifeKillAfterUncreditedDeath_UpgradesWithoutExtraDeath()
        {
            var session = new GameSession(isHost: true) { LocalPeerId = 1 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            uint lifeId = session.BeginPlayerLife(victim.PeerId);

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 0,
                VictimId = 2,
                LifeId = lifeId,
                Reason = "terrain/self"
            });
            tracker.HandleLocalDeathReport(new DeathReportPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = lifeId,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.IsTrue(tracker.KillFeed[0].HasKillCredit);
            Assert.AreEqual("AIM-9L", tracker.KillFeed[0].WeaponName);
        }

        [Test]
        public void HandleScoreEvent_DuplicateLifeId_CountsOnce()
        {
            var session = new GameSession(isHost: false) { LocalPeerId = 2 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);
            var packet = new ScoreEventPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = 12,
                WeaponName = "AIM-9L",
                Reason = "killed"
            };

            tracker.HandleScoreEvent(packet);
            tracker.HandleScoreEvent(packet);

            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleScoreEvent_UnknownKiller_Ignored()
        {
            var session = new GameSession(isHost: false) { LocalPeerId = 2 };
            var victim = session.AddPlayer(2, "Victim");
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleScoreEvent(new ScoreEventPacket
            {
                KillerId = 404,
                VictimId = 2,
                LifeId = 12,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(0, victim.Deaths);
            Assert.AreEqual(0, tracker.KillFeed.Count);
        }

        [Test]
        public void HandleScoreEvent_FriendlyKill_CountsUncreditedDeath()
        {
            var session = new GameSession(isHost: false)
            {
                LocalPeerId = 2,
                GameMode = Core.MultiplayerGameMode.TeamDogfight
            };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.Team = Core.MultiplayerTeam.Team2;
            victim.Team = Core.MultiplayerTeam.Team2;
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleScoreEvent(new ScoreEventPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = 12,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(0, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.IsFalse(tracker.KillFeed[0].HasKillCredit);
            Assert.AreEqual("friendly-fire", tracker.KillFeed[0].KillerName);
        }

        [Test]
        public void HandleScoreEvent_ContradictoryKillAfterUncreditedDeath_Ignored()
        {
            var session = new GameSession(isHost: false) { LocalPeerId = 2 };
            var killer = session.AddPlayer(1, "Killer");
            var victim = session.AddPlayer(2, "Victim");
            killer.IsAlive = true;
            victim.IsAlive = true;

            var tracker = new ScoreTracker(session, new PacketRouter(), () => 123f);

            tracker.HandleScoreEvent(new ScoreEventPacket
            {
                KillerId = 0,
                VictimId = 2,
                LifeId = 12,
                Reason = "terrain/self"
            });
            tracker.HandleScoreEvent(new ScoreEventPacket
            {
                KillerId = 1,
                VictimId = 2,
                LifeId = 13,
                WeaponName = "AIM-9L",
                Reason = "killed"
            });

            Assert.AreEqual(0, killer.Kills);
            Assert.AreEqual(1, victim.Deaths);
            Assert.AreEqual(1, tracker.KillFeed.Count);
            Assert.IsFalse(tracker.KillFeed[0].HasKillCredit);
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class StateMachineTests
    {
        private GameStateMachine _sm;

        [SetUp]
        public void SetUp()
        {
            _sm = new GameStateMachine();
        }

        // ═══════════════════════════════════════════════════════════
        //  A. Valid transitions — every edge in the matrix
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void Disconnected_To_HostingLobby()
        {
            Assert.IsTrue(_sm.TryTransition(GameState.HostingLobby));
            Assert.AreEqual(GameState.HostingLobby, _sm.CurrentState);
        }

        [Test]
        public void Disconnected_To_ClientLobby()
        {
            Assert.IsTrue(_sm.TryTransition(GameState.ClientLobby));
            Assert.AreEqual(GameState.ClientLobby, _sm.CurrentState);
        }

        [Test]
        public void HostingLobby_To_Loading()
        {
            _sm.TryTransition(GameState.HostingLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.Loading));
            Assert.AreEqual(GameState.Loading, _sm.CurrentState);
        }

        [Test]
        public void HostingLobby_To_Disconnected()
        {
            _sm.TryTransition(GameState.HostingLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void ClientLobby_To_Loading()
        {
            _sm.TryTransition(GameState.ClientLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.Loading));
            Assert.AreEqual(GameState.Loading, _sm.CurrentState);
        }

        [Test]
        public void ClientLobby_To_Disconnected()
        {
            _sm.TryTransition(GameState.ClientLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Loading_To_Spawning()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);

            Assert.IsTrue(_sm.TryTransition(GameState.Spawning));
            Assert.AreEqual(GameState.Spawning, _sm.CurrentState);
        }

        [Test]
        public void Loading_To_Disconnected()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Loading_To_ReturningToLobby()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);

            Assert.IsTrue(_sm.TryTransition(GameState.ReturningToLobby));
            Assert.AreEqual(GameState.ReturningToLobby, _sm.CurrentState);
        }

        [Test]
        public void Spawning_To_InGame()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);

            Assert.IsTrue(_sm.TryTransition(GameState.InGame));
            Assert.AreEqual(GameState.InGame, _sm.CurrentState);
        }

        [Test]
        public void Spawning_To_Disconnected()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Spawning_To_ReturningToLobby()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);

            Assert.IsTrue(_sm.TryTransition(GameState.ReturningToLobby));
            Assert.AreEqual(GameState.ReturningToLobby, _sm.CurrentState);
        }

        [Test]
        public void InGame_To_Respawning()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);

            Assert.IsTrue(_sm.TryTransition(GameState.Respawning));
            Assert.AreEqual(GameState.Respawning, _sm.CurrentState);
        }

        [Test]
        public void InGame_To_ReturningToLobby()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);

            Assert.IsTrue(_sm.TryTransition(GameState.ReturningToLobby));
            Assert.AreEqual(GameState.ReturningToLobby, _sm.CurrentState);
        }

        [Test]
        public void InGame_To_Disconnected()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Respawning_To_Spawning()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);
            _sm.TryTransition(GameState.Respawning);

            Assert.IsTrue(_sm.TryTransition(GameState.Spawning));
            Assert.AreEqual(GameState.Spawning, _sm.CurrentState);
        }

        [Test]
        public void Respawning_To_ReturningToLobby()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);
            _sm.TryTransition(GameState.Respawning);

            Assert.IsTrue(_sm.TryTransition(GameState.ReturningToLobby));
            Assert.AreEqual(GameState.ReturningToLobby, _sm.CurrentState);
        }

        [Test]
        public void Respawning_To_Disconnected()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);
            _sm.TryTransition(GameState.Respawning);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void ReturningToLobby_To_HostingLobby()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.ReturningToLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.HostingLobby));
            Assert.AreEqual(GameState.HostingLobby, _sm.CurrentState);
        }

        [Test]
        public void ReturningToLobby_To_ClientLobby()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.ReturningToLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.ClientLobby));
            Assert.AreEqual(GameState.ClientLobby, _sm.CurrentState);
        }

        [Test]
        public void ReturningToLobby_To_Disconnected()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.ReturningToLobby);

            Assert.IsTrue(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        // ═══════════════════════════════════════════════════════════
        //  B. Invalid transitions — return false, state unchanged
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void Disconnected_To_InGame_Invalid()
        {
            Assert.IsFalse(_sm.TryTransition(GameState.InGame));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Disconnected_To_Spawning_Invalid()
        {
            Assert.IsFalse(_sm.TryTransition(GameState.Spawning));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void InGame_To_HostingLobby_Invalid()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);
            _sm.TryTransition(GameState.InGame);

            Assert.IsFalse(_sm.TryTransition(GameState.HostingLobby));
            Assert.AreEqual(GameState.InGame, _sm.CurrentState);
        }

        [Test]
        public void Loading_To_InGame_Invalid()
        {
            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);

            Assert.IsFalse(_sm.TryTransition(GameState.InGame));
            Assert.AreEqual(GameState.Loading, _sm.CurrentState);
        }

        [Test]
        public void SameState_Transition_ReturnsFalse()
        {
            // Disconnected → Disconnected should fail
            Assert.IsFalse(_sm.TryTransition(GameState.Disconnected));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Disconnected_To_Loading_Invalid()
        {
            Assert.IsFalse(_sm.TryTransition(GameState.Loading));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Disconnected_To_Respawning_Invalid()
        {
            Assert.IsFalse(_sm.TryTransition(GameState.Respawning));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        [Test]
        public void Disconnected_To_ReturningToLobby_Invalid()
        {
            Assert.IsFalse(_sm.TryTransition(GameState.ReturningToLobby));
            Assert.AreEqual(GameState.Disconnected, _sm.CurrentState);
        }

        // ═══════════════════════════════════════════════════════════
        //  C. Event tests — OnStateChanged
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void OnStateChanged_Fires_WithCorrectValues()
        {
            GameState? capturedOld = null;
            GameState? capturedNew = null;
            _sm.OnStateChanged += (old, @new) =>
            {
                capturedOld = old;
                capturedNew = @new;
            };

            _sm.TryTransition(GameState.HostingLobby);

            Assert.AreEqual(GameState.Disconnected, capturedOld);
            Assert.AreEqual(GameState.HostingLobby, capturedNew);
        }

        [Test]
        public void OnStateChanged_Fires_OnEveryValidTransition()
        {
            var transitions = new List<(GameState old, GameState @new)>();
            _sm.OnStateChanged += (old, @new) => transitions.Add((old, @new));

            _sm.TryTransition(GameState.HostingLobby);
            _sm.TryTransition(GameState.Loading);
            _sm.TryTransition(GameState.Spawning);

            Assert.AreEqual(3, transitions.Count);
            Assert.AreEqual((GameState.Disconnected, GameState.HostingLobby), transitions[0]);
            Assert.AreEqual((GameState.HostingLobby, GameState.Loading), transitions[1]);
            Assert.AreEqual((GameState.Loading, GameState.Spawning), transitions[2]);
        }

        [Test]
        public void OnStateChanged_DoesNotFire_OnInvalidTransition()
        {
            int fireCount = 0;
            _sm.OnStateChanged += (old, @new) => fireCount++;

            _sm.TryTransition(GameState.InGame); // invalid from Disconnected

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void OnStateChanged_DoesNotFire_OnSameStateTransition()
        {
            int fireCount = 0;
            _sm.OnStateChanged += (old, @new) => fireCount++;

            _sm.TryTransition(GameState.Disconnected); // same state

            Assert.AreEqual(0, fireCount);
        }

        // ═══════════════════════════════════════════════════════════
        //  D. GameSession integration
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void GameSession_Dispose_ResetsStateMachineToDisconnected()
        {
            var session = new GameSession(isHost: true);
            session.StateMachine.TryTransition(GameState.HostingLobby);
            session.StateMachine.TryTransition(GameState.Loading);

            session.Dispose();

            Assert.AreEqual(GameState.Disconnected, session.StateMachine.CurrentState);
        }

        [Test]
        public void GameSession_Dispose_ClearsPlayers()
        {
            var session = new GameSession(isHost: true);
            session.LocalPeerId = 1;
            session.AddPlayer(42, "Alpha");
            session.AddPlayer(43, "Bravo");
            Assert.AreEqual(2, session.PlayerCount);

            session.Dispose();

            Assert.AreEqual(0, session.PlayerCount);
        }

        [Test]
        public void GameSession_AddPlayer_IncrementsCount()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                Assert.AreEqual(0, session.PlayerCount);

                session.AddPlayer(10, "Pilot1");
                Assert.AreEqual(1, session.PlayerCount);

                session.AddPlayer(20, "Pilot2");
                Assert.AreEqual(2, session.PlayerCount);
            }
        }

        [Test]
        public void GameSession_RemovePlayer_DecrementsCount()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                session.AddPlayer(10, "Pilot1");
                session.AddPlayer(20, "Pilot2");
                Assert.AreEqual(2, session.PlayerCount);

                session.RemovePlayer(10);
                Assert.AreEqual(1, session.PlayerCount);
            }
        }

        [Test]
        public void GameSession_RemovePlayer_NonExistent_NoOp()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                session.AddPlayer(10, "Pilot1");

                session.RemovePlayer(999); // does not exist
                Assert.AreEqual(1, session.PlayerCount);
            }
        }

        [Test]
        public void GameSession_AddPlayer_DuplicatePeerId_UpdatesName()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                var first = session.AddPlayer(42, "OldName");
                var second = session.AddPlayer(42, "NewName");

                Assert.AreSame(first, second);
                Assert.AreEqual("NewName", second.PlayerName);
                Assert.AreEqual(1, session.PlayerCount);
            }
        }

        [Test]
        public void GameSession_BeginPlayerLife_MarksAliveAndIncrementsLifeId()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                var player = session.AddPlayer(1, "Pilot");

                uint firstLife = session.BeginPlayerLife(1);
                uint secondLife = session.BeginPlayerLife(1);

                Assert.AreEqual(1u, firstLife);
                Assert.AreEqual(2u, secondLife);
                Assert.IsTrue(player.IsAlive);
                Assert.IsFalse(player.IsAwaitingRespawn);
                Assert.AreEqual(2u, player.LifeId);
            }
        }

        [Test]
        public void GameSession_EndPlayerLife_MarksAwaitingRespawnAndKeepsLifeId()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                var player = session.AddPlayer(1, "Pilot");
                uint lifeId = session.BeginPlayerLife(1);

                uint endedLife = session.EndPlayerLife(1);

                Assert.AreEqual(lifeId, endedLife);
                Assert.IsFalse(player.IsAlive);
                Assert.IsTrue(player.IsAwaitingRespawn);
                Assert.AreEqual(lifeId, player.LifeId);
            }
        }

        [Test]
        public void GameSession_IsCurrentLiveLife_RejectsDeadOrOldLife()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                session.AddPlayer(1, "Pilot");

                uint firstLife = session.BeginPlayerLife(1);
                Assert.IsTrue(session.IsCurrentLiveLife(1, firstLife));

                session.EndPlayerLife(1);
                Assert.IsFalse(session.IsCurrentLiveLife(1, firstLife));

                uint secondLife = session.BeginPlayerLife(1);
                Assert.IsFalse(session.IsCurrentLiveLife(1, firstLife));
                Assert.IsTrue(session.IsCurrentLiveLife(1, secondLife));
            }
        }

        [Test]
        public void GameSession_GetPlayer_ReturnsNullForUnknownPeer()
        {
            using (var session = new GameSession(isHost: true))
            {
                Assert.IsNull(session.GetPlayer(999));
            }
        }

        [Test]
        public void GameSession_ClampMaxPlayersTotal_LimitsToEightPlayers()
        {
            Assert.AreEqual(1, GameSession.ClampMaxPlayersTotal(0));
            Assert.AreEqual(4, GameSession.ClampMaxPlayersTotal(4));
            Assert.AreEqual(8, GameSession.ClampMaxPlayersTotal(8));
            Assert.AreEqual(8, GameSession.ClampMaxPlayersTotal(64));
        }

        [Test]
        public void GameSession_ArePlayersOnSameTeam_OnlyTrueInTeamMode()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                var first = session.AddPlayer(1, "One");
                var second = session.AddPlayer(2, "Two");
                first.Team = MultiplayerTeam.Team1;
                second.Team = MultiplayerTeam.Team1;

                Assert.IsFalse(session.ArePlayersOnSameTeam(1, 2));

                session.GameMode = MultiplayerGameMode.TeamDogfight;
                Assert.IsTrue(session.ArePlayersOnSameTeam(1, 2));

                second.Team = MultiplayerTeam.Team2;
                Assert.IsFalse(session.ArePlayersOnSameTeam(1, 2));

                second.Team = MultiplayerTeam.None;
                Assert.IsFalse(session.ArePlayersOnSameTeam(1, 2));
            }
        }

        [Test]
        public void GameSession_StateChanged_BubblesFromStateMachine()
        {
            using (var session = new GameSession(isHost: true))
            {
                GameState? capturedOld = null;
                GameState? capturedNew = null;
                session.OnStateChanged += (old, @new) =>
                {
                    capturedOld = old;
                    capturedNew = @new;
                };

                session.StateMachine.TryTransition(GameState.HostingLobby);

                Assert.AreEqual(GameState.Disconnected, capturedOld);
                Assert.AreEqual(GameState.HostingLobby, capturedNew);
            }
        }

        [Test]
        public void GameSession_Dispose_ThrowsOnSubsequentAddPlayer()
        {
            var session = new GameSession(isHost: true);
            session.Dispose();

            Assert.Throws<ObjectDisposedException>(() => session.AddPlayer(1, "Test"));
        }

        [Test]
        public void GameSession_Dispose_ThrowsOnSubsequentRemovePlayer()
        {
            var session = new GameSession(isHost: true);
            session.Dispose();

            Assert.Throws<ObjectDisposedException>(() => session.RemovePlayer(1));
        }

        [Test]
        public void GameSession_DoubleDispose_DoesNotThrow()
        {
            var session = new GameSession(isHost: true);
            session.Dispose();

            Assert.DoesNotThrow(() => session.Dispose());
        }
    }
}

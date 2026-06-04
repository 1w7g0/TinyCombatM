using NUnit.Framework;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class SmokeTest
    {
        [Test]
        public void GameStateMachine_StartsDisconnected()
        {
            var sm = new GameStateMachine();
            Assert.AreEqual(GameState.Disconnected, sm.CurrentState);
        }

        [Test]
        public void GameSession_CreatesAndDisposes()
        {
            var session = new GameSession(isHost: true);
            Assert.IsNotNull(session);
            Assert.IsTrue(session.IsHost);
            session.Dispose();
        }

        [Test]
        public void GameStateMachine_ValidTransition_Succeeds()
        {
            var sm = new GameStateMachine();
            bool result = sm.TryTransition(GameState.HostingLobby);
            Assert.IsTrue(result);
            Assert.AreEqual(GameState.HostingLobby, sm.CurrentState);
        }

        [Test]
        public void GameStateMachine_InvalidTransition_ReturnsFalse()
        {
            var sm = new GameStateMachine();
            // Cannot go directly from Disconnected to InGame
            bool result = sm.TryTransition(GameState.InGame);
            Assert.IsFalse(result);
            Assert.AreEqual(GameState.Disconnected, sm.CurrentState);
        }

        [Test]
        public void GameSession_Dispose_ResetsToDisconnected()
        {
            var session = new GameSession(isHost: true);
            session.StateMachine.TryTransition(GameState.HostingLobby);
            Assert.AreEqual(GameState.HostingLobby, session.StateMachine.CurrentState);
            session.Dispose();
            Assert.AreEqual(GameState.Disconnected, session.StateMachine.CurrentState);
        }

        [Test]
        public void GameSession_AddPlayer_RoundTrips()
        {
            using (var session = new GameSession(isHost: true))
            {
                session.LocalPeerId = 1;
                var player = session.AddPlayer(42, "TestPilot");
                Assert.IsNotNull(player);
                Assert.AreEqual("TestPilot", player.PlayerName);
                Assert.AreEqual((ulong)42, player.PeerId);
                Assert.AreEqual(1, session.PlayerCount);

                var fetched = session.GetPlayer(42);
                Assert.AreSame(player, fetched);
            }
        }
    }
}

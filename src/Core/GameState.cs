using System;
using System.Collections.Generic;

namespace TCAMultiplayer.Core
{
    /// <summary>
    /// All possible states in the multiplayer game flow.
    /// </summary>
    public enum GameState
    {
        /// <summary>Not connected to any session.</summary>
        Disconnected,

        /// <summary>Host has created lobby, waiting for players.</summary>
        HostingLobby,

        /// <summary>Client has joined lobby, waiting for game start.</summary>
        ClientLobby,

        /// <summary>Game world is loading.</summary>
        Loading,

        /// <summary>Players are spawning into the world.</summary>
        Spawning,

        /// <summary>Active gameplay.</summary>
        InGame,

        /// <summary>Player is dead, waiting to respawn.</summary>
        Respawning,

        /// <summary>Transitioning back to lobby.</summary>
        ReturningToLobby
    }

    /// <summary>
    /// How players spawn into the mission.
    /// </summary>
    public enum LobbySpawnType
    {
        InAir,
        Runway,
        Ramp
    }

    /// <summary>
    /// Time-of-day setting for the mission.
    /// Mirrors Falcon.World.TimeOfDay without creating a hard Core→Game dependency.
    /// </summary>
    public enum TimeOfDaySetting
    {
        Dawn,
        Morning,
        Noon,
        Afternoon,
        Evening,
        Night
    }

    /// <summary>
    /// Multiplayer-owned game mode. Native FlightGame remains Freeflight; this
    /// controls lobby rules, scoring, and friendly-fire behavior.
    /// </summary>
    public enum MultiplayerGameMode
    {
        FreeForAllDogfight = 0,
        TeamDogfight = 1
    }

    /// <summary>
    /// Multiplayer team identity. Native TCA only has Blue/Red/Neutral coalitions;
    /// Team3/Team4 are enforced by the mod's scoring and damage rules.
    /// </summary>
    public enum MultiplayerTeam
    {
        None = 0,
        Team1 = 1,
        Team2 = 2,
        Team3 = 3,
        Team4 = 4
    }

    /// <summary>
    /// State machine with validated transitions.
    /// Invalid transitions return false — they never throw.
    /// </summary>
    public class GameStateMachine
    {
        private readonly Dictionary<GameState, HashSet<GameState>> ValidTransitions =
            new Dictionary<GameState, HashSet<GameState>>
            {
                [GameState.Disconnected] = new HashSet<GameState>
                {
                    GameState.HostingLobby,
                    GameState.ClientLobby
                },
                [GameState.HostingLobby] = new HashSet<GameState>
                {
                    GameState.Loading,
                    GameState.Disconnected
                },
                [GameState.ClientLobby] = new HashSet<GameState>
                {
                    GameState.Loading,
                    GameState.Disconnected
                },
                [GameState.Loading] = new HashSet<GameState>
                {
                    GameState.Spawning,
                    GameState.Disconnected,
                    GameState.ReturningToLobby
                },
                [GameState.Spawning] = new HashSet<GameState>
                {
                    GameState.InGame,
                    GameState.Disconnected,
                    GameState.ReturningToLobby
                },
                [GameState.InGame] = new HashSet<GameState>
                {
                    GameState.Respawning,
                    GameState.ReturningToLobby,
                    GameState.Disconnected
                },
                [GameState.Respawning] = new HashSet<GameState>
                {
                    GameState.Spawning,
                    GameState.ReturningToLobby,
                    GameState.Disconnected
                },
                [GameState.ReturningToLobby] = new HashSet<GameState>
                {
                    GameState.HostingLobby,
                    GameState.ClientLobby,
                    GameState.Disconnected
                }
            };

        /// <summary>Current state of the game session.</summary>
        public GameState CurrentState { get; private set; } = GameState.Disconnected;

        /// <summary>Raised after every successful transition (oldState, newState).</summary>
        public event Action<GameState, GameState> OnStateChanged;

        /// <summary>
        /// Attempt a state transition. Returns true if the transition is valid and was applied.
        /// Returns false (never throws) if the transition is not allowed.
        /// </summary>
        public bool TryTransition(GameState newState)
        {
            if (newState == CurrentState)
                return false;

            if (!ValidTransitions.TryGetValue(CurrentState, out var allowed) || !allowed.Contains(newState))
                return false;

            var oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
            return true;
        }

        /// <summary>
        /// Force-reset to Disconnected. Used only by Dispose paths.
        /// </summary>
        internal void Reset()
        {
            var old = CurrentState;
            CurrentState = GameState.Disconnected;
            if (old != GameState.Disconnected)
                OnStateChanged?.Invoke(old, GameState.Disconnected);
        }
    }
}

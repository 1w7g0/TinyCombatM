using System;
using BepInEx.Logging;

namespace TCAMultiplayer.Core
{
    /// <summary>
    /// Static logging wrapper.
    /// Call <see cref="Init"/> once from the plugin entry point.
    /// Falls back to UnityEngine.Debug.Log when no BepInEx logger is available (e.g. in tests).
    /// Does NOT depend on Plugin.Instance.
    /// </summary>
    public static class Log
    {
        private static ManualLogSource _logger;

        /// <summary>
        /// Initialize with a BepInEx log source. Safe to call multiple times (last writer wins).
        /// </summary>
        public static void Init(ManualLogSource logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Whether the BepInEx logger has been set.</summary>
        public static bool IsInitialized => _logger != null;

        // ── Public API ──────────────────────────────────────────────────

        public static void Info(string message)
        {
            if (_logger != null)
                _logger.LogInfo(message);
            else
                UnityEngine.Debug.Log($"[TCAMP] {message}");
        }

        public static void Warning(string message)
        {
            if (_logger != null)
                _logger.LogWarning(message);
            else
                UnityEngine.Debug.LogWarning($"[TCAMP] {message}");
        }

        public static void Error(string message)
        {
            if (_logger != null)
                _logger.LogError(message);
            else
                UnityEngine.Debug.LogError($"[TCAMP] {message}");
        }

        public static void Debug(string message)
        {
            if (_logger != null)
                _logger.LogDebug(message);
            else
                UnityEngine.Debug.Log($"[TCAMP][DBG] {message}");
        }

        /// <summary>
        /// Convenience overload: log with a category tag, e.g. [NET], [PKT].
        /// </summary>
        public static void Info(string tag, string message)  => Info($"[{tag}] {message}");
        public static void Warning(string tag, string message) => Warning($"[{tag}] {message}");
        public static void Error(string tag, string message)   => Error($"[{tag}] {message}");
        public static void Debug(string tag, string message)   => Debug($"[{tag}] {message}");
    }
}

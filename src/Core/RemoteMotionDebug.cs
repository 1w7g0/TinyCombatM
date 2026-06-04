using System;
using System.IO;
using UnityEngine;

namespace TCAMultiplayer.Core
{
    /// <summary>Runtime-toggleable diagnostics for remote aircraft motion debugging.</summary>
    public static class RemoteMotionDebug
    {
        private const string Tag = "REMOTE-DBG";
        private static float _nextPollTime;
        private static bool _flagFileEnabled;
        private static bool _lastEnabled;
        private static bool _hasLoggedState;

        public static bool Enabled
        {
            get
            {
                Poll();
                return (ModConfig.RemoteMotionDebugEnabled?.Value ?? false) || _flagFileEnabled;
            }
        }

        public static float LogIntervalSeconds
        {
            get
            {
                float interval = ModConfig.RemoteMotionDebugLogIntervalSeconds?.Value ?? 0.25f;
                return Mathf.Clamp(interval, 0.05f, 5f);
            }
        }

        public static float DrawScale
        {
            get
            {
                float scale = ModConfig.RemoteMotionDebugDrawScale?.Value ?? 0.20f;
                return Mathf.Clamp(scale, 0.01f, 2f);
            }
        }

        public static string FlagFilePath => ResolveFlagFilePath();

        public static void Poll()
        {
            float now = Time.unscaledTime;
            if (now < _nextPollTime)
                return;

            _nextPollTime = now + 0.25f;
            _flagFileEnabled = File.Exists(ResolveFlagFilePath());

            bool enabled = (ModConfig.RemoteMotionDebugEnabled?.Value ?? false) || _flagFileEnabled;
            if (!_hasLoggedState || enabled != _lastEnabled)
            {
                _hasLoggedState = true;
                _lastEnabled = enabled;
                Log.Info(Tag, $"Motion debug {(enabled ? "enabled" : "disabled")} " +
                              $"config={ModConfig.RemoteMotionDebugEnabled?.Value ?? false} " +
                              $"flag={_flagFileEnabled} path={ResolveFlagFilePath()}");
            }
        }

        private static string ResolveFlagFilePath()
        {
            string configured = ModConfig.RemoteMotionDebugFlagFile?.Value;
            if (string.IsNullOrWhiteSpace(configured))
                configured = "TCAMP.remote-motion-debug.flag";

            try
            {
                if (Path.IsPathRooted(configured))
                    return configured;

                string configDir = ModConfig.ConfigDirectory;
                if (!string.IsNullOrEmpty(configDir))
                    return Path.Combine(configDir, configured);
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"Could not resolve debug flag path '{configured}': {ex.Message}");
            }

            return configured;
        }
    }
}

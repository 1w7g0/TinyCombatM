using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Falcon.World;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Patches
{
    /// <summary>
    /// Deterministic cloud/wind sync for multiplayer.
    /// Replaces RNG seed before cloud generation and uses synced time for wind.
    /// </summary>
    [HarmonyPatch]
    internal static class EnvironmentPatch
    {
        /// <summary>Returns a deterministic seed when in MP, or null for singleplayer.</summary>
        public static Func<int?> GetDeterministicSeed;

        /// <summary>Returns true when in an active multiplayer session.</summary>
        public static Func<bool> IsMultiplayerSession;

        // Cached reflection for WindVelocityMS (private setter)
        private static PropertyInfo _windVelProp;
        private static bool _windVelCached;

        [HarmonyPatch(typeof(CloudMeshes), "InitializeClouds")]
        [HarmonyPrefix]
        static void CloudsPrefix()
        {
            try
            {
                int? seed = GetDeterministicSeed?.Invoke();
                if (seed.HasValue)
                {
                    UnityEngine.Random.InitState(seed.Value);
                    Log.Info("ENV", $"Deterministic cloud seed: {seed.Value}");
                }
            }
            catch (Exception ex)
            {
                Log.Error("ENV", $"Cloud seed error: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(Falcon.World.Environment), "UpdateWindVector")]
        [HarmonyPrefix]
        static bool WindPrefix(Falcon.World.Environment __instance, Component ___GlobalWind)
        {
            try
            {
                if (IsMultiplayerSession?.Invoke() != true) return true;

                // Synced time from time-of-day (deterministic across all clients)
                float t = (float)__instance.TODTimespan.TotalSeconds;

                float speed = __instance.WindSpeedMS
                    + Mathf.PerlinNoise(0f, t) * __instance.WindTurbulence;
                float heading = __instance.WindHeading + 180f
                    + Falcon.Utilities.Perlin.Noise(t, 50f) * (__instance.WindSpeedMS / 4f)
                    + Falcon.Utilities.Perlin.Noise(t, 200f) * (__instance.WindTurbulence / 2f);
                float vert = Falcon.Utilities.Perlin.Noise(t, 100f)
                    * Mathf.Sqrt(__instance.WindSpeedMS + __instance.WindTurbulence);

                ___GlobalWind.transform.rotation =
                    Quaternion.AngleAxis(heading, Vector3.up)
                    * Quaternion.AngleAxis(vert, Vector3.right);

                // WindZone lives in a separate unreferenced module — use reflection
                var wzType = ___GlobalWind.GetType();
                wzType.GetProperty("windMain")?.SetValue(___GlobalWind, __instance.WindSpeedMS, null);
                wzType.GetProperty("windTurbulence")?.SetValue(___GlobalWind, __instance.WindTurbulence, null);

                // WindVelocityMS auto-property with private setter
                if (!_windVelCached)
                {
                    _windVelCached = true;
                    _windVelProp = typeof(Falcon.World.Environment).GetProperty(
                        "WindVelocityMS", BindingFlags.Public | BindingFlags.Instance);
                }
                _windVelProp?.SetValue(__instance, ___GlobalWind.transform.forward * speed, null);

                return false; // skip original
            }
            catch (Exception ex)
            {
                Log.Error("ENV", $"Wind sync error: {ex.Message}");
                return true; // fallback to original
            }
        }
    }
}

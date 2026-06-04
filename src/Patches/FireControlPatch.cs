using System;
using HarmonyLib;
using UnityEngine;
using Falcon.Vehicles;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Patches
{
    /// <summary>
    /// Controls gun firing on remote aircraft clones.
    /// Remote guns use network-synced firing state instead of local input.
    /// </summary>
    [HarmonyPatch(typeof(FireControl), "Update")]
    internal static class FireControlPatch
    {
        /// <summary>
        /// Return true if this FireControl belongs to a remote aircraft clone.
        /// </summary>
        public static Func<FireControl, bool> IsRemote;

        /// <summary>
        /// Return true if the remote aircraft's gun should be firing.
        /// </summary>
        public static Func<FireControl, bool> GetRemoteFiring;

        /// <summary>
        /// Let the session layer configure remote native guns before Gun2.Update().
        /// </summary>
        public static Action<FireControl> ConfigureRemoteGun;

        [HarmonyPrefix]
        static bool Prefix(FireControl __instance)
        {
            try
            {
                // Local aircraft — let original Update run unmodified
                if (IsRemote?.Invoke(__instance) != true)
                    return true;

                bool shouldFire = GetRemoteFiring?.Invoke(__instance) ?? false;

                if (__instance.Gun == null)
                    return false; // no gun initialized yet, skip

                ConfigureRemoteGun?.Invoke(__instance);
                __instance.Gun.IsFiring = shouldFire;
                __instance.Gun.Update(Time.timeAsDouble, Time.deltaTime);

                return false; // skip original Update for remote aircraft
            }
            catch (Exception ex)
            {
                Log.Error("FIRE-CTL", $"Remote fire control error: {ex.Message}");
                return true; // fallback to original on error
            }
        }
    }
}

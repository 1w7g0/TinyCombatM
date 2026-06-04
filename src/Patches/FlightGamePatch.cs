using HarmonyLib;
using Falcon.Game2;

namespace TCAMultiplayer.Patches
{
    /// <summary>
    /// Hooks into FlightGame.Update for per-frame state reading.
    /// The actual state reading logic is in Sync module — this just provides the hook.
    /// </summary>
    [HarmonyPatch(typeof(FlightGame))]
    internal static class FlightGamePatch
    {
        public static event System.Action OnFlightGameUpdate;
        public static event System.Action OnFlightGameAwake;
        public static event System.Action OnFlightGameDestroy;

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        static void UpdatePostfix() => OnFlightGameUpdate?.Invoke();

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void AwakePostfix() => OnFlightGameAwake?.Invoke();

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroy")]
        static void OnDestroyPostfix() => OnFlightGameDestroy?.Invoke();
    }
}

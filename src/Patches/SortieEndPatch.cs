using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Falcon.Game2.UI;
using HarmonyLib;
using UnityEngine.UI;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Patches
{
    /// <summary>
    /// Intercepts sortie-end actions so only the host can end the mission in MP.
    /// Clients see the button hidden; host triggers return-to-lobby.
    /// </summary>
    [HarmonyPatch]
    internal static class SortieEndPatch
    {
        /// <summary>Returns true when in an active multiplayer session.</summary>
        public static Func<bool> IsMultiplayerSession;

        /// <summary>Returns true when the local player is host.</summary>
        public static Func<bool> IsHost;

        /// <summary>Called when the host confirms returning to lobby. Arg = source name.</summary>
        public static Action<string> OnRequestReturnToLobby;

        private static readonly FieldInfo PauseFinishBtn =
            AccessTools.Field(typeof(PauseMenu), "FinishMissionButton");

        private static readonly FieldInfo RearmEndBtn =
            AccessTools.Field(typeof(RearmRefuelDialog), "EndSortieButton");

        [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.ShowPauseMenu))]
        [HarmonyPostfix]
        static void PausePostfix(PauseMenu __instance, ref UniTask<PauseMenu.Result> __result)
        {
            if (IsMultiplayerSession?.Invoke() != true) return;
            __result = WrapPause(__instance, __result);
        }

        [HarmonyPatch(typeof(RearmRefuelDialog), nameof(RearmRefuelDialog.RunLoadoutSelector))]
        [HarmonyPostfix]
        static void RearmPostfix(RearmRefuelDialog __instance,
            ref UniTask<RearmRefuelDialog.RearmResult> __result)
        {
            if (IsMultiplayerSession?.Invoke() != true) return;
            __result = WrapRearm(__instance, __result);
        }

        private static async UniTask<PauseMenu.Result> WrapPause(
            PauseMenu menu, UniTask<PauseMenu.Result> original)
        {
            bool host = IsHost?.Invoke() ?? false;
            SetButtonActive(PauseFinishBtn, menu, host);

            var result = await original;
            if (result != PauseMenu.Result.FinishMission) return result;

            if (host)
                OnRequestReturnToLobby?.Invoke("PauseMenu");
            else
                Log.Info("SORTIE", "Client blocked from ending sortie (pause menu)");

            return PauseMenu.Result.Resume;
        }

        private static async UniTask<RearmRefuelDialog.RearmResult> WrapRearm(
            RearmRefuelDialog dialog, UniTask<RearmRefuelDialog.RearmResult> original)
        {
            bool host = IsHost?.Invoke() ?? false;
            SetButtonActive(RearmEndBtn, dialog, host);

            var result = await original;
            if (result.Action != RearmRefuelDialog.Action.EndSortie) return result;

            if (host)
                OnRequestReturnToLobby?.Invoke("RearmRefuel");
            else
                Log.Info("SORTIE", "Client blocked from ending sortie (rearm dialog)");

            result.Action = RearmRefuelDialog.Action.Resume;
            return result;
        }

        private static void SetButtonActive(FieldInfo field, object target, bool active)
        {
            try
            {
                if (field?.GetValue(target) is Button btn && btn.gameObject != null)
                {
                    btn.gameObject.SetActive(active);
                    btn.interactable = active;
                }
            }
            catch { /* UI hierarchy may shift during dialog init */ }
        }
    }
}

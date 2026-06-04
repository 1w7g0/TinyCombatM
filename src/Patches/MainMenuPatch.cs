using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TCAMultiplayer.UI;
using Falcon.Game2;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Patches
{
    /// <summary>
    /// Injects a Multiplayer button into the main menu, placed after the Arena button.
    /// </summary>
    [HarmonyPatch(typeof(MainMenu))]
    internal static class MainMenuPatch
    {
        private static GameObject _buttonGo;

        /// <summary>Called when the Multiplayer button is clicked. Receives the MainMenu instance.</summary>
        public static Action<MainMenu> OnMultiplayerClicked;

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void AwakePostfix(MainMenu __instance, Button ___ArenaButton)
        {
            if (___ArenaButton == null)
            {
                Log.Warning("MENU", "ArenaButton not found — cannot inject Multiplayer button");
                return;
            }

            // Unity-safe null check: destroyed objects pass C# null but not Unity's operator
            if (_buttonGo != null && _buttonGo) return;
            _buttonGo = null; // clear stale reference

            // Initialize UIFactory with game's native prefabs
            UIFactory.Initialize(__instance);

            try
            {
                _buttonGo = UnityEngine.Object.Instantiate(
                    ___ArenaButton.gameObject, ___ArenaButton.transform.parent);
                _buttonGo.name = "MultiplayerButton";

                int idx = ___ArenaButton.transform.GetSiblingIndex();
                _buttonGo.transform.SetSiblingIndex(idx + 1);

                var text = _buttonGo.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = "MULTIPLAYER";

                var btn = _buttonGo.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    Log.Info("MENU", "Multiplayer button clicked");
                    OnMultiplayerClicked?.Invoke(__instance);
                });

                Log.Info("MENU", "Injected Multiplayer button into main menu");
            }
            catch (Exception ex)
            {
                Log.Error("MENU", $"Failed to inject button: {ex.Message}");
            }
        }
    }
}

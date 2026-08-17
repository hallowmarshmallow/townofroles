using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Shows the mod's system messages (role notifications, command feedback,
    /// and host broadcasts) through the game's native "SYSTEM ALERT" popup
    /// (HudManager.ChatPopup.ShowWarning) instead of a chat bubble. Every call
    /// site routes through <see cref="Show"/> so no per-message styling is needed.
    /// </summary>
    internal static class SystemChat
    {
        public const string SenderName = "Town of Roles";

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<int> ColorIndex { get; private set; }
        public static ConfigEntry<string> HatQuery { get; private set; }

        private static HatBehaviour _catHat;
        private static int? _resolvedColor;

        public static void Init(ConfigFile config)
        {
            Enabled = config.Bind(
                "SystemChat", "Enabled", true,
                "Style the mod's system messages as the Town of Roles mascot (pink crewmate + black-cat hat) instead of the default warning bubble.");
            ColorIndex = config.Bind(
                "SystemChat", "ColorIndex", 3,
                "Player palette index of the mascot's body color. 3 = pink in the classic palette.");
            HatQuery = config.Bind(
                "SystemChat", "HatQuery", "cat",
                "Substring matched (case-insensitively) against hat store names / product ids to pick the mascot's hat. Use 'black' to prefer the black-cat hat.");
        }

        public static void Reset()
        {
            _catHat = null;
            _resolvedColor = null;
        }

        /// <summary>
        /// Shows a system message through the game's native "SYSTEM ALERT" popup
        /// (HudManager.ChatPopup.ShowWarning). Falls back to a log line when the
        /// HUD/popup is not available (lobby, main menu). Cosmetic only — never
        /// throws.
        /// </summary>
        public static void Show(string message)
        {
            try
            {
                var popup = HudManager.Instance?.ChatPopup;
                if (popup == null)
                {
                    BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogInfo(message);
                    return;
                }
                popup.ShowWarning(message);
            }
            catch
            {
                // Non-fatal: the popup must never crash gameplay code.
            }
        }

        [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChatWarning))]
        internal static class ChatController_AddChatWarning_StylePatch
        {
            private static void Postfix(ChatController __instance)
            {
                try { StyleLatestBubble(__instance); }
                catch { /* cosmetic only */ }
            }
        }

        private static void StyleLatestBubble(ChatController chat)
        {
            // ChatBubble is internal in the 2026.8.9 interop.  Do not reach into
            // its private layout fields from a mod: AddChatWarning remains fully
            // functional, while this optional mascot reskin is safely skipped until
            // a reflection adapter is maintained for the new chat prefab.
            return;
        }

        private static HatBehaviour ResolveCatHat()
        {
            if (_catHat != null) return _catHat;
            try
            {
                var hats = HatManager.Instance?.AllHats;
                if (hats == null || hats.Count == 0) return null;
                var query = (HatQuery?.Value ?? "cat").Trim().ToLowerInvariant();
                if (query.Length == 0) query = "cat";
                HatBehaviour best = null;
                for (var i = 0; i < hats.Count; i++)
                {
                    var h = hats.get_Item(i);
                    if (h == null) continue;
                    var hay = ((h.StoreName ?? "") + " " + (h.ProductId ?? "")).ToLowerInvariant();
                    if (hay.IndexOf(query, StringComparison.Ordinal) < 0) continue;
                    if (best == null || hay.Contains("black")) best = h;
                }
                _catHat = best;
            }
            catch
            {
                _catHat = null;
            }
            return _catHat;
        }

        private static int ResolveColor()
        {
            if (_resolvedColor.HasValue) return _resolvedColor.Value;
            var palette = Palette.PlayerColors;
            var color = ColorIndex?.Value ?? 3;
            if (palette == null) color = 3;
            else if (color < 0 || color >= palette.Length) color = 0;
            _resolvedColor = color;
            return color;
        }

        private const string SystemHatName = "SystemChatHat";
    }
}

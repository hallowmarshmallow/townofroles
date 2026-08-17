using System;
using HarmonyLib;
using TownOfUs.ManuAPI.Roles.Arsonist;
using TownOfUs.ManuAPI.Roles.Executioner;
using TownOfUs.ManuAPI.Roles.Glitch;
using TownOfUs.ManuAPI.Roles.Jester;
using TownOfUs.ManuAPI.Roles.Phantom;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Keeps the custom exile reveal text ("X was the Jester." / "was the
    /// Executioner." / "was the Arsonist." / "was the Phantom." / "was The
    /// Glitch.") applied for the whole exile animation.
    ///
    /// The original port patched the compiler-generated coroutine type
    /// ExileController._Animate_d__17.MoveNext, which Classic Us 2026.8.9's
    /// interop exposes only as a private nested type — it cannot be referenced
    /// from a mod assembly (CS0426/CS0117). Instead of touching the coroutine
    /// state machine, this polls the public static ExileController.Instance
    /// every frame and re-applies the text whenever the exiled player carries a
    /// custom role. That mirrors the coroutine postfix cadence (the game's Begin
    /// body overwrites completeString after our Begin prefix runs, so the re-apply
    /// must keep running through the animation).
    /// </summary>
    internal static class ExileTextFix
    {
        /// <summary>
        /// Cached per-exile resolution so the per-frame tick only re-assigns a
        /// string while a single exile animation is running (the player list is
        /// iterated once when the exiled player changes, not every frame).
        /// </summary>
        private static byte _exiledPlayerId = byte.MaxValue;
        private static string _cachedText;

        /// <summary>
        /// Per-frame upkeep, called from a HudManager.Update postfix. Cheap:
        /// one null check when no exile is running.
        /// </summary>
        public static void Tick()
        {
            var controller = ExileController.Instance;
            if (controller == null)
            {
                _exiledPlayerId = byte.MaxValue;
                _cachedText = null;
                return;
            }
            // completeString / exiled are protected in the 2026.8.9 interop —
            // access them through the reflection adapter (see GameReflection).
            var exiled = GameReflection.GetExileExiled(controller);
            if (exiled == null)
            {
                _cachedText = null;
                return;
            }
            // Resolve the reveal text once per exiled player; re-apply it for
            // the rest of the animation (the game's Begin body overwrites
            // completeString after our Begin prefix runs, so the re-apply must
            // keep running through the animation).
            if (_exiledPlayerId != exiled.PlayerId || _cachedText == null)
            {
                _cachedText = GetExileText(exiled);
                _exiledPlayerId = exiled.PlayerId;
            }
            var text = _cachedText;
            if (text == null) return;
            try
            {
                GameReflection.SetCompleteString(controller, text);
                if (controller.Text != null) controller.Text.Text = text;
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Exile text: " + e.Message);
            }
        }

        private static string GetExileText(GameData.PlayerInfo exiled)
        {
            // AllPlayerControls can be briefly null during scene transitions.
            if (PlayerControl.AllPlayerControls == null) return null;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.PlayerId != exiled.PlayerId) continue;
                if (JesterSystem.IsJester(player)) return exiled.PlayerName + " was the Jester.";
                if (ExecutionerSystem.IsActiveExecutioner(player)) return exiled.PlayerName + " was the Executioner.";
                if (ArsonistSystem.IsArsonist(player)) return exiled.PlayerName + " was the Arsonist.";
                if (PhantomSystem.IsPhantom(player)) return exiled.PlayerName + " was the Phantom.";
                if (GlitchSystem.IsGlitch(player)) return exiled.PlayerName + " was The Glitch.";
                return null;
            }
            return null;
        }
    }

    /// <summary>
    /// Drives ExileTextFix.Tick every frame. Installed unconditionally — inert
    /// (single null check) unless an exile animation is actually running.
    /// </summary>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    internal static class HudManager_Update_ExileTextFixPatch
    {
        private static void Postfix()
        {
            try
            {
                ExileTextFix.Tick();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("ExileTextFix: " + e.Message);
            }
        }
    }
}

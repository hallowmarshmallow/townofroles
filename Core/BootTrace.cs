using System;
using BepInEx.Logging;
using HarmonyLib;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Startup crash diagnostics. Each marker logs a line from a real game
    /// lifecycle callback so a crash log shows exactly how far the game booted
    /// before the process aborted:
    ///   [TOU-BOOT] M1  -> after chainloader, before the boot scene callbacks
    ///   [TOU-BOOT] M2  -> AmongUsClient.Awake ran (core client object alive)
    ///   [TOU-BOOT] M3  -> MainMenuManager.Start ran (main menu reached)
    ///   [TOU-BOOT] M4  -> VersionShower.Start ran (version text built)
    ///   [TOU-BOOT] M5  -> GameStartManager.Start ran (lobby reached)
    ///   [TOU-BOOT] M6  -> HudManager.Start ran (in-game HUD built)
    ///   [TOU-BOOT] M7  -> MeetingHud.Start ran (a meeting opened)
    /// Every marker is wrapped in try/catch so a marker can never crash the game.
    /// </summary>
    internal static class BootTrace
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("TOU-BOOT");

        public static void Mark(string label)
        {
            try
            {
                Log.LogInfo(label);
            }
            catch
            {
                // Logging must never crash.
            }
        }

        // M1 is logged from the end of Load() so there is always at least one
        // marker even if every scene patch below fails to resolve.

        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Awake))]
        internal static class M2_AmongUsClient_Awake
        {
            private static void Postfix() { try { BootTrace.Mark("M2 AmongUsClient.Awake"); } catch { } }
        }

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        internal static class M3_MainMenuManager_Start
        {
            private static void Postfix() { try { BootTrace.Mark("M3 MainMenuManager.Start"); } catch { } }
        }

        [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
        internal static class M4_VersionShower_Start
        {
            private static void Postfix() { try { BootTrace.Mark("M4 VersionShower.Start"); } catch { } }
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
        internal static class M5_GameStartManager_Start
        {
            private static void Postfix() { try { BootTrace.Mark("M5 GameStartManager.Start"); } catch { } }
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
        internal static class M6_HudManager_Start
        {
            private static void Postfix() { try { BootTrace.Mark("M6 HudManager.Start"); } catch { } }
        }

        // "Start" is private in the 2026.8.9 interop; string form matches the
        // role systems' MeetingHud patches.
        [HarmonyPatch(typeof(MeetingHud), "Start")]
        internal static class M7_MeetingHud_Start
        {
            private static void Postfix() { try { BootTrace.Mark("M7 MeetingHud.Start"); } catch { } }
        }
    }
}

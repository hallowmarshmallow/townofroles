using HarmonyLib;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Commands
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
    internal static class PlayerControl_RpcSendChat_CommandPatch
    {
        private static bool Prefix(PlayerControl __instance, string chatText)
        {
            if (CommandConfig.Enabled?.Value != true) return true;
            if (__instance == null || __instance != PlayerControl.LocalPlayer) return true;
            return !CommandSystem.TryHandle(__instance, chatText);
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.CoClose))]
    internal static class ChatController_Close_AlwaysCommandPatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix()
        {
            // Keep the native chat panel open when explicitly requested. The
            // command backend still works if this toggle is disabled.
            return !(CommandConfig.Enabled?.Value == true && CommandConfig.AlwaysCommandChat?.Value == true);
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_VisualEffectsPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance == null || __instance != PlayerControl.LocalPlayer) return;
            if (CommandConfig.Enabled?.Value != true) return;
            CommandSystem.TickLocalEffects();
        }
    }
}

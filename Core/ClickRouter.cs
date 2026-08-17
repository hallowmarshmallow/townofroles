using System;
using System.Collections.Generic;
using HarmonyLib;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Delegate-free click dispatch for the mod's custom UI buttons.
    ///
    /// Classic Us never marshals managed delegates into Il2Cpp at runtime: its
    /// entire UI flows through the native pipeline
    /// (PassiveButtonManager.Update -> PassiveUiElement.ReceiveClickDown ->
    /// PassiveButton.OnClick.Invoke) with listeners wired in prefabs/scenes,
    /// and the game's protection terminates the process (SIGKILL) the first
    /// time a managed delegate is converted into an Il2Cpp delegate — e.g.
    /// OnClick.AddListener(managed UnityAction), which is exactly what the
    /// mod's custom UI previously did when building buttons.
    ///
    /// Cloned buttons still flow through the same native pipeline, so instead
    /// of AddListener we give each button's PassiveButton GameObject a unique
    /// name, register a handler here, and intercept PassiveButton.
    /// ReceiveClickDown in a Harmony prefix: when the name matches, the handler
    /// runs and the prefix returns false so the native (dead) OnClick listeners
    /// on the clone are never invoked. No managed delegate is ever marshalled.
    /// </summary>
    internal static class ClickRouter
    {
        private static readonly Dictionary<string, Action> Handlers = new();

        public static void Register(string buttonId, Action handler)
        {
            if (buttonId == null || handler == null) return;
            Handlers[buttonId] = handler;
        }

        public static void Unregister(string buttonId)
        {
            if (buttonId != null) Handlers.Remove(buttonId);
        }

        public static void UnregisterAll(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids) Handlers.Remove(id);
        }

        public static void Reset() => Handlers.Clear();

        public static bool TryDispatch(string buttonId)
        {
            if (buttonId == null || !Handlers.TryGetValue(buttonId, out var handler)) return false;
            try
            {
                handler();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("Town of Roles")
                    .LogError("ClickRouter dispatch '" + buttonId + "': " + e.Message);
            }
            return true;
        }
    }

    /// <summary>
    /// Routes clicks on the mod's named buttons through ClickRouter before the
    /// native OnClick listeners run. Returning false for a handled button means
    /// the clone's OnClick (which may still reference the destroyed
    /// KillButtonManager) is never invoked.
    /// </summary>
    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickDown))]
    internal static class PassiveButton_ReceiveClickDown_ClickRouterPatch
    {
        private static bool Prefix(PassiveButton __instance)
        {
            if (__instance == null || __instance.gameObject == null) return true;
            return !ClickRouter.TryDispatch(__instance.gameObject.name);
        }
    }
}

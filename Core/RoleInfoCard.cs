using System;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Role info card in the tasks tab (ported from the original Town-Of-Us'
    /// role-description display).
    ///
    /// The vanilla game builds a card at the top of the task list from
    /// PlayerControl.importantTextTask (an ImportantTextTask with Text/Color).
    /// For virtual custom roles the backing role is CrewmateRole, so that card
    /// shows the vanilla crewmate text. This patch rewrites it to \"Your Role:
    /// X — description\" whenever the local player holds a custom role, and
    /// leaves the vanilla text untouched otherwise. Re-applied on a throttle so
    /// the game recreating the task after NewTaskText() is handled.
    /// </summary>
    internal static class RoleInfoCard
    {
        private static float _nextCheck;
        private static string _originalText; // vanilla ImportantTextTask.Text captured at first overwrite

        public static void Reset()
        {
            _originalText = null;
        }

        public static void Poll(PlayerControl local)
        {
            if (local == null || local.Data == null) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;

            try
            {
                var task = local.importantTextTask;
                if (task == null) return;

                if (RolePresentation.TryGet(local, out var roleName, out var roleColor))
                {
                    // Cache the vanilla text exactly once, before our first write,
                    // so we can hand it back when the player stops holding a role.
                    if (_originalText == null) _originalText = task.Text;
                    var card = "Your Role: " + roleName + "\n" + DescriptionFor(roleName);
                    if (task.Text != card)
                    {
                        task.Text = card;
                        task.Color = roleColor;
                    }
                }
                else if (_originalText != null && task.Text != null && task.Text.StartsWith("Your Role:", StringComparison.Ordinal))
                {
                    // Role was converted away / reset: restore the vanilla card
                    // instead of showing a stale role card or wiping the text.
                    task.Text = _originalText;
                    _originalText = null;
                }
            }
            catch { }
        }

        private static string DescriptionFor(string roleName) => RoleCatalog.TaskTextFor(roleName);
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    internal static class HudManager_Update_RoleInfoCardPatch
    {
        private static void Postfix()
        {
            try
            {
                RoleInfoCard.Poll(PlayerControl.LocalPlayer);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Role info card: " + e.Message);
            }
        }
    }
}

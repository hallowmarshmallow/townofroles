using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TownOfUs.ManuAPI.Roles
{
    /// <summary>
    /// ManuAPI adds virtual-role files to TaskAdderGame.TaskParent, but the stock
    /// insertion path keeps advancing one horizontal row. Reflow only role files
    /// after that insertion so they stay inside the folder and continue on rows.
    /// </summary>
    internal static class FreeplayRoleLayout
    {
        private const int Columns = 4;
        private const float ColumnSpacing = 0.82f;
        private const float RowSpacing = 0.68f;

        internal static void Reflow(TaskAdderGame game)
        {
            if (game == null || game.ActiveItems == null) return;

            var roleButtons = new List<Transform>();
            for (int i = 0; i < game.ActiveItems.Count; i++)
            {
                var item = game.ActiveItems[i];
                if (item == null) continue;
                var button = item.GetComponent<TaskAddButton>();
                if (button != null && button.IsRole) roleButtons.Add(item);
            }

            if (roleButtons.Count == 0) return;

            // Sort role buttons by their current screen order before reflowing.
            // This avoids using an arbitrary insertion order that can push items
            // off the intended folder grid.
            roleButtons.Sort((a, b) =>
            {
                var yCompare = b.localPosition.y.CompareTo(a.localPosition.y);
                return yCompare != 0 ? yCompare : a.localPosition.x.CompareTo(b.localPosition.x);
            });

            // Center the grid around the folder's existing role-button location.
            // This preserves the game's canvas/camera setup while constraining every
            // entry to four columns and as many visible rows as are needed.
            var origin = roleButtons[0].localPosition;
            origin.x -= ((Math.Min(Columns, roleButtons.Count) - 1) * ColumnSpacing) * 0.5f;

            for (int i = 0; i < roleButtons.Count; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                var target = roleButtons[i];
                target.localPosition = new Vector3(
                    origin.x + column * ColumnSpacing,
                    origin.y - row * RowSpacing,
                    target.localPosition.z);
            }

            // The game only sizes the folder scroller in ShowFolder (task
            // folders); OpenRoleFolder lays out its rows without touching the
            // scroller. Once the mod's role buttons exceed the visible rows, the
            // bottom rows land past the scrollable area and go off-screen with no
            // way to reach them. Mirror ShowFolder's own sizing call using the
            // mod's grid geometry so every row is scrollable.
            var scroller = game.scroller;
            if (scroller != null)
            {
                try
                {
                    scroller.CalculateAndSetYBounds(roleButtons.Count, Columns, 3.95f, RowSpacing);
                    scroller.SetYBoundsMin(0f);
                }
                catch (Exception e)
                {
                    BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogWarning("Freeplay role scroller resize: " + e.Message);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TaskAdderGame), nameof(TaskAdderGame.OpenRoleFolder))]
    [HarmonyPriority(Priority.Last)]
    internal static class TaskAdderGame_OpenRoleFolder_TownOfUsLayoutPatch
    {
        private static void Postfix(TaskAdderGame __instance)
        {
            try { FreeplayRoleLayout.Reflow(__instance); }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Freeplay role layout: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TaskAdderGame), nameof(TaskAdderGame.ApplyClickMask))]
    [HarmonyPriority(Priority.Last)]
    internal static class TaskAdderGame_ApplyClickMask_TownOfUsLayoutPatch
    {
        private static void Postfix(TaskAdderGame __instance)
        {
            try { FreeplayRoleLayout.Reflow(__instance); }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Freeplay role layout refresh: " + e.Message);
            }
        }
    }
}

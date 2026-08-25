using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// A small arrow button pinned to the top-right of Classic Us' customize
    /// window. Clicking it swaps the window's content to the Town Of Us roles
    /// page (NativeRolesPage): the game's own settings rows are hidden and
    /// native Crewmate / Impostor / Neutral rows take their place. Clicking
    /// again restores the native rows.
    ///
    /// The arrow is a KillButton clone (same proven primitive as the overlay's
    /// buttons): delegate-free clicks via ClickRouter, positioned just right
    /// of the rightmost tab button so it is guaranteed to sit inside the
    /// visible window.
    /// </summary>
    internal static class NativeMenuArrow
    {
        private static CustomPlayerMenu _trackedCpm;
        private static GameObject _arrow;
        private static TextMeshPro _arrowText;
        private static bool _anchorGood;

        /// <summary>True once the arrow exists for the current window instance.</summary>
        public static bool IsActive => _trackedCpm != null && _arrow != null && _arrow;

        /// <summary>
        /// Destroys any rows that the fallback Game-tab injection placed before
        /// the arrow was built (SettingMenu.OnEnable can fire during
        /// Instantiate, before CustomPlayerMenu.Start has set Instance, so the
        /// fallback may have run first).
        /// </summary>
        public static void RemoveFallbackRows(SettingMenu menu)
        {
            if (menu == null || menu.menu == null || menu.menu.transform == null) return;
            try
            {
                var parent = menu.menu.transform;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var child = parent.GetChild(i);
                    if (child == null || child.name == null) continue;
                    if (child.name.StartsWith("ToU_Row_", StringComparison.OrdinalIgnoreCase))
                        UnityEngine.Object.Destroy(child.gameObject);
                }
            }
            catch (Exception e)
            {
                Log("cleanup fallback: " + e.Message);
            }
        }

        /// <summary>
        /// Build the arrow for the active settings window, once per
        /// CustomPlayerMenu instance. Returns true when the arrow is live (in
        /// which case the caller skips the old Game-tab row injection).
        /// </summary>
        public static bool Ensure(SettingMenu menu)
        {
            if (RoleConfig.NativeMenuRows?.Value != true) return false;
            if (menu == null || menu.menu == null || menu.transform == null) return false;
            try
            {
                // Find the window that actually hosts this SettingMenu by walking
                // up the hierarchy. CustomPlayerMenu.Instance is ambiguous — the
                // main-menu cosmetics screen is also a CustomPlayerMenu whose
                // Start() overwrites Instance, which would build the arrow on an
                // invisible window.
                var cpm = FindCustomPlayerMenu(menu);
                if (cpm == null || cpm.transform == null) return false;

                // The window is re-instantiated from scratch every open; rebuild
                // only when the instance changed, or when the previous build ran
                // before rows existed (so its anchor was only a guess).
                if (_trackedCpm == cpm && _arrow != null && _arrow && _anchorGood) return true;

                if (_trackedCpm != cpm) NativeRolesPage.Reset(); // stale page belongs to the old window

                _trackedCpm = cpm;
                _anchorGood = false;
                if (_arrow != null) UnityEngine.Object.Destroy(_arrow); // drop a stale guess before rebuilding
                _arrow = null;
                BuildArrow(cpm, menu);
                if (_arrow != null)
                {
                    RemoveFallbackRows(menu); // undo any pre-Start Game-tab injection
                    Log(_anchorGood
                        ? "arrow created at " + _arrow.transform.position.ToString("F2") + " (top-right anchor)"
                        : "arrow created (no tabs/rows yet, will re-anchor on next open)");
                }
                return _arrow != null;
            }
            catch (Exception e)
            {
                Log("create arrow: " + e.Message);
                _trackedCpm = null;
                return false;
            }
        }

        /// <summary>
        /// The SettingMenu lives inside the window that owns the Hat/Skin/Pet/Game
        /// tabs, so walking up from it finds the exact CustomPlayerMenu instance
        /// that is on screen — no dependence on the (ambiguous) static Instance.
        /// </summary>
        private static CustomPlayerMenu FindCustomPlayerMenu(SettingMenu menu)
        {
            var t = menu.transform;
            while (t != null)
            {
                var cpm = t.GetComponent<CustomPlayerMenu>();
                if (cpm != null) return cpm;
                t = t.parent;
            }
            return null;
        }

        private static void BuildArrow(CustomPlayerMenu cpm, SettingMenu menu)
        {
            var hud = HudManager.Instance;
            if (hud == null || hud.KillButton == null) return;

            var clone = UnityEngine.Object.Instantiate(hud.KillButton.gameObject, cpm.transform);
            clone.name = "ToU_Nav_Arrow";

            // Keep only visuals + click handling (same strip as the overlay).
            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.TryCast<PassiveButton>() != null) continue;
                if (comp.TryCast<TextMeshPro>() != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            // Background: prefer root SpriteRenderer, fall back to largest.
            SpriteRenderer background = clone.GetComponent<SpriteRenderer>();
            if (background == null || background.sprite == null)
            {
                background = null;
                float largest = -1f;
                foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.sprite == null) continue;
                    var size = sr.sprite.bounds.size;
                    float area = size.x * size.y;
                    if (area > largest) { largest = area; background = sr; }
                }
            }
            foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && sr != background) sr.enabled = false;

            if (background != null)
            {
                // Bright amber so it stands out against the window's own dark
                // background (the overlay's dark-slate buttons only worked
                // because they sat on a near-black fullscreen backdrop).
                background.color = new Color(1f, 0.78f, 0.25f, 0.98f);
                background.sortingOrder = 520; // above the native window
            }

            var tmp = clone.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
            {
                tmp.text = NativeRolesPage.IsActive ? "\u00AB" : "\u00BB"; // « while the roles page is open, » otherwise
                tmp.fontSize = Mathf.Max(tmp.fontSize, 2f);
                tmp.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                tmp.sortingOrder = 530;
            }
            _arrowText = tmp;

            var t = clone.transform;
            // The clone is a child of cpm.transform, so assign world position
            // (localPosition would be relative to the window's own transform).
            t.position = ComputeArrowPosition(menu, out _anchorGood);
            t.localScale = Vector3.one * 0.8f;
            t.localRotation = Quaternion.identity;

            // Delegate-free click: opens the tabbed ToU window on top.
            PassiveButton passive = null;
            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null) continue;
                if (passive == null) { passive = pb; continue; }
                try { pb.OnClick.RemoveAllListeners(); } catch { }
                pb.enabled = false;
            }
            if (passive != null)
            {
                try { passive.OnClick.RemoveAllListeners(); } catch { }
                passive.gameObject.name = clone.name;
                ClickRouter.Register(clone.name, () =>
                {
                    NativeRolesPage.Toggle(menu);
                    if (_arrowText != null)
                        _arrowText.text = NativeRolesPage.IsActive ? "\u00AB" : "\u00BB";
                });
            }

            clone.SetActive(true);
            _arrow = clone;
        }

        /// <summary>
        /// Anchor the arrow to the window's top-right corner.
        ///
        /// Primary anchor: the tab bar. The rightmost tab button (Game) defines
        /// the window's top edge and is guaranteed to be on screen — the arrow
        /// sits just right of it. Because the arrow is parented to the window
        /// root (not a tab's panel), it stays put while switching Hat/Skin/Pet/
        /// Game tabs, which is what "top right of the customize menu" means.
        ///
        /// Fallback: the rows' own world bounds (rows are also guaranteed on
        /// screen), top-right corner of the row column. This deliberately
        /// avoids scanning sprites for a "window frame": the largest sprite
        /// under the window can be a fullscreen dimmer, which is what pinned
        /// the old tab button to a screen corner.
        /// </summary>
        private static Vector3 ComputeArrowPosition(SettingMenu menu, out bool anchored)
        {
            anchored = false;
            var anchor = menu.menu != null && menu.menu.transform != null
                ? menu.menu.transform
                : menu.transform;

            // Fallback (nothing built yet): top-right area of the menu.
            Vector3 pos = anchor.position + new Vector3(4.6f, 2.6f, 0f);

            // Primary: just right of the rightmost tab button. Pick by X
            // position, not array order — "rightmost on screen" is what top
            // right means, independent of how a build orders the Tabs array.
            if (_trackedCpm != null && _trackedCpm.Tabs != null && _trackedCpm.Tabs.Length > 0)
            {
                SpriteRenderer rightmost = null;
                float bestX = float.NegativeInfinity;
                for (int i = 0; i < _trackedCpm.Tabs.Length; i++)
                {
                    var tab = _trackedCpm.Tabs[i];
                    if (tab == null || tab.Button == null) continue;
                    var bp = tab.Button.transform.position;
                    if (bp.x > bestX) { bestX = bp.x; rightmost = tab.Button; }
                }
                if (rightmost != null)
                {
                    var bp = rightmost.transform.position;
                    float half = rightmost.sprite != null ? rightmost.sprite.bounds.extents.x : 0.5f;
                    // +0.8 gap keeps the arrow's own collider clear of the tab
                    // button's click zone (the ClickRouter only blocks the
                    // arrow's click, not the tab's native one).
                    pos = new Vector3(bp.x + half + 0.8f, bp.y, bp.z);
                    anchored = true;
                }
            }

            // Fallback: top-right of the row column (rows are on screen).
            if (!anchored && menu.AllItems != null && menu.AllItems.Count > 0)
            {
                // Seed bounds from the first non-null row (rows can be
                // destroyed/rebuilt by the game's own layout code).
                Vector3 min = default, max = default;
                bool seeded = false;
                for (int i = 0; i < menu.AllItems.Count; i++)
                {
                    var tr = menu.AllItems[i];
                    if (tr == null) continue;
                    var p = tr.position;
                    if (!seeded) { min = max = p; seeded = true; continue; }
                    min.x = Mathf.Min(min.x, p.x); max.x = Mathf.Max(max.x, p.x);
                    min.y = Mathf.Min(min.y, p.y); max.y = Mathf.Max(max.y, p.y);
                }
                if (seeded)
                {
                    // Just outside the row column, one row-step above the top
                    // row — inside the window, never on a screen corner.
                    pos = new Vector3(max.x + 1.15f, max.y + 0.45f, min.z);
                    anchored = true;
                }
            }
            return pos;
        }

        private static void Log(string message) =>
            BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogInfo("Native menu arrow: " + message);
    }

    /// <summary>
    /// Fires when the settings window builds its rows. Builds the arrow (or
    /// falls back to the old Game-tab rows if the window can't be located).
    /// </summary>
    [HarmonyPatch(typeof(SettingMenu), nameof(SettingMenu.OnEnable))]
    internal static class SettingMenu_OnEnable_MenuArrowPatch
    {
        private static void Postfix(SettingMenu __instance)
        {
            try
            {
            SettingsScroll.Reset();
            if (NativeMenuArrow.Ensure(__instance))
            {
                NativeRolesPage.OnMenuEnabled(__instance); // re-apply an open page over freshly built rows
                return;
            }
            NativeSettingsRows.Inject(__instance); // fallback: native rows in the Game tab
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Menu arrow: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Backup trigger: SettingMenu.OnEnable can fire during Instantiate (before
    /// CustomPlayerMenu.Start sets Instance), so this guarantees the arrow gets
    /// built once the window is fully alive.
    /// </summary>
    [HarmonyPatch(typeof(CustomPlayerMenu), nameof(CustomPlayerMenu.Start))]
    internal static class CustomPlayerMenu_Start_MenuArrowPatch
    {
        private static void Postfix(CustomPlayerMenu __instance)
        {
            try
            {
                if (RoleConfig.NativeMenuRows?.Value != true) return;
                if (__instance == null || __instance.gameObject == null) return;
                var sm = __instance.gameObject.GetComponentInChildren<SettingMenu>(true);
                if (sm != null) NativeMenuArrow.Ensure(sm);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Menu arrow start: " + e.Message);
            }
        }
    }
}

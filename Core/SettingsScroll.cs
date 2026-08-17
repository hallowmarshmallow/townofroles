using System;
using HarmonyLib;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Adds mouse-wheel scrolling to Classic Us' native game-config menu so the
    /// Town Of Us native rows — and the game's own off-screen role sections —
    /// are reachable. The 8.9 settings list has no scroll mechanism of its
    /// own: no ScrollRect, no wheel handling. This postfix translates the row
    /// children vertically on the wheel, clamped between the list's natural
    /// top (offset 0) and the point where the last row reaches the bottom of
    /// the visible area. Rows are rebuilt from scratch every time the menu
    /// opens, so Reset() on open keeps the scroll state consistent.
    ///
    /// Gated behind RoleConfig.NativeMenuRows (experimental opt-in).
    /// </summary>
    internal static class SettingsScroll
    {
        /// <summary>Visible list height in world units (~YStart down to screen bottom).</summary>
        private const float ViewportHeight = 9.0f;

        /// <summary>One wheel notch (~3) moves the list by roughly one row (0.45).</summary>
        private const float WheelFactor = 0.15f;

        private static float _offset;
        private static Transform _trackedFirstChild;
        private static System.Reflection.PropertyInfo _mouseScrollDelta;
        private static bool _inputResolved;
        private static bool _inputWarned;

        // UnityEngine.Input is not in the stale local GameLibs reference, while
        // the runtime-generated interop has it (the game reads the wheel in the
        // voting screen). Resolve it lazily via reflection so the scroll stays
        // drift-proof (same rule as the RpcBroadcastSystemAlert fix).
        private static System.Reflection.PropertyInfo MouseScrollDelta
        {
            get
            {
                if (_inputResolved) return _mouseScrollDelta;
                _inputResolved = true;
                _mouseScrollDelta = ResolveMouseScrollDelta();
                if (_mouseScrollDelta == null && !_inputWarned)
                {
                    _inputWarned = true;
                    BepInEx.Logging.Logger.CreateLogSource("TownOfUs")
                        .LogWarning("Settings scroll: UnityEngine.Input not found; wheel scrolling disabled.");
                }
                return _mouseScrollDelta;
            }
        }

        private static System.Reflection.PropertyInfo ResolveMouseScrollDelta()
        {
            foreach (var asmName in new[] { "UnityEngine.InputLegacyModule", "UnityEngine.CoreModule" })
            {
                var t = Type.GetType("UnityEngine.Input, " + asmName);
                if (t == null) continue;
                var p = t.GetProperty("mouseScrollDelta", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (p != null) return p;
            }
            // Fallback: some interop generators rename the module assembly.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == null || asm.IsDynamic) continue;
                try
                {
                    var t = asm.GetType("UnityEngine.Input");
                    if (t == null) continue;
                    var p = t.GetProperty("mouseScrollDelta", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (p != null) return p;
                }
                catch { }
            }
            return null;
        }

        /// <summary>Called when the menu (re)builds its rows, so scrolling starts from the top.</summary>
        public static void Reset() => _offset = 0f;

        public static void Tick(GameOptionsMenu menu)
        {
            if (RoleConfig.NativeMenuRows?.Value != true) return;
            if (menu == null || menu.transform == null) return;

            // Rows are rebuilt from scratch on every menu open and on internal
            // role rebuilds (GameOptionsMenu.Update re-runs SettingMenu.OnEnable
            // via _pendingRoleRebuild). Detect a new root child and reset the
            // accumulated offset so a stale value never applies to freshly
            // positioned rows.
            var firstChild = menu.transform.childCount > 0 ? menu.transform.GetChild(0) : null;
            if (firstChild != _trackedFirstChild)
            {
                _offset = 0f;
                _trackedFirstChild = firstChild;
            }

            float wheel = 0f;
            if (MouseScrollDelta == null) return;
            try
            {
                if (MouseScrollDelta.GetValue(null) is Vector2 v) wheel = v.y;
            }
            catch
            {
                return;
            }
            if (Mathf.Abs(wheel) < 0.01f) return;

            // Content height is offset-invariant, so it can be measured from
            // the current (already scrolled) positions.
            float first = float.MinValue, last = float.MaxValue;
            for (int i = 0; i < menu.transform.childCount; i++)
            {
                var child = menu.transform.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                float y = child.localPosition.y;
                if (y > first) first = y;
                if (y < last) last = y;
            }
            if (first == float.MinValue || last == float.MaxValue) return;

            float contentHeight = first - last;
            float maxOffset = Mathf.Max(0f, contentHeight - ViewportHeight);
            float newOffset = Mathf.Clamp(_offset - wheel * WheelFactor, 0f, maxOffset);
            float shift = newOffset - _offset;
            if (Mathf.Abs(shift) < 0.0001f) return;
            _offset = newOffset;

            for (int i = 0; i < menu.transform.childCount; i++)
            {
                var child = menu.transform.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                var p = child.localPosition;
                p.y += shift;
                child.localPosition = p;
            }
        }
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
    internal static class GameOptionsMenu_Update_SettingsScrollPatch
    {
        private static void Postfix(GameOptionsMenu __instance)
        {
            try
            {
                SettingsScroll.Tick(__instance);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Settings scroll: " + e.Message);
            }
        }
    }
}

using System;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    internal static class VisualEffects
    {
        // The "hallowmarsh" gradient: the creator's smooth blue/pink cycling
        // color, hardcoded here and applied to the local player's body renderers
        // via the game's own PlayerMaterial path (same one the Camouflager uses).
        private static readonly Color GradientBlue = new(0.30f, 0.62f, 1f, 1f);
        private static readonly Color GradientPink = new(1f, 0.45f, 0.62f, 1f);
        private const float GradientSpeed = 2.5f;

        private static bool _gradient;
        private static bool _rainbow;
        private static int _restoreColor = -1;
        private static PlayerColorSetter _rainbowSetter;

        public static bool GradientEnabled => _gradient;
        public static bool RainbowEnabled => _rainbow;

        public static void SetGradient(bool enabled)
        {
            if (enabled)
            {
                DisableNativeRainbow();
                _rainbow = false;
                var local = PlayerControl.LocalPlayer;
                if (local != null && local.Data != null)
                    RememberVanillaColor(local);
                _gradient = true;
            }
            else
            {
                _gradient = false;
                if (!_rainbow) RestoreVanillaColor();
            }
        }

        public static void SetRainbow(bool enabled)
        {
            if (enabled)
            {
                var local = PlayerControl.LocalPlayer;
                if (local == null || local.Data == null)
                    return;

                RememberVanillaColor(local);
                _gradient = false;
                _rainbow = true;
                _rainbowSetter = local.GetComponent<PlayerColorSetter>();
                if (_rainbowSetter != null)
                    _rainbowSetter.EnableRainbowMode();
            }
            else
            {
                _rainbow = false;
                DisableNativeRainbow();
                if (!_gradient) RestoreVanillaColor();
            }
        }

        public static void Tick()
        {
            if (!_gradient && !_rainbow) return;

            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;
            if (local.Data.IsDead)
            {
                Reset();
                return;
            }

            if (_rainbow)
            {
                // Native PlayerColorSetter.Update() animates the hue. Do not call
                // SetColor here, which would overwrite the native rainbow mode.
                if (_rainbowSetter == null)
                    _rainbowSetter = local.GetComponent<PlayerColorSetter>();
                if (_rainbowSetter != null)
                    _rainbowSetter.EnableRainbowMode();
                return;
            }

            // Smooth blue/pink hallowmarsh gradient, re-tinted every FixedUpdate.
            RememberVanillaColor(local);
            var t = (Mathf.Sin(Time.unscaledTime * GradientSpeed) + 1f) / 2f;
            var color = Color.Lerp(GradientBlue, GradientPink, t);
            TintLocalBody(local, color);
        }

        public static void Reset()
        {
            _gradient = false;
            _rainbow = false;
            DisableNativeRainbow();
            RestoreVanillaColor();
        }

        private static void TintLocalBody(PlayerControl local, Color color)
        {
            foreach (var renderer in local.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                try { PlayerMaterial.SetColors(color, renderer); } catch { }
            }
        }

        private static void RestoreLocalBody(PlayerControl local, int colorId)
        {
            foreach (var renderer in local.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                try { PlayerControl.SetPlayerMaterialColors(colorId, renderer); } catch { }
            }
        }

        private static void RememberVanillaColor(PlayerControl local)
        {
            if (_restoreColor < 0 && local?.Data != null)
                _restoreColor = local.Data.ColorId;
        }

        private static void DisableNativeRainbow()
        {
            if (_rainbowSetter != null)
                _rainbowSetter.DisableRainbowMode();
            _rainbowSetter = null;
        }

        private static void RestoreVanillaColor()
        {
            var local = PlayerControl.LocalPlayer;
            if (local != null && local.Data != null && _restoreColor >= 0)
            {
                try { local.SetColor(_restoreColor); } catch { }
                RestoreLocalBody(local, _restoreColor);
            }
            _restoreColor = -1;
        }
    }
}

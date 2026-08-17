using System;

namespace TownOfUs.ManuAPI.Core
{
    internal static class VisualEffects
    {
        // Gradient mode uses ordinary vanilla color IDs. Rainbow mode is handled
        // by Classic Us's native PlayerColorSetter component below.
        private static readonly int[] Palette = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };

        private static DateTime _nextStep = DateTime.MinValue;
        private static int _step;
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
                _gradient = true;
                _rainbow = false;
            }
            else
            {
                _gradient = false;
                if (!_rainbow) RestoreVanillaColor();
            }
            _nextStep = DateTime.MinValue;
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
            _nextStep = DateTime.MinValue;
        }

        public static void Tick()
        {
            if (!_gradient && !_rainbow) return;
            if (DateTime.UtcNow < _nextStep) return;
            _nextStep = DateTime.UtcNow.AddMilliseconds(850);

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

            RememberVanillaColor(local);
            local.SetColor(Palette[_step % Palette.Length]);
            _step++;
        }

        public static void Reset()
        {
            _gradient = false;
            _rainbow = false;
            DisableNativeRainbow();
            RestoreVanillaColor();
            _nextStep = DateTime.MinValue;
            _step = 0;
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
                local.SetColor(_restoreColor);
            _restoreColor = -1;
        }
    }
}

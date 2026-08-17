using System;
using System.Reflection;
using ClassicUs.ManuAPI;
using UnityEngine;

namespace TownOfUs.ManuAPI.Assets
{
    internal static class RoleArt
    {
        private static readonly Assembly Assembly = typeof(RoleArt).Assembly;
        private static readonly string ResourcePrefix = "TownOfUs.ManuAPI.Assets.OriginalTownOfUs.Resources.";
        private static Sprite _engineer;
        private static Sprite _medic;
        private static Sprite _seer;
        private static Sprite _janitor;
        private static Sprite _revive;
        private static Sprite _douse;
        private static Sprite _ignite;
        private static Sprite _cycle;
        private static Sprite _guess;
        private static Sprite _swapperSwitch;
        private static Sprite _morph;
        private static Sprite _camouflage;
        private static Sprite _swoop;
        private static Sprite _drag;
        private static Sprite _footprint;
        private static Sprite _rewind;
        private static Sprite _arrow;
        private static Sprite _shift;
        private static Sprite _shiftKill;
        private static Sprite _mine;
        private static Sprite _abstain;
        private static bool _preloadAttempted;
        private static string _originalButtonText;
        private static Sprite _originalButtonSprite;
        private static Vector3 _originalButtonScale;
        private static KillButtonManager _capturedButton;

        private static Sprite Load(string name)
        {
            try
            {
                return AssetUtils.LoadSpriteFromEmbeddedResource(
                    Assembly, ResourcePrefix + name, 260f, 512);
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogWarning(
                    "Could not load original Town Of Us asset " + name + ": " + ex.Message);
                return null;
            }
        }

        public static void Preload()
        {
            if (_preloadAttempted) return;
            _preloadAttempted = true;
            _engineer = Load("Engineer.png");
            _medic = Load("Medic.png");
            _seer = Load("Seer.png");
            _janitor = Load("Janitor.png");
            _revive = Load("Revive.png");
            _douse = Load("Douse.png");
            _ignite = Load("Ignite.png");
            _cycle = Load("Cycle.png");
            _guess = Load("Guess.png");
            _swapperSwitch = Load("SwapperSwitch.png");
            _morph = Load("Morph.png");
            _camouflage = Load("Camouflage.png");
            _swoop = Load("Swoop.png");
            _drag = Load("Drag.png");
            _footprint = Load("Footprint.png");
            _rewind = Load("Rewind.png");
            _arrow = Load("Arrow.png");
            _shift = Load("Shift.png");
            _shiftKill = Load("ShiftKill.png");
            _mine = Load("Mine.png");
            _abstain = Load("Abstain.png");
        }

        public static Sprite Engineer { get { Preload(); return _engineer; } }
        public static Sprite Medic { get { Preload(); return _medic; } }
        public static Sprite Seer { get { Preload(); return _seer; } }
        public static Sprite Janitor { get { Preload(); return _janitor; } }
        public static Sprite Revive { get { Preload(); return _revive; } }
        public static Sprite Douse { get { Preload(); return _douse; } }
        public static Sprite Ignite { get { Preload(); return _ignite; } }
        public static Sprite Cycle { get { Preload(); return _cycle; } }
        public static Sprite Guess { get { Preload(); return _guess; } }
        public static Sprite SwapperSwitch { get { Preload(); return _swapperSwitch; } }
        public static Sprite Morph { get { Preload(); return _morph; } }
        public static Sprite Camouflage { get { Preload(); return _camouflage; } }
        public static Sprite Swoop { get { Preload(); return _swoop; } }
        public static Sprite Drag { get { Preload(); return _drag; } }
        public static Sprite Footprint { get { Preload(); return _footprint; } }
        public static Sprite Rewind { get { Preload(); return _rewind; } }
        public static Sprite Arrow { get { Preload(); return _arrow; } }
        public static Sprite Shift { get { Preload(); return _shift; } }
        public static Sprite ShiftKill { get { Preload(); return _shiftKill; } }
        public static Sprite Mine { get { Preload(); return _mine; } }
        public static Sprite Abstain { get { Preload(); return _abstain; } }

        public static void Apply(KillButtonManager button, Sprite icon)
        {
            if (button == null || icon == null) return;
            if (_capturedButton != button)
            {
                _capturedButton = button;
                _originalButtonText = button.ButtonText?.text;
                _originalButtonSprite = FindRenderer(button)?.sprite;
                _originalButtonScale = FindRenderer(button)?.transform.localScale ?? Vector3.one;
            }
            var renderer = FindRenderer(button);
            if (renderer != null)
            {
                renderer.sprite = icon;
                renderer.transform.localScale = Vector3.one * 0.62f;
            }
        }

        public static void Restore(KillButtonManager button)
        {
            if (button == null || _capturedButton != button) return;
            if (button.ButtonText != null) button.ButtonText.text = _originalButtonText ?? string.Empty;
            var renderer = FindRenderer(button);
            if (renderer != null)
            {
                renderer.sprite = _originalButtonSprite;
                renderer.transform.localScale = _originalButtonScale == Vector3.zero ? Vector3.one : _originalButtonScale;
            }
            _capturedButton = null;
            _originalButtonText = null;
            _originalButtonSprite = null;
            _originalButtonScale = Vector3.one;
        }

        private static SpriteRenderer FindRenderer(KillButtonManager button)
        {
            if (button == null) return null;
            var root = button.GetComponent<SpriteRenderer>();
            if (root != null) return root;
            foreach (var child in button.GetComponentsInChildren<SpriteRenderer>(true))
                if (child != null) return child;
            return null;
        }
    }
}

using ClassicUs.ManuAPI;
using UnityEngine;
using TMPro;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;
using TownOfUs.ManuAPI.Roles.Engineer;
using TownOfUs.ManuAPI.Roles.Medic;
using TownOfUs.ManuAPI.Roles.Seer;
using TownOfUs.ManuAPI.Roles.Sheriff;
using TownOfUs.ManuAPI.Roles.Vigilante;
using TownOfUs.ManuAPI.Roles.Janitor;
using TownOfUs.ManuAPI.Roles.Altruist;
using TownOfUs.ManuAPI.Roles.Arsonist;
using TownOfUs.ManuAPI.Roles.Morphling;
using TownOfUs.ManuAPI.Roles.Camouflager;
using TownOfUs.ManuAPI.Roles.Swooper;
using TownOfUs.ManuAPI.Roles.Underdog;
using TownOfUs.ManuAPI.Roles.Undertaker;
using TownOfUs.ManuAPI.Roles.Investigator;
using TownOfUs.ManuAPI.Roles.TimeLord;
using TownOfUs.ManuAPI.Roles.Snitch;
using TownOfUs.ManuAPI.Roles.Phantom;
using TownOfUs.ManuAPI.Roles.Shifter;
using TownOfUs.ManuAPI.Roles.Glitch;
using TownOfUs.ManuAPI.Roles.Miner;

namespace TownOfUs.ManuAPI.Roles
{
    /// <summary>
    /// Ability buttons for every role with an active ability. ManuAPI clones the
    /// native button template and places the custom ability at the standard
    /// Kill-button slot. The native KillButtonManager is deliberately left
    /// untouched by this mod so Impostor kills and reports run through the pure
    /// vanilla pipeline (Harmony-patching PerformKill was linked to a native
    /// delegate crash on Linux at first real kill).
    ///
    /// Safety rules enforced here: buttons are only ticked while the local player
    /// is alive in-game, destroyed clones are detected and rebuilt cleanly, and
    /// icon normalization is cached instead of performed every frame.
    /// </summary>
    internal static class CustomRoleAbilities
    {
        private static readonly EngineerButton Engineer = new();
        private static readonly MedicButton Medic = new();
        private static readonly SeerButton Seer = new();
        private static readonly SheriffButton Sheriff = new();
        private static readonly VigilanteButton Vigilante = new();
        private static readonly JanitorButton Janitor = new();
        private static readonly AltruistButton Altruist = new();
        private static readonly ArsonistButton Arsonist = new();
        private static readonly MorphlingButton Morphling = new();
        private static readonly CamouflagerButton Camouflager = new();
        private static readonly SwooperButton Swooper = new();
        private static readonly UndertakerButton Undertaker = new();
        private static readonly TimeLordButton TimeLord = new();
        private static readonly ShifterButton Shifter = new();
        private static readonly GlitchMimicButton GlitchMimic = new();
        private static readonly GlitchHackButton GlitchHack = new();
        private static readonly GlitchKillButton GlitchKill = new();
        private static readonly MinerButton Miner = new();

        private static readonly RoleAbilityButton[] All = { Engineer, Medic, Seer, Sheriff, Vigilante, Janitor, Altruist, Arsonist, Morphling, Camouflager, Swooper, Undertaker, TimeLord, Shifter, GlitchMimic, GlitchHack, GlitchKill, Miner };

        internal static void Tick(HudManager hud)
        {
            if (hud == null || hud.KillButton == null) return;

            var local = PlayerControl.LocalPlayer;
            // Only create/refresh buttons while actually playing as an alive role.
            // Outside a live round (lobby, Freeplay computer, meeting transitions)
            // the HUD button template is not in a safe state to clone every frame.
            if (local == null || local.Data == null || local.Data.IsDead) return;

            Engineer.Tick(hud);
            Medic.Tick(hud);
            Seer.Tick(hud);
            Sheriff.Tick(hud);
            Vigilante.Tick(hud);
            Janitor.Tick(hud);
            Altruist.Tick(hud);
            Arsonist.Tick(hud);
            Morphling.Tick(hud);
            Camouflager.Tick(hud);
            Swooper.Tick(hud);
            Undertaker.Tick(hud);
            TimeLord.Tick(hud);
            Shifter.Tick(hud);
            GlitchMimic.Tick(hud);
            GlitchHack.Tick(hud);
            GlitchKill.Tick(hud);
            Miner.Tick(hud);
        }

        internal static void ResetAll()
        {
            foreach (var ability in All)
            {
                try { ability.Reset(); }
                catch { }
            }
        }

        public static void OnGameStarted(GameStartedEventArgs _) => ResetAll();
        public static void OnGameEnded(GameEndedEventArgs _) => ResetAll();

        /// <summary>
        /// Detects HUD clones the game destroyed so they are rebuilt instead of
        /// reused. Called from the FixedUpdate patch on a throttle to stay cheap.
        /// </summary>
        internal static void Maintain()
        {
            foreach (var ability in All)
            {
                try
                {
                    ability.Maintain();
                }
                catch { }
            }
        }

        private sealed class EngineerButton : RoleAbilityButton
        {
            protected override string Name => "Fix Sab";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Engineer ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Engineer?.Value == true &&
                       EngineerSystem.IsEngineer(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => EngineerAbility.CanFixSab(PlayerControl.LocalPlayer);
            protected override void OnActivate() => EngineerAbility.TryFixSab(PlayerControl.LocalPlayer);
        }

        private sealed class MedicButton : RoleAbilityButton
        {
            protected override string Name => "Protect";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Medic ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Medic?.Value == true &&
                       MedicSystem.IsMedic(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => MedicSystem.CanProtectNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => MedicSystem.TryProtect(PlayerControl.LocalPlayer);
        }

        private sealed class SeerButton : RoleAbilityButton
        {
            protected override string Name => "Investigate";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Seer ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Seer?.Value == true &&
                       SeerSystem.IsSeer(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => SeerSystem.CanInvestigateNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => SeerSystem.TryInvestigate(PlayerControl.LocalPlayer);
        }

        private sealed class SheriffButton : RoleAbilityButton
        {
            protected override string Name => "Shoot";
            protected override float Cooldown => Options.KillCooldown;
            // No dedicated original Town-Of-Us Sheriff icon is bundled; keep the
            // native button artwork so the button reads as the shooting ability.
            protected override Sprite CreateIcon(Sprite original) => original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Sheriff?.Value == true &&
                       SheriffSystem.IsSheriff(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() =>
                !SheriffAbilityHolder.IsCoolingDown && SheriffSystem.HasTarget(PlayerControl.LocalPlayer);
            protected override void OnActivate()
            {
                var local = PlayerControl.LocalPlayer;
                if (!SheriffAbilityHolder.TryStartCooldown()) return;
                SheriffSystem.TryShoot(local);
            }
        }

        private sealed class VigilanteButton : RoleAbilityButton
        {
            protected override string Name => "Shoot";
            protected override Sprite CreateIcon(Sprite original) => original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Vigilante?.Value == true &&
                       VigilanteSystem.IsVigilante(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => VigilanteSystem.CanShootNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => VigilanteSystem.TryShoot(PlayerControl.LocalPlayer);
        }

        private sealed class JanitorButton : RoleAbilityButton
        {
            protected override string Name => "Clean";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Janitor ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Janitor?.Value == true &&
                       JanitorSystem.IsJanitor(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => JanitorSystem.CanCleanNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => JanitorSystem.TryClean(PlayerControl.LocalPlayer);
        }

        private sealed class AltruistButton : RoleAbilityButton
        {
            protected override string Name => "Revive";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Revive ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Altruist?.Value == true &&
                       AltruistSystem.IsAltruist(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => AltruistSystem.CanReviveNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => AltruistSystem.TryRevive(PlayerControl.LocalPlayer);
        }

        private sealed class ArsonistButton : RoleAbilityButton
        {
            protected override string Name => "Douse";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Douse ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Arsonist?.Value == true &&
                       ArsonistSystem.IsArsonist(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => ArsonistSystem.CanDouseNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => ArsonistSystem.TryDouse(PlayerControl.LocalPlayer);
        }

        private sealed class MorphlingButton : RoleAbilityButton
        {
            protected override string Name => "Morph";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Morph ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Morphling?.Value == true &&
                       MorphlingSystem.IsMorphling(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => MorphlingSystem.CanMorphNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => MorphlingSystem.TryMorph(PlayerControl.LocalPlayer);
        }

        private sealed class CamouflagerButton : RoleAbilityButton
        {
            protected override string Name => "Camouflage";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Camouflage ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Camouflager?.Value == true &&
                       CamouflagerSystem.IsCamouflager(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => CamouflagerSystem.CanCamouflageNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => CamouflagerSystem.TryCamouflage(PlayerControl.LocalPlayer);
        }

        private sealed class SwooperButton : RoleAbilityButton
        {
            protected override string Name => "Swoop";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Swoop ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Swooper?.Value == true &&
                       SwooperSystem.IsSwooper(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => SwooperSystem.CanSwoopNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => SwooperSystem.TrySwoop(PlayerControl.LocalPlayer);
        }

        private sealed class TimeLordButton : RoleAbilityButton
        {
            protected override string Name => "Rewind";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Rewind ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.TimeLord?.Value == true &&
                       TimeLordSystem.IsTimeLord(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => TimeLordSystem.CanRewindNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => TimeLordSystem.TryRewind(PlayerControl.LocalPlayer);
        }

        private sealed class UndertakerButton : RoleAbilityButton
        {
            // Single toggle button: Drag picks up the nearest body, pressing
            // again drops it (UndertakerSystem.CanDragNow returns true while
            // dragging, and TryDrag drops when already dragging).
            protected override string Name => "Drag";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Drag ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Undertaker?.Value == true &&
                       UndertakerSystem.IsUndertaker(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => UndertakerSystem.CanDragNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => UndertakerSystem.TryDrag(PlayerControl.LocalPlayer);
        }

        private sealed class ShifterButton : RoleAbilityButton
        {
            protected override string Name => "Shift";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Shift ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Shifter?.Value == true &&
                       ShifterSystem.IsShifter(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => ShifterSystem.CanShiftNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => ShifterSystem.TryShift(PlayerControl.LocalPlayer);
        }

        private sealed class GlitchMimicButton : RoleAbilityButton
        {
            protected override string Name => "Mimic";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Shift ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Glitch?.Value == true &&
                       GlitchSystem.IsGlitch(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => GlitchSystem.CanMimicNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => GlitchSystem.TryMimic(PlayerControl.LocalPlayer);
        }

        private sealed class GlitchHackButton : RoleAbilityButton
        {
            protected override string Name => "Hack";
            protected override Vector3 SlotOffset => new(0f, 0.9f, 0f);
            protected override Sprite CreateIcon(Sprite original) => original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Glitch?.Value == true &&
                       GlitchSystem.IsGlitch(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => GlitchSystem.CanHackNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => GlitchSystem.TryHack(PlayerControl.LocalPlayer);
        }

        private sealed class GlitchKillButton : RoleAbilityButton
        {
            protected override string Name => "Kill";
            protected override Vector3 SlotOffset => new(0f, 1.8f, 0f);
            protected override Sprite CreateIcon(Sprite original) => original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Glitch?.Value == true &&
                       GlitchSystem.IsGlitch(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => GlitchSystem.CanKillNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => GlitchSystem.TryKill(PlayerControl.LocalPlayer);
        }

        private sealed class MinerButton : RoleAbilityButton
        {
            protected override string Name => "Mine";
            protected override Sprite CreateIcon(Sprite original) => RoleArt.Mine ?? original;
            protected override bool IsVisible()
            {
                var local = PlayerControl.LocalPlayer;
                return local != null && local.Data != null && RoleConfig.Miner?.Value == true &&
                       MinerSystem.IsMiner(local) && !local.Data.IsDead;
            }
            protected override bool CanActivate() => MinerSystem.CanMineNow(PlayerControl.LocalPlayer);
            protected override void OnActivate() => MinerSystem.TryMine(PlayerControl.LocalPlayer);
        }
    }

    /// <summary>
    /// A safe replacement for ManuAPI's CustomAbility UI implementation.
    /// ManuAPI 1.7 creates its buttons with UnityEvent.AddListener, which asks
    /// IL2CPP to marshal a managed delegate. Classic Us rejects that marshal at
    /// runtime. These buttons use the game's normal PassiveButton click path
    /// and ClickRouter instead, so no managed Unity delegate is installed.
    /// </summary>
    internal abstract class RoleAbilityButton
    {
        private GameObject _button;
        private SpriteRenderer _renderer;
        private TextMeshPro _abilityText;
        private TextMeshPro _cooldownText;
        private string _routerId;
        private float _cooldownRemaining;
        private bool _loggedMissingDeps;
        private bool _loggedCreated;
        private bool _loggedVisible;

        protected abstract string Name { get; }
        protected virtual float Cooldown => 0f;
        // Vertical offset (world units) from the prefab's own ability-button slot,
        // so multi-button roles stack up into distinct slots instead of overlapping.
        // Zero keeps a single-ability button exactly on the game's native slot.
        protected virtual Vector3 SlotOffset => Vector3.zero;
        protected abstract Sprite CreateIcon(Sprite original);
        protected abstract bool IsVisible();
        protected virtual bool CanActivate() => true;
        protected abstract void OnActivate();

        internal void Tick(HudManager hud)
        {
            EnsureCreated(hud);
            if (_button == null || !_button) return;

            var visible = IsVisible();
            if (_button.activeSelf != visible)
            {
                _button.SetActive(visible);
                if (visible && !_loggedVisible)
                {
                    _loggedVisible = true;
                    LogVisualState("shown");
                }
            }
            if (!visible) return;

            if (_cooldownRemaining > 0f)
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.fixedDeltaTime);

            var ready = _cooldownRemaining <= 0f && CanActivate();
            if (_renderer != null)
                _renderer.color = ready ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.55f);
            if (_cooldownText != null)
                _cooldownText.text = _cooldownRemaining > 0f ? Mathf.Ceil(_cooldownRemaining).ToString("0") : string.Empty;
            if (_abilityText != null) _abilityText.text = Name;
        }

        internal void Maintain()
        {
            if (_button == null || !_button)
            {
                Reset();
                return;
            }
        }

        internal void Reset()
        {
            if (_routerId != null) ClickRouter.Unregister(_routerId);
            _routerId = null;
            if (_button != null) UnityEngine.Object.Destroy(_button);
            _button = null;
            _renderer = null;
            _abilityText = null;
            _cooldownText = null;
            _cooldownRemaining = 0f;
        }

        private void EnsureCreated(HudManager hud)
        {
            if (_button != null || hud == null || hud.KillButton == null) return;

            // This is the exact container and prefab used by Classic Us roles
            // (VanillaButtonManager.Create).  Cloning KillButton into the HUD root
            // copied KillButtonManager state and bypassed the game layout, which is
            // why the old buttons were unstable and appeared at arbitrary places.
            var parent = hud.transform.Find("Buttons/BottomRight");
            var cached = DestroyableSingleton<CachedMaterials>.Instance;
            if (parent == null || cached == null || cached.abilityButton == null)
            {
                if (!_loggedMissingDeps)
                {
                    _loggedMissingDeps = true;
                    var reason = parent == null ? "HUD path 'Buttons/BottomRight' not found"
                        : cached == null ? "CachedMaterials singleton not ready"
                        : "CachedMaterials.abilityButton prefab is null";
                    BepInEx.Logging.Logger.CreateLogSource("TownOfUs")
                        .LogWarning("Ability button '" + Name + "' not created: " + reason);
                }
                return;
            }
            var nativeButton = UnityEngine.Object.Instantiate(cached.abilityButton, parent);
            if (nativeButton == null) return;
            var clone = nativeButton.gameObject;
            clone.name = "ToU_Ability_" + GetType().Name;

            // Position via the prefab's own AspectPosition anchor — the same slot
            // the game's native role buttons land in. Extra buttons shift the
            // anchor's DistanceFromEdge up one slot so multi-button roles (Glitch)
            // stack in distinct slots instead of piling onto one slot near the
            // report button. The anchor stays alive so buttons also re-position on
            // resolution changes.
            //
            // (Previously we copied KillButton's world position and Destroy()'d the
            // anchor; because Destroy is deferred, the still-alive anchor's OnEnable
            // re-anchored every button back onto the same baked slot when SetActive
            // fired, which is why multi-button roles all stacked on top of each other.)
            var aspect = clone.GetComponent<AspectPosition>();
            if (aspect != null)
            {
                aspect.updateAlways = false;
                aspect.DistanceFromEdge += SlotOffset;
                aspect.AdjustPosition();
            }
            else
            {
                // No anchor on the prefab: fall back to the Kill button's position.
                clone.transform.position = hud.KillButton.transform.position + SlotOffset;
            }

            // Preserve the native prefab's renderers/text/colliders.  The click
            // router intercepts before its prefab listener is invoked, so no
            // managed UnityAction is marshalled into IL2CPP and no native component
            // needs to be destroyed.
            _renderer = nativeButton.spriteRender;
            _abilityText = nativeButton.AbilityText;
            _cooldownText = nativeButton.CooldownText;
            if (_renderer == null) _renderer = clone.GetComponentInChildren<SpriteRenderer>(true);
            if (_renderer != null)
            {
                var originalSprite = _renderer.sprite;
                var icon = CreateIcon(originalSprite);
                if (icon != null) _renderer.sprite = icon;
                _renderer.enabled = true;
                _renderer.gameObject.SetActive(true);
                // Size the custom art to the native button artwork so a role icon
                // renders the same on-screen size as the vanilla Kill/Use buttons
                // instead of appearing oversized.
                _renderer.transform.localScale = Vector3.one;
                if (icon != null && icon != originalSprite && originalSprite != null)
                {
                    var nativeWidth = originalSprite.bounds.size.x;
                    var iconWidth = icon.bounds.size.x;
                    if (nativeWidth > 0.0001f && iconWidth > 0.0001f)
                        _renderer.transform.localScale = Vector3.one * (nativeWidth / iconWidth);
                }
            }

            var passive = clone.GetComponentInChildren<PassiveButton>(true);
            if (passive == null)
            {
                UnityEngine.Object.Destroy(clone);
                _renderer = null;
                _abilityText = null;
                _cooldownText = null;
                return;
            }

            _routerId = clone.name;
            passive.gameObject.name = _routerId;
            ClickRouter.Register(_routerId, HandleClick);
            _button = clone;
            if (!_loggedCreated)
            {
                _loggedCreated = true;
                LogVisualState("created");
            }
        }

        private void LogVisualState(string when)
        {
            var sprite = _renderer != null && _renderer.sprite != null ? _renderer.sprite.name : "<null>";
            var pos = _button != null ? _button.transform.position.ToString() : "<none>";
            var localPos = _button != null ? _button.transform.localPosition.ToString() : "<none>";
            var rootActive = _button != null && _button.activeSelf;
            var hierActive = _button != null && _button.activeInHierarchy;
            var rendEnabled = _renderer != null && _renderer.enabled;
            var killPos = DestroyableSingleton<HudManager>.InstanceExists
                ? DestroyableSingleton<HudManager>.Instance.KillButton?.transform.position.ToString()
                : "<none>";
            var aspectInfo = "<none>";
            if (_button != null)
            {
                var asp = _button.GetComponent<AspectPosition>();
                if (asp != null)
                    aspectInfo = "align=" + asp.Alignment + " dfe=" + asp.DistanceFromEdge +
                                 " cam=" + (asp.parentCam != null ? asp.parentCam.name : "<null>");
            }
            BepInEx.Logging.Logger.CreateLogSource("TownOfUs")
                .LogInfo("Ability button '" + Name + "' [" + when + "] pos=" + pos +
                         " local=" + localPos + " killPos=" + killPos +
                         " " + aspectInfo +
                         " activeSelf=" + rootActive + " activeInHierarchy=" + hierActive +
                         " renderer=" + (_renderer != null) + " enabled=" + rendEnabled +
                         " sprite=" + sprite);
        }

        private void HandleClick()
        {
            if (_cooldownRemaining > 0f || !CanActivate()) return;
            OnActivate();
            if (Cooldown > 0f) _cooldownRemaining = Cooldown;
        }
    }

    /// <summary>
    /// Per-frame upkeep for the batch-3 Impostor roles: Camouflager grey-out /
    /// restore, Swooper renderer toggling, Underdog cooldown clamping (host), and
    /// Undertaker body-follow. One throttle-able postfix keeps the per-frame cost
    /// flat.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_Batch3Patch
    {
        private static int _tick;

        private static void Postfix()
        {
            try
            {
                _tick++;
                if ((_tick & 3) != 0) return; // ~15/s is plenty for all four systems
                CamouflagerSystem.Tick();
                SwooperSystem.Tick();
                UnderdogSystem.Tick();
                UndertakerSystem.Tick();
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Batch-3 role tick: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Per-frame upkeep for the batch-4 roles: Investigator footprints, Time
    /// Lord position recording (host), Snitch arrows, and the Phantom win/death
    /// checks. Throttled with the same pacing as the batch-3 patch.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_Batch4Patch
    {
        private static int _tick;

        private static void Postfix()
        {
            try
            {
                _tick++;
                if ((_tick & 3) != 0) return;
                InvestigatorSystem.Tick();
                TimeLordSystem.Tick();
                SnitchSystem.Tick();
                PhantomSystem.Tick();
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Batch-4 role tick: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Per-frame upkeep for the batch-5 roles: Glitch mimic expiry + elimination
    /// win and pool assignment (host). Shifter's swap is event-driven; Miner
    /// cleans up on game end.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_Batch5Patch
    {
        private static int _tick;

        private static void Postfix()
        {
            try
            {
                _tick++;
                if ((_tick & 3) != 0) return;
                GlitchSystem.Tick();
                ShifterSystem.Tick();
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Batch-5 role tick: " + e.Message);
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_CustomRoleAbilitiesPatch
    {
        private static bool _maintain;

        private static void Postfix(HudManager __instance)
        {
            try
            {
                CustomRoleAbilities.Tick(__instance);
                // Normalize/maintain at most every other FixedUpdate.
                _maintain = !_maintain;
                if (_maintain) CustomRoleAbilities.Maintain();
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Custom role ability buttons: " + e.Message);
            }
        }
    }
}

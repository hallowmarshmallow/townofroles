using System;
using System.Collections.Generic;
using System.Text;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Modifiers
{
    /// <summary>
    /// Player modifiers, ported from the original Town-Of-Us mod:
    ///
    ///  - Torch:      vision unaffected by lights sabotage (Crewmate).
    ///  - Diseased:   killing them triples the killer's kill cooldown (Crewmate).
    ///  - Flash:      moves at 2x speed.
    ///  - Tiebreaker: their vote decides tied meetings.
    ///  - Drunk:      movement controls are inverted.
    ///  - Giant:      bigger body, slower walk.
    ///  - Button Barry: can call an emergency meeting from anywhere.
    ///
    /// The host rolls each enabled modifier's probability per eligible player at
    /// round start and broadcasts the full assignment table so every client
    /// agrees. Most effects are applied host-side or on the local player only;
    /// Giant's scale is applied on every client so everyone sees the size.
    /// </summary>
    internal static class ModifierSystem
    {
        public const string Torch = "Torch";
        public const string Diseased = "Diseased";
        public const string Flash = "Flash";
        public const string Tiebreaker = "Tiebreaker";
        public const string Drunk = "Drunk";
        public const string Giant = "Giant";
        public const string ButtonBarry = "Button Barry";

        public static readonly string[] All = { Torch, Diseased, Flash, Tiebreaker, Drunk, Giant, ButtonBarry };

        private const string AssignRpc = "townofus.ModifierAssign";
        private const int MaxRetries = 300;
        private const float GiantScale = 1.4f;

        private static readonly Dictionary<byte, HashSet<string>> Assigned = new(); // playerId -> modifiers
        private static bool _assigned;
        private static int _attempts;
        private static readonly Dictionary<byte, int> DiseasedPenalties = new(); // killerId -> marker
        private static bool _lightCaptured;
        private static float _lightBaseScale = -1f;

        // Skipped while the ship/spawn transition is settling (set by GameStarted):
        // scaling freshly-spawned player bodies on the spawn frame can fault the CLR.
        private static float _settleUntil = float.MinValue;

        public static bool Has(byte playerId, string modifier) =>
            Assigned.TryGetValue(playerId, out var set) && set.Contains(modifier);

        public static bool Has(PlayerControl player, string modifier) =>
            player != null && Has(player.PlayerId, modifier);

        /// <summary>True when at least one modifier is enabled (cheap per-frame gate).</summary>
        public static bool AnyEnabled =>
            (RoleConfig.ModifierTorch?.Value == true) ||
            (RoleConfig.ModifierDiseased?.Value == true) ||
            (RoleConfig.ModifierFlash?.Value == true) ||
            (RoleConfig.ModifierTiebreaker?.Value == true) ||
            (RoleConfig.ModifierDrunk?.Value == true) ||
            (RoleConfig.ModifierGiant?.Value == true) ||
            (RoleConfig.ModifierButtonBarry?.Value == true);

        public static string NamesFor(byte playerId)
        {
            if (!Assigned.TryGetValue(playerId, out var set) || set.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < All.Length; i++)
            {
                if (!set.Contains(All[i])) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(All[i]);
            }
            return sb.ToString();
        }

        // ── Tick (host: assignment + diseased penalty; local: torch/flash) ───
        public static void Tick()
        {
            var local = PlayerControl.LocalPlayer;
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
            {
                if (!_assigned && _attempts < MaxRetries)
                {
                    _attempts++;
                    TryAssign();
                }
                ApplyDiseasedPenalties();
            }

            if (local == null || local.Data == null || local.Data.IsDead) return;
            ApplyTorchLight(local);
            ApplySpeed(local);
        }

        /// <summary>Called on every client: keeps Giant players visually big.</summary>
        public static void ApplyGiantScales()
        {
            if (Time.unscaledTime < _settleUntil) return; // spawn transition: let players finish spawning first
            if (ExileController.Instance != null || MeetingHud.Instance != null) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead) continue;
                if (!Has(player.PlayerId, Giant)) continue;
                try
                {
                    // A destroyed / not-yet-spawned body has a null Transform in
                    // IL2CPP; touching its localScale faults the CLR (the native
                    // PAL_SEHException). Also only write when the scale actually
                    // changed, so a healthy body is mutated once instead of every
                    // frame while it is mid-transition.
                    var body = player.transform;
                    if (body == null) continue;
                    if (Mathf.Abs(body.localScale.x - GiantScale) < 0.001f) continue;
                    body.localScale = Vector3.one * GiantScale;
                }
                catch { }
            }
        }

        // ── Assignment ───────────────────────────────────────────────────────
        private static void TryAssign()
        {
            if (PlayerControl.AllPlayerControls == null || PlayerControl.AllPlayerControls.Count == 0) return;

            // Wait for roles to be assigned (players have a myRole) before rolling.
            bool ready = false;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player != null && player.Data != null && player.Data.myRole != null) { ready = true; break; }
            }
            if (!ready) return;

            Assigned.Clear();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected || player.Data.IsDead) continue;
                var team = player.Data.myRole?.RoleTeamType;
                for (int i = 0; i < All.Length; i++)
                {
                    var modifier = All[i];
                    if (!IsEnabled(modifier)) continue;
                    // Torch / Diseased ride on Crewmates (the original mod's team split).
                    if ((modifier == Torch || modifier == Diseased) && team != RoleTeamTypes.Crewmate) continue;
                    if (UnityEngine.Random.Range(0f, 100f) >= Probability(modifier)) continue;
                    if (!Assigned.TryGetValue(player.PlayerId, out var set))
                    {
                        set = new HashSet<string>();
                        Assigned[player.PlayerId] = set;
                    }
                    set.Add(modifier);
                }
            }

            _assigned = true;
            var payload = BuildPayload();
            if (payload == null) return;
            try { TownOfUsRpcMux.Send(AssignRpc, payload); } catch (Exception e) { Log("broadcast: " + e.Message); }
            ParsePayload(payload); // host parses its own table for a single code path
        }

        private static bool IsEnabled(string modifier)
        {
            switch (modifier)
            {
                case Torch: return RoleConfig.ModifierTorch?.Value == true;
                case Diseased: return RoleConfig.ModifierDiseased?.Value == true;
                case Flash: return RoleConfig.ModifierFlash?.Value == true;
                case Tiebreaker: return RoleConfig.ModifierTiebreaker?.Value == true;
                case Drunk: return RoleConfig.ModifierDrunk?.Value == true;
                case Giant: return RoleConfig.ModifierGiant?.Value == true;
                default: return RoleConfig.ModifierButtonBarry?.Value == true;
            }
        }

        private static float Probability(string modifier)
        {
            switch (modifier)
            {
                case Torch: return RoleConfig.Chance(RoleConfig.ModifierTorchProbability);
                case Diseased: return RoleConfig.Chance(RoleConfig.ModifierDiseasedProbability);
                case Flash: return RoleConfig.Chance(RoleConfig.ModifierFlashProbability);
                case Tiebreaker: return RoleConfig.Chance(RoleConfig.ModifierTiebreakerProbability);
                case Drunk: return RoleConfig.Chance(RoleConfig.ModifierDrunkProbability);
                case Giant: return RoleConfig.Chance(RoleConfig.ModifierGiantProbability);
                default: return RoleConfig.Chance(RoleConfig.ModifierButtonBarryProbability);
            }
        }

        private static string BuildPayload()
        {
            if (Assigned.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var pair in Assigned)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(pair.Key).Append('=');
                var first = true;
                foreach (var name in All)
                {
                    if (!pair.Value.Contains(name)) continue;
                    if (!first) sb.Append(',');
                    sb.Append(name);
                    first = false;
                }
            }
            return sb.ToString();
        }

        private static void ParsePayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            Assigned.Clear();
            foreach (var group in payload.Split(';'))
            {
                var eq = group.IndexOf('=');
                if (eq <= 0) continue;
                if (!byte.TryParse(group.Substring(0, eq), out var playerId)) continue;
                var set = new HashSet<string>();
                foreach (var name in group.Substring(eq + 1).Split(','))
                    if (name.Length > 0) set.Add(name);
                if (set.Count > 0) Assigned[playerId] = set;
            }
        }

        [ReactorRpc(AssignRpc)]
        private static void OnAssign(byte senderId, string payload)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            ParsePayload(payload);
        }

        // ── Torch ────────────────────────────────────────────────────────────
        private static void ApplyTorchLight(PlayerControl local)
        {
            if (local.myLight == null) return;
            try
            {
                var scale = local.myLight.transform.localScale;
                if (!_lightCaptured)
                {
                    _lightBaseScale = scale.x;
                    _lightCaptured = true;
                }
                // When lights are sabotaged the game shrinks the light; a Torch
                // keeps the full radius it had while lights were normal.
                if (_lightBaseScale > 0f && Has(local.PlayerId, Torch) && scale.x < _lightBaseScale - 0.05f)
                    local.myLight.transform.localScale = new Vector3(_lightBaseScale, _lightBaseScale, scale.z);
            }
            catch { }
        }

        // ── Flash / Giant speed ──────────────────────────────────────────────
        private static void ApplySpeed(PlayerControl local)
        {
            if (local.MyPhysics == null) return;
            float multiplier = 1f;
            if (Has(local.PlayerId, Flash)) multiplier *= 2f;
            if (Has(local.PlayerId, Giant)) multiplier *= 0.75f;
            if (multiplier != 1f) local.MyPhysics.Speed = 4.5f * multiplier;
        }

        // ── Diseased (host) ──────────────────────────────────────────────────
        public static void OnBeforeMurder(MurderEventArgs args)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || args?.Target == null || args.Killer == null) return;
            if (!Has(args.Target.PlayerId, Diseased)) return;
            DiseasedPenalties[args.Killer.PlayerId] = 0;
        }

        private static void ApplyDiseasedPenalties()
        {
            if (DiseasedPenalties.Count == 0) return;
            foreach (var killerId in new List<byte>(DiseasedPenalties.Keys))
            {
                var killer = PlayerUtils.FindById(killerId);
                if (killer == null || killer.Data == null || killer.Data.IsDead)
                {
                    DiseasedPenalties.Remove(killerId);
                    continue;
                }
                var baseCooldown = PlayerControl.GameOptions != null ? PlayerControl.GameOptions.KillCooldown : 10f;
                var timer = killer.killTimer;
                // The game applies the normal cooldown right after the kill;
                // once we see it, triple it.
                if (timer > 0.05f && timer <= baseCooldown + 0.5f)
                {
                    try { killer.RpcSetKillTimer(baseCooldown * 3f); } catch (Exception e) { Log("diseased: " + e.Message); }
                    DiseasedPenalties.Remove(killerId);
                }
            }
        }

        private static PlayerControl PlayerUtils.FindById(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────
        public static void Reset()
        {
            Assigned.Clear();
            _assigned = false;
            _attempts = 0;
            DiseasedPenalties.Clear();
            _lightCaptured = false;
            _lightBaseScale = -1f;
        }

        public static void OnGameStarted(GameStartedEventArgs _)
        {
            Reset();
            _settleUntil = Time.unscaledTime + 1.5f;
        }
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();

        private static void Log(string message) =>
            BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("ModifierSystem " + message);
    }

    // ── Tiebreaker: their vote decides tied meetings (host tally) ────────────
    // NOTE: this postfix must stay installed AFTER the Mayor vote-bank postfix on
    // the same method, so it sees the final tally (Mayor's extra votes included).
    // The plugin installs Mayor first (Load order), which Harmony preserves.
    [HarmonyPatch(typeof(MeetingHud), "CalculateVotes")]
    internal static class MeetingHud_CalculateVotes_TiebreakerPatch
    {
        private static void Postfix(MeetingHud __instance, Il2CppStructArray<byte> __result)
        {
            try
            {
                var client = AmongUsClient.Instance;
                if (client == null || !client.AmHost) return;
                // playerStates is private in the 2026.8.9 interop.
                var states = GameReflection.GetPlayerStates(__instance);
                if (__instance == null || states == null || __result == null) return;

                var best = -1;
                var bestCount = -1;
                for (int i = 0; i < states.Length; i++)
                {
                    if (__result[i] > bestCount) { bestCount = __result[i]; best = i; }
                }
                if (bestCount <= 0) return;

                var tied = 0;
                for (int i = 0; i < states.Length; i++)
                    if (__result[i] == bestCount) tied++;
                if (tied < 2) return; // no tie

                // The Tiebreaker's vote breaks the tie (only if they voted a player).
                for (int i = 0; i < states.Length; i++)
                {
                    var area = states[i];
                    if (area == null || !area.DidVote) continue;
                    if (area.VotedFor == 253 || area.VotedFor == 254) continue;
                    if (area.VotedFor >= __result.Length) continue;
                    var voter = PlayerUtils.FindById(area.TargetPlayerId);
                    if (voter == null || !ModifierSystem.Has(voter.PlayerId, ModifierSystem.Tiebreaker)) continue;
                    __result[area.VotedFor] = (byte)Mathf.Min(255, __result[area.VotedFor] + 1);
                    return;
                }
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Tiebreaker vote: " + e.Message);
            }
        }

        private static PlayerControl PlayerUtils.FindById(byte id)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == id) return player;
            return null;
        }
    }

    // ── Drunk: inverted movement controls (local player only) ────────────────
    [HarmonyPatch(typeof(PlayerPhysics), "FixedUpdate")]
    internal static class PlayerPhysics_FixedUpdate_DrunkPatch
    {
        private static void Postfix(PlayerPhysics __instance)
        {
            try
            {
                var local = PlayerControl.LocalPlayer;
                if (local == null || local.MyPhysics == null || __instance != local.MyPhysics) return;
                if (local.Data == null || local.Data.IsDead) return;
                if (MeetingHud.Instance != null || ExileController.Instance != null) return;
                if (!ModifierSystem.Has(local.PlayerId, ModifierSystem.Drunk)) return;
                var body = __instance.GetComponent<Rigidbody2D>();
                if (body != null && body.velocity.sqrMagnitude > 0.01f) body.velocity = -body.velocity;
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Drunk controls: " + e.Message);
            }
        }
    }

    // ── Per-frame modifier upkeep (tick + giant scale for everyone) ──────────
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_ModifierPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance != PlayerControl.LocalPlayer) return;
            if (!ModifierSystem.AnyEnabled) return; // all modifiers off: no per-frame work
            ModifierSystem.Tick();
            ModifierSystem.ApplyGiantScales();
        }
    }
}

using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Glitch
{
    /// <summary>
    /// The Glitch gameplay logic (ported from Town-Of-Us' Glitch.cs).
    ///
    /// A Neutral Killing role with three abilities on three buttons:
    ///   - Mimic: copy the nearest player's name + body color for a duration.
    ///   - Hack: block the target from reporting bodies for a duration.
    ///   - Kill: a direct kill through KillManager.
    /// Win condition: be the last player standing (same elimination check as
    /// the Arsonist). Rides the Crewmate pool like the other neutrals.
    /// </summary>
    internal static class GlitchSystem
    {
        private const string MimicRpc = "townofus.GlitchMimic";
        private const string HackRpc = "townofus.GlitchHack";
        private const string RequestMimicRpc = "townofus.GlitchRequestMimic";
        private const string RequestHackRpc = "townofus.GlitchRequestHack";
        private const string RequestKillRpc = "townofus.GlitchRequestKill";
        private const string KillRpc = "townofus.GlitchKill";
        private const string WinRpc = "townofus.GlitchWin";
        private const int MaxRetries = 300;

        private static readonly Dictionary<byte, DateTime> MimicUntil = new();
        private static readonly Dictionary<byte, DateTime> HackUntil = new();
        // Independent cooldowns per ability (the OG role has three separate
        // buttons with three separate timers).
        private static readonly Dictionary<byte, DateTime> MimicCooldowns = new();
        private static readonly Dictionary<byte, DateTime> HackCooldowns = new();
        private static readonly Dictionary<byte, DateTime> KillCooldowns = new();
        private static readonly Dictionary<byte, (string Name, int Color)> OriginalOutfit = new();
        private static bool _glitchWon;
        private static int _resultScreenRetries;
        private static bool _poolAssignmentDone;
        private static int _poolAssignmentAttempts;
        private static byte? _pendingPlayerId;
        private static int _pendingRetries;

        public static bool IsGlitch(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, GlitchRole.Id);

        public static bool IsHacked(PlayerControl player) =>
            player != null && player.Data != null && HackUntil.TryGetValue(player.PlayerId, out var until) && DateTime.UtcNow < until;

        // ── Mimic ────────────────────────────────────────────────────────────
        internal static bool CanMimicNow(PlayerControl glitch)
        {
            if (!IsGlitch(glitch) || glitch.Data == null || glitch.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(glitch.PlayerId, MimicCooldowns) &&
                   ClosestPlayerFinder.GetClosestTarget(glitch, out _);
        }

        public static void TryMimic(PlayerControl glitch)
        {
            var client = AmongUsClient.Instance;
            if (client == null || glitch == null || glitch.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestMimicRpc, glitch.PlayerId);
                return;
            }
            if (!CanMimicNow(glitch)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(glitch, out var target)) return;
            if (target.Data == null) return;

            var targetName = target.Data.PlayerName;
            var targetColor = target.Data.ColorId;
            var ownName = glitch.Data.PlayerName;
            var ownColor = glitch.Data.ColorId;

            OriginalOutfit[glitch.PlayerId] = (ownName, ownColor);
            MimicUntil[glitch.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.GlitchMimicDuration, 10f));
            MimicCooldowns[glitch.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.GlitchMimicCooldown, 30f));

            ApplyName(glitch, targetName);
            Recolor(glitch, targetColor);
            TownOfUsRpcMux.Send(MimicRpc, glitch.PlayerId, targetName, targetColor);
            Local("You mimicked " + targetName + ".");
        }

        // ── Hack ─────────────────────────────────────────────────────────────
        internal static bool CanHackNow(PlayerControl glitch)
        {
            if (!IsGlitch(glitch) || glitch.Data == null || glitch.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(glitch.PlayerId, HackCooldowns) &&
                   ClosestPlayerFinder.GetClosestTarget(glitch, out _);
        }

        public static void TryHack(PlayerControl glitch)
        {
            var client = AmongUsClient.Instance;
            if (client == null || glitch == null || glitch.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestHackRpc, glitch.PlayerId);
                return;
            }
            if (!CanHackNow(glitch)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(glitch, out var target)) return;
            if (target == glitch || target.Data == null) return;

            HackUntil[target.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.GlitchHackDuration, 10f));
            HackCooldowns[glitch.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.GlitchHackCooldown, 30f));
            TownOfUsRpcMux.Send(HackRpc, target.PlayerId);
            Local("You hacked " + target.Data.PlayerName + ".");
        }

        // ── Kill ─────────────────────────────────────────────────────────────
        internal static bool CanKillNow(PlayerControl glitch)
        {
            if (!IsGlitch(glitch) || glitch.Data == null || glitch.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(glitch.PlayerId, KillCooldowns) &&
                   ClosestPlayerFinder.GetClosestTarget(glitch, out _);
        }

        public static void TryKill(PlayerControl glitch)
        {
            var client = AmongUsClient.Instance;
            if (client == null || glitch == null || glitch.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestKillRpc, glitch.PlayerId);
                return;
            }
            if (!CanKillNow(glitch)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(glitch, out var target)) return;
            if (target == glitch || target.Data == null) return;

            KillCooldowns[glitch.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.GlitchKillCooldown, 30f));
            KillManager.Kill(glitch, target);
            TownOfUsRpcMux.Send(KillRpc, target.PlayerId);
        }

        // ── Round lifecycle / pool ───────────────────────────────────────────
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();

        public static void Reset()
        {
            MimicUntil.Clear();
            HackUntil.Clear();
            MimicCooldowns.Clear();
            HackCooldowns.Clear();
            KillCooldowns.Clear();
            OriginalOutfit.Clear();
            _glitchWon = false;
            _resultScreenRetries = 0;
            _poolAssignmentDone = false;
            _poolAssignmentAttempts = 0;
            _pendingPlayerId = null;
            _pendingRetries = 0;
        }

        public static void OnGameEnded(GameEndedEventArgs _) { }

        /// <summary>Host tick: revert expired mimics, and run the elimination win check.</summary>
        public static void Tick()
        {
            if (_glitchWon && _resultScreenRetries < MaxRetries) _resultScreenRetries++;
            else if (_resultScreenRetries >= MaxRetries) _glitchWon = false;

            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            if (!_poolAssignmentDone && _poolAssignmentAttempts < MaxRetries)
            {
                _poolAssignmentAttempts++;
                TryAssignFromPool(RoleTeamTypes.Crewmate);
            }

            RevertExpiredMimics();

            if (!_pendingPlayerId.HasValue)
            {
                CheckEliminationWin();
                return;
            }
            var player = PlayerUtils.FindById(_pendingPlayerId.Value);
            if (player != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(player, GlitchRole.Id);
                if (RoleRegistry.IsAssigned(player, GlitchRole.Id))
                {
                    _pendingPlayerId = null;
                    _pendingRetries = 0;
                }
            }
            if (++_pendingRetries >= MaxRetries)
            {
                _pendingPlayerId = null;
                _pendingRetries = 0;
            }
        }

        private static void RevertExpiredMimics()
        {
            if (MimicUntil.Count == 0) return;
            var now = DateTime.UtcNow;
            foreach (var key in new List<byte>(MimicUntil.Keys))
            {
                if (now < MimicUntil[key]) continue;
                var glitch = PlayerUtils.FindById(key);
                if (glitch == null || glitch.Data == null) continue;

                MimicUntil.Remove(key);
                var (ownName, ownColor) = OriginalOutfit.TryGetValue(key, out var cached)
                    ? cached
                    : (glitch.Data.PlayerName, glitch.Data.ColorId);
                OriginalOutfit.Remove(key);

                ApplyName(glitch, ownName);
                Recolor(glitch, ownColor);
                TownOfUsRpcMux.Send("townofus.GlitchRevert", key, ownName, ownColor);
                return; // one revert per tick is plenty
            }
        }

        public static void TryAssignFromPool(RoleTeamTypes type)
        {
            if (type != RoleTeamTypes.Crewmate || _poolAssignmentDone) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            var requested = RoleConfig.Count(RoleConfig.GlitchCount);
            if (requested <= 0)
            {
                _poolAssignmentDone = true;
                return;
            }

            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && RoleRegistry.IsAssigned(player, GlitchRole.Id)) return;

            var candidates = new List<PlayerControl>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected || player.Data.IsDead) continue;
                if (player.Data.myRole == null || player.Data.myRole.RoleTeamType != RoleTeamTypes.Crewmate) continue;
                if (IsClaimedByCustomRole(player)) continue;
                candidates.Add(player);
            }
            if (candidates.Count == 0) return;
            if (RoleManager.Instance == null) return;

            var assigned = 0;
            while (assigned < requested && candidates.Count > 0)
            {
                if (UnityEngine.Random.Range(0f, 100f) >= RoleConfig.Chance(RoleConfig.GlitchChance)) break;
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var target = candidates[index];
                candidates.RemoveAt(index);
                RoleManager.Instance.AssignRole(target, GlitchRole.Id);
                if (!RoleRegistry.IsAssigned(target, GlitchRole.Id)) continue;
                assigned++;
                TownOfUsRpcMux.Send(AssignRpc, target.PlayerId);
            }

            _poolAssignmentDone = true;
        }

        private const string AssignRpc = "townofus.GlitchAssign";

        private static bool IsClaimedByCustomRole(PlayerControl player) =>
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Shifter.ShifterRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Phantom.PhantomRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Sheriff.SheriffRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Engineer.EngineerRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Medic.MedicRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Seer.SeerRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Vigilante.VigilanteRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Altruist.AltruistRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Mayor.MayorRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Swapper.SwapperRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Spy.SpyRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Investigator.InvestigatorRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.TimeLord.TimeLordRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Snitch.SnitchRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Jester.JesterRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Executioner.ExecutionerRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Arsonist.ArsonistRole.Id);

        /// <summary>The Glitch wins when every other living player is dead.</summary>
        private static void CheckEliminationWin()
        {
            if (_glitchWon || ShipStatus.Instance == null) return;
            var aliveGlitches = 0;
            var aliveOthers = 0;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected || player.Data.IsDead) continue;
                if (RoleRegistry.IsAssigned(player, GlitchRole.Id)) aliveGlitches++;
                else aliveOthers++;
            }
            if (aliveGlitches == 0 || aliveOthers > 0) return;

            _glitchWon = true;
            _resultScreenRetries = 0;
            TownOfUsRpcMux.Send(WinRpc);
            ShipStatus.Instance.StartEndGame(GameOverReason.Custom, 0.5f);
        }

        // ── RPCs ─────────────────────────────────────────────────────────────
        [ReactorRpc(RequestMimicRpc)]
        private static void OnRequestMimic(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryMimic(player);
                    return;
                }
            }
        }

        [ReactorRpc(RequestHackRpc)]
        private static void OnRequestHack(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryHack(player);
                    return;
                }
            }
        }

        [ReactorRpc(RequestKillRpc)]
        private static void OnRequestKill(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryKill(player);
                    return;
                }
            }
        }

        [ReactorRpc(MimicRpc)]
        private static void OnMimic(byte senderId, byte glitchId, string targetName, int targetColor)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var glitch = PlayerUtils.FindById(glitchId);
            if (glitch == null) return;
            // The name already arrived through the host's RpcSetName broadcast;
            // clients only need to recolor the body.
            Recolor(glitch, targetColor);
        }

        [ReactorRpc("townofus.GlitchRevert")]
        private static void OnRevert(byte senderId, byte glitchId, string ownName, int ownColor)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var glitch = PlayerUtils.FindById(glitchId);
            if (glitch == null) return;
            Recolor(glitch, ownColor);
        }

        [ReactorRpc(HackRpc)]
        private static void OnHack(byte senderId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var target = PlayerUtils.FindById(targetId);
            if (target == null) return;
            HackUntil[target.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.GlitchHackDuration, 10f));
            if (target.PlayerId == PlayerControl.LocalPlayer?.PlayerId)
                Local("You have been hacked! You cannot report bodies or do tasks.");
        }

        [ReactorRpc(KillRpc)]
        private static void OnKill(byte senderId, byte targetId)
        {
            // KillManager.Kill is already networked host-authoritative; the
            // companion RPC exists only to keep client-side kill tables in sync
            // for future hacking/guessing features.
        }

        [ReactorRpc(AssignRpc)]
        private static void OnAssignRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var target = PlayerUtils.FindById(playerId);
            if (target != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(target, GlitchRole.Id);
                return;
            }
            _pendingPlayerId = playerId;
            _pendingRetries = 0;
        }

        [ReactorRpc(WinRpc)]
        private static void OnWin(byte senderId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _glitchWon = true;
            _resultScreenRetries = 0;
        }

        // ── Hack suppression hooks ───────────────────────────────────────────
        /// <summary>GameEvents.BeforeReport hook: a hacked player cannot report bodies.</summary>
        public static void OnBeforeReport(ReportEventArgs args)
        {
            if (args.IsEmergencyMeeting || args.Reporter == null) return;
            if (IsHacked(args.Reporter)) args.Cancelled = true;
        }

        // ── End screen ───────────────────────────────────────────────────────
        public static bool HasPendingWin => _glitchWon;

        public static bool ApplyResultTitle(EndGameManager manager)
        {
            if (!_glitchWon || manager == null) return false;
            var applied = false;
            if (manager.WinText != null)
            {
                manager.WinText.text = "The Glitch Wins";
                manager.WinText.color = new Color(0.45f, 0.95f, 0.35f, 1f);
                applied = true;
            }
            if (manager.AltWinText != null)
            {
                manager.AltWinText.text = "The Glitch Wins";
                manager.AltWinText.color = new Color(0.45f, 0.95f, 0.35f, 1f);
                applied = true;
            }
            return applied;
        }

        public static void ConsumePendingWin() => _glitchWon = false;

        // ── Visual helpers (shared with Morphling) ───────────────────────────
        private static void ApplyName(PlayerControl player, string name)
        {
            if (player == null || string.IsNullOrEmpty(name)) return;
            try
            {
                if (player.Data != null && player.Data.PlayerName != name)
                    player.RpcSetName(name);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Glitch name: " + e.Message);
            }
        }

        private static void Recolor(PlayerControl player, int colorId)
        {
            if (player == null) return;
            try
            {
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    try { PlayerControl.SetPlayerMaterialColors(colorId, renderer); } catch { }
                }
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Glitch recolor: " + e.Message);
            }
        }

        private static PlayerControl PlayerUtils.FindById(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        private static DateTime GetCooldown(byte glitchId, Dictionary<byte, DateTime> table) =>
            table.TryGetValue(glitchId, out var value) ? value : DateTime.MinValue;

        private static void Local(string message)
        {
            try
            {
                SystemChat.Show(message);
            }
            catch { }
        }
    }

    // String "Update" form: EndGameManager.Update is missed by the local GameLibs
    // interop (same rationale as the Jester patch, see PORTING.md).
    [HarmonyPatch(typeof(EndGameManager), "Update")]
    internal static class EndGameManager_Update_GlitchPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!GlitchSystem.HasPendingWin || __instance == null) return;
            if (GlitchSystem.ApplyResultTitle(__instance)) GlitchSystem.ConsumePendingWin();
        }
    }

    // String "SetEverythingUp" form: the method is private in the 8.9 game, so
    // the interop omits it and nameof would fail to compile. Harmony resolves
    // the string via reflection at runtime (same rationale as "Update" above).
    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    internal static class EndGameManager_SetEverythingUp_GlitchPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!GlitchSystem.HasPendingWin || __instance == null) return;
            try
            {
                if (GlitchSystem.ApplyResultTitle(__instance)) GlitchSystem.ConsumePendingWin();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Glitch end-screen patch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    internal static class ExileController_Begin_GlitchPatch
    {
        private static void Prefix(ExileController __instance, GameData.PlayerInfo exiled, bool tie)
        {
            if (__instance == null || exiled == null || tie) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.PlayerId != exiled.PlayerId) continue;
                if (!RoleRegistry.IsAssigned(player, GlitchRole.Id)) return;
                var text = exiled.PlayerName + " was The Glitch.";
                if (__instance.Text != null) __instance.Text.Text = text;
                // completeString is protected in the 2026.8.9 interop.
                GameReflection.SetCompleteString(__instance, text);
                return;
            }
        }
    }

    // Exile reveal text is re-applied every frame by Core/ExileTextFix (polling
    // ExileController.Instance). The old ExileController_Animate_GlitchPatch
    // targeted the compiler-generated coroutine type ExileController.
    // _Animate_d__17.MoveNext, which the 2026.8.9 interop hides as a private
    // nested type — never reference compiler-generated coroutine types (see
    // PORTING.md).

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_GlitchPatch
    {
        // Deferred to the FixedUpdate Tick loop — assigning synchronously inside
        // the game's native AssignRolesForTeam pass (ShipStatus.Start) touches
        // freshly-spawned players mid-transition and can fault the CLR (segfault).
        // Tick() runs the same pool a few frames later once the scene settles.
        private static void Postfix(RoleTeamTypes type, int max) { }
    }
}

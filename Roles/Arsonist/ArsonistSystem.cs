using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;
using TownOfUs.ManuAPI.Roles.Spy;

namespace TownOfUs.ManuAPI.Roles.Arsonist
{
    /// <summary>
    /// Arsonist gameplay logic (ported from Town-Of-Us' Arsonist.cs).
    ///
    /// Douse marks the nearest player (synced over Manactor); Ignite kills every
    /// doused player through KillManager. The Arsonist wins when every other
    /// living player is dead. Like the other neutrals it rides the vanilla
    /// Crewmate pool, converted host-side at round start.
    /// </summary>
    internal static class ArsonistSystem
    {
        private const string DouseRpc = "townofus.ArsonistDouse";
        private const string RequestDouseRpc = "townofus.ArsonistRequestDouse";
        private const string RequestIgniteRpc = "townofus.ArsonistRequestIgnite";
        private const string IgniteRpc = "townofus.ArsonistIgnite";
        private const string WinRpc = "townofus.ArsonistWin";
        private const int MaxRetries = 300;

        private static readonly HashSet<byte> Doused = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static bool _arsonistWon;
        private static int _resultScreenRetries;
        private static bool _poolAssignmentDone;
        private static int _poolAssignmentAttempts;
        private static byte? _pendingPlayerId;
        private static int _pendingRetries;

        public static bool IsArsonist(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, ArsonistRole.Id);

        public static int DousedCount => Doused.Count;

        internal static bool CanDouseNow(PlayerControl arsonist)
        {
            if (!IsArsonist(arsonist) || arsonist.Data == null || arsonist.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(arsonist.PlayerId) &&
                   ClosestPlayerFinder.GetClosestTarget(arsonist, out _);
        }

        internal static bool CanIgniteNow(PlayerControl arsonist) =>
            IsArsonist(arsonist) && arsonist.Data != null && !arsonist.Data.IsDead && Doused.Count > 0;

        public static void TryDouse(PlayerControl arsonist)
        {
            var client = AmongUsClient.Instance;
            if (client == null || arsonist == null || arsonist.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestDouseRpc, arsonist.PlayerId);
                return;
            }
            if (!CanDouseNow(arsonist)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(arsonist, out var target)) return;
            if (!Doused.Add(target.PlayerId)) return; // already doused

            Cooldowns[arsonist.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.ArsonistDouseCooldown, 10f));
            TownOfUsRpcMux.Send(DouseRpc, target.PlayerId);
            SpySystem.OnPlayerDoused(target.PlayerId);
        }

        public static void TryIgnite(PlayerControl arsonist)
        {
            var client = AmongUsClient.Instance;
            if (client == null || arsonist == null || arsonist.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestIgniteRpc, arsonist.PlayerId);
                return;
            }
            if (!CanIgniteNow(arsonist)) return;

            var victims = new List<PlayerControl>();
            foreach (var id in Doused)
            {
                var player = FindPlayer(id);
                if (player != null && player.Data != null && !player.Data.IsDead) victims.Add(player);
            }
            if (victims.Count == 0) return;

            Doused.Clear();
            foreach (var victim in victims) KillManager.Kill(arsonist, victim);
            TownOfUsRpcMux.Send(IgniteRpc);
            Local("The Arsonist ignited their doused targets!");
        }

        // ── Round lifecycle / pool ───────────────────────────────────────────
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();

        public static void Reset()
        {
            Doused.Clear();
            Cooldowns.Clear();
            _arsonistWon = false;
            _resultScreenRetries = 0;
            _poolAssignmentDone = false;
            _poolAssignmentAttempts = 0;
            _pendingPlayerId = null;
            _pendingRetries = 0;
        }

        public static void OnGameEnded(GameEndedEventArgs _) { }

        public static void Tick()
        {
            if (_arsonistWon && _resultScreenRetries < MaxRetries) _resultScreenRetries++;
            else if (_resultScreenRetries >= MaxRetries) _arsonistWon = false;

            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            if (!_poolAssignmentDone && _poolAssignmentAttempts < MaxRetries)
            {
                _poolAssignmentAttempts++;
                TryAssignFromPool(RoleTeamTypes.Crewmate);
            }

            if (!_pendingPlayerId.HasValue)
            {
                CheckEliminationWin();
                return;
            }
            var player = FindPlayer(_pendingPlayerId.Value);
            if (player != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(player, ArsonistRole.Id);
                if (RoleRegistry.IsAssigned(player, ArsonistRole.Id))
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

        public static void TryAssignFromPool(RoleTeamTypes type)
        {
            if (type != RoleTeamTypes.Crewmate || _poolAssignmentDone) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            var requested = RoleConfig.Count(RoleConfig.ArsonistCount);
            if (requested <= 0)
            {
                _poolAssignmentDone = true;
                return;
            }

            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && RoleRegistry.IsAssigned(player, ArsonistRole.Id)) return;

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
                if (UnityEngine.Random.Range(0f, 100f) >= RoleConfig.Chance(RoleConfig.ArsonistChance)) break;
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var target = candidates[index];
                candidates.RemoveAt(index);
                RoleManager.Instance.AssignRole(target, ArsonistRole.Id);
                if (!RoleRegistry.IsAssigned(target, ArsonistRole.Id)) continue;
                assigned++;
                TownOfUsRpcMux.Send(AssignRpc, target.PlayerId);
            }

            _poolAssignmentDone = true;
        }

        private const string AssignRpc = "townofus.ArsonistAssign";

        private static bool IsClaimedByCustomRole(PlayerControl player) =>
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Shifter.ShifterRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Glitch.GlitchRole.Id) ||
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
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Executioner.ExecutionerRole.Id);

        /// <summary>The Arsonist wins when every other living player is dead.</summary>
        private static void CheckEliminationWin()
        {
            if (_arsonistWon || ShipStatus.Instance == null) return;
            var aliveArsonists = 0;
            var aliveOthers = 0;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected || player.Data.IsDead) continue;
                if (RoleRegistry.IsAssigned(player, ArsonistRole.Id)) aliveArsonists++;
                else aliveOthers++;
            }
            if (aliveArsonists == 0 || aliveOthers > 0) return;

            _arsonistWon = true;
            _resultScreenRetries = 0;
            TownOfUsRpcMux.Send(WinRpc);
            ShipStatus.Instance.StartEndGame(GameOverReason.Custom, 0.5f);
        }

        // ── RPCs ─────────────────────────────────────────────────────────────
        [ManactorRpc(RequestDouseRpc)]
        private static void OnRequestDouse(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryDouse(player);
                    return;
                }
            }
        }

        [ManactorRpc(RequestIgniteRpc)]
        private static void OnRequestIgnite(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryIgnite(player);
                    return;
                }
            }
        }

        [ManactorRpc(DouseRpc)]
        private static void OnDouse(byte senderId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            Doused.Add(targetId);
        }

        [ManactorRpc(AssignRpc)]
        private static void OnAssignRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var target = FindPlayer(playerId);
            if (target != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(target, ArsonistRole.Id);
                return;
            }
            _pendingPlayerId = playerId;
            _pendingRetries = 0;
        }

        [ManactorRpc(IgniteRpc)]
        private static void OnIgnite(byte senderId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            Doused.Clear();
            Local("The Arsonist ignited their doused targets!");
        }

        [ManactorRpc(WinRpc)]
        private static void OnWin(byte senderId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _arsonistWon = true;
            _resultScreenRetries = 0;
        }

        // ── End screen ───────────────────────────────────────────────────────
        public static bool HasPendingWin => _arsonistWon;

        public static bool ApplyResultTitle(EndGameManager manager)
        {
            if (!_arsonistWon || manager == null) return false;
            var applied = false;
            if (manager.WinText != null)
            {
                manager.WinText.text = "Arsonist Wins";
                manager.WinText.color = new Color(1f, 0.45f, 0.15f, 1f);
                applied = true;
            }
            if (manager.AltWinText != null)
            {
                manager.AltWinText.text = "Arsonist Wins";
                manager.AltWinText.color = new Color(1f, 0.45f, 0.15f, 1f);
                applied = true;
            }
            return applied;
        }

        public static void ConsumePendingWin() => _arsonistWon = false;

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        private static DateTime GetCooldown(byte arsonistId) =>
            Cooldowns.TryGetValue(arsonistId, out var value) ? value : DateTime.MinValue;

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
    internal static class EndGameManager_Update_ArsonistPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!ArsonistSystem.HasPendingWin || __instance == null) return;
            if (ArsonistSystem.ApplyResultTitle(__instance)) ArsonistSystem.ConsumePendingWin();
        }
    }

    // String "SetEverythingUp" form: the method is private in the 8.9 game, so
    // the interop omits it and nameof would fail to compile. Harmony resolves
    // the string via reflection at runtime (same rationale as "Update" above).
    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    internal static class EndGameManager_SetEverythingUp_ArsonistPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!ArsonistSystem.HasPendingWin || __instance == null) return;
            try
            {
                if (ArsonistSystem.ApplyResultTitle(__instance)) ArsonistSystem.ConsumePendingWin();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Arsonist end-screen patch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    internal static class ExileController_Begin_ArsonistPatch
    {
        private static void Prefix(ExileController __instance, GameData.PlayerInfo exiled, bool tie)
        {
            if (__instance == null || exiled == null || tie) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.PlayerId != exiled.PlayerId) continue;
                if (!RoleRegistry.IsAssigned(player, ArsonistRole.Id)) return;
                var text = exiled.PlayerName + " was the Arsonist.";
                if (__instance.Text != null) __instance.Text.Text = text;
                // completeString is protected in the 2026.8.9 interop.
                GameReflection.SetCompleteString(__instance, text);
                return;
            }
        }
    }

    // Exile reveal text is re-applied every frame by Core/ExileTextFix (polling
    // ExileController.Instance). The old ExileController_Animate_ArsonistPatch
    // targeted the compiler-generated coroutine type ExileController.
    // _Animate_d__17.MoveNext, which the 2026.8.9 interop hides as a private
    // nested type — never reference compiler-generated coroutine types (see
    // PORTING.md).

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_ArsonistPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance == PlayerControl.LocalPlayer) ArsonistSystem.Tick();
        }
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_ArsonistPatch
    {
        // Deferred to the FixedUpdate Tick loop — assigning synchronously inside
        // the game's native AssignRolesForTeam pass (ShipStatus.Start) touches
        // freshly-spawned players mid-transition and can fault the CLR (segfault).
        // Tick() runs the same pool a few frames later once the scene settles.
        private static void Postfix(RoleTeamTypes type, int max) { }
    }
}

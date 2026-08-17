using System;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;
using TownOfUs.ManuAPI.Roles.Engineer;
using TownOfUs.ManuAPI.Roles.Sheriff;

namespace TownOfUs.ManuAPI.Roles.Jester
{
    internal static class JesterSystem
    {
        private const string JesterWinRpc = "townofus.JesterWin";
        private const string JesterAssignRpc = "townofus.JesterAssign";
        private const int MaxAssignmentRetries = 300;
        private static bool _jesterWon;
        private static int _resultScreenRetries;
        private static bool _poolAssignmentDone;
        private static int _poolAssignmentAttempts;
        private static byte? _pendingAssignmentPlayerId;
        private static int _pendingAssignmentRetries;

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();

        public static void Reset()
        {
            _jesterWon = false;
            _resultScreenRetries = 0;
            _poolAssignmentDone = false;
            _poolAssignmentAttempts = 0;
            _pendingAssignmentPlayerId = null;
            _pendingAssignmentRetries = 0;
        }

        public static void Tick()
        {
            if (_jesterWon && _resultScreenRetries < MaxAssignmentRetries) _resultScreenRetries++;
            else if (_resultScreenRetries >= MaxAssignmentRetries) _jesterWon = false;

            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost && !_poolAssignmentDone && _poolAssignmentAttempts < MaxAssignmentRetries)
            {
                _poolAssignmentAttempts++;
                TryAssignFromPool(RoleTeamTypes.Crewmate);
            }

            if (!_pendingAssignmentPlayerId.HasValue) return;
            var player = FindPlayer(_pendingAssignmentPlayerId.Value);
            if (player != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(player, JesterRole.Id);
                if (RoleRegistry.IsAssigned(player, JesterRole.Id))
                {
                    _pendingAssignmentPlayerId = null;
                    _pendingAssignmentRetries = 0;
                    return;
                }
            }
            if (++_pendingAssignmentRetries >= MaxAssignmentRetries)
            {
                _pendingAssignmentPlayerId = null;
                _pendingAssignmentRetries = 0;
            }
        }

        public static void TryAssignFromPool(RoleTeamTypes type)
        {
            if (type != RoleTeamTypes.Crewmate || _poolAssignmentDone) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            var requested = RoleConfig.Count(RoleConfig.JesterCount);
            if (requested <= 0)
            {
                _poolAssignmentDone = true;
                return;
            }

            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && RoleRegistry.IsAssigned(player, JesterRole.Id)) return;

            var candidates = new System.Collections.Generic.List<PlayerControl>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected || player.Data.IsDead) continue;
                if (player.Data.myRole == null || player.Data.myRole.RoleTeamType != RoleTeamTypes.Crewmate) continue;
                if (RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Sheriff.SheriffRole.Id) ||
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
                    RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Executioner.ExecutionerRole.Id) ||
                    RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Arsonist.ArsonistRole.Id) ||
                    RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Phantom.PhantomRole.Id) ||
                    RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Shifter.ShifterRole.Id) ||
                    RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Glitch.GlitchRole.Id)) continue;
                candidates.Add(player);
            }
            if (candidates.Count == 0) return;
            if (RoleManager.Instance == null) return;

            var assigned = 0;
            while (assigned < requested && candidates.Count > 0)
            {
                if (UnityEngine.Random.Range(0f, 100f) >= RoleConfig.Chance(RoleConfig.JesterChance)) break;
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var target = candidates[index];
                candidates.RemoveAt(index);
                RoleManager.Instance.AssignRole(target, JesterRole.Id);
                if (!RoleRegistry.IsAssigned(target, JesterRole.Id)) continue;
                assigned++;
                TownOfUsRpcMux.Send(JesterAssignRpc, target.PlayerId);
            }

            _poolAssignmentDone = true;
        }

        public static void OnGameEnded(GameEndedEventArgs _) { }

        public static void OnPlayerExiled(PlayerEventArgs args)
        {
            if (args?.Player == null || !IsJester(args.Player)) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || ShipStatus.Instance == null) return;
            _jesterWon = true;
            _resultScreenRetries = 0;
            TownOfUsRpcMux.Send(JesterWinRpc);
            ShipStatus.Instance.StartEndGame(GameOverReason.Custom, 0.5f);
        }

        [ManactorRpc(JesterAssignRpc)]
        private static void OnJesterAssignRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var target = FindPlayer(playerId);
            if (target != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(target, JesterRole.Id);
                NotifyConverted(target);
                return;
            }
            _pendingAssignmentPlayerId = playerId;
            _pendingAssignmentRetries = 0;
        }

        /// <summary>
        /// Converts a player to the Jester (host-authoritative, e.g. an
        /// Executioner whose target died). Assigns the role locally and tells
        /// every client to do the same through the existing assign RPC.
        /// </summary>
        public static void ConvertToJester(PlayerControl player)
        {
            if (player == null || player.Data == null) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return; // host-authoritative
            if (RoleManager.Instance == null) return;
            if (RoleRegistry.IsAssigned(player, JesterRole.Id)) return;

            RoleManager.Instance.AssignRole(player, JesterRole.Id);
            TownOfUsRpcMux.Send(JesterAssignRpc, player.PlayerId);
            NotifyConverted(player);
        }

        private static void NotifyConverted(PlayerControl player)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || player == null || local.PlayerId != player.PlayerId) return;
            try
            {
                SystemChat.Show("You became the Jester — get yourself voted out to win!");
            }
            catch { }
        }

        [ManactorRpc(JesterWinRpc)]
        private static void OnJesterWinRpc(byte senderId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _jesterWon = true;
            _resultScreenRetries = 0;
        }

        public static bool HasPendingWin => _jesterWon;

        public static bool ApplyResultTitle(EndGameManager manager)
        {
            if (!_jesterWon || manager == null) return false;
            var applied = false;
            if (manager.WinText != null)
            {
                manager.WinText.text = "Jester Wins";
                manager.WinText.color = new Color(0.86f, 0.35f, 0.95f, 1f);
                applied = true;
            }
            if (manager.AltWinText != null)
            {
                manager.AltWinText.text = "Jester Wins";
                manager.AltWinText.color = new Color(0.86f, 0.35f, 0.95f, 1f);
                applied = true;
            }
            return applied;
        }

        public static void ConsumePendingWin() => _jesterWon = false;

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        public static bool IsJester(PlayerControl player) => RoleRegistry.IsAssigned(player, JesterRole.Id);
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    internal static class ExileController_Begin_JesterPatch
    {
        private static void Prefix(ExileController __instance, GameData.PlayerInfo exiled, bool tie)
        {
            if (__instance == null || exiled == null || tie) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.PlayerId != exiled.PlayerId) continue;
                if (!RoleRegistry.IsAssigned(player, JesterRole.Id)) return;
                var text = exiled.PlayerName + " was the Jester.";
                if (__instance.Text != null) __instance.Text.Text = text;
                // completeString is protected in the 2026.8.9 interop.
                GameReflection.SetCompleteString(__instance, text);
                return;
            }
        }
    }

    // Exile reveal text is re-applied every frame by Core/ExileTextFix (polling
    // ExileController.Instance). The old ExileController_Animate_JesterPatch
    // targeted the compiler-generated coroutine type ExileController.
    // _Animate_d__17.MoveNext, which the 2026.8.9 interop hides as a private
    // nested type — never reference compiler-generated coroutine types (see
    // PORTING.md).

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_JesterPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance == PlayerControl.LocalPlayer) JesterSystem.Tick();
        }
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_JesterPatch
    {
        // Assignment is deliberately deferred to the FixedUpdate Tick loop.
        // Running TryAssignFromPool synchronously inside the game's native
        // AssignRolesForTeam pass (ShipStatus.Start) touches freshly-spawned
        // players mid-transition and can fault the CLR (segfault). Tick() runs
        // the same pool a few frames later, once the scene has settled.
        private static void Postfix(RoleTeamTypes type, int max) { }
    }

    // "Update" is a string on purpose: the 8.9 local GameLibs interop missed
    // EndGameManager.Update (nameof would fail to compile against it), while
    // the runtime-generated interop has it. Harmony resolves via reflection,
    // so a string target survives interop drift (see PORTING.md).
    [HarmonyPatch(typeof(EndGameManager), "Update")]
    internal static class EndGameManager_Update_JesterPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!JesterSystem.HasPendingWin || __instance == null) return;
            if (JesterSystem.ApplyResultTitle(__instance)) JesterSystem.ConsumePendingWin();
        }
    }

    // String "SetEverythingUp" form: the method is private in the 8.9 game, so
    // the interop omits it and nameof would fail to compile. Harmony resolves
    // the string via reflection at runtime (same rationale as "Update" above).
    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    internal static class EndGameManager_SetEverythingUp_JesterPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!JesterSystem.HasPendingWin || __instance == null) return;
            try
            {
                if (JesterSystem.ApplyResultTitle(__instance)) JesterSystem.ConsumePendingWin();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Jester end-screen patch: " + e);
            }
        }
    }
}

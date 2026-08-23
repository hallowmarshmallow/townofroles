using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Phantom
{
    /// <summary>
    /// Phantom gameplay logic (ported from Town-Of-Us' Phantom.cs).
    ///
    /// The Phantom rides the vanilla Crewmate pool (converted host-side like the
    /// other neutrals). When they die they become a semi-transparent phantom and
    /// must complete all their remaining tasks to win. The host detects the
    /// death, broadcasts it so every client renders the phantom faded, and
    /// watches task completion for the win ("Phantom Wins" end screen).
    /// </summary>
    internal static class PhantomSystem
    {
        private const string DeathRpc = "townofus.PhantomDeath";
        private const string WinRpc = "townofus.PhantomWin";
        private const int MaxRetries = 300;

        private static readonly HashSet<byte> PhantomDead = new(); // all clients: faded rendering
        private static bool _phantomWon;
        private static int _resultScreenRetries;
        private static bool _poolAssignmentDone;
        private static int _poolAssignmentAttempts;
        private static byte? _pendingPlayerId;
        private static int _pendingRetries;

        public static bool IsPhantom(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, PhantomRole.Id);

        /// <summary>True once the phantom has died (win condition is now live).</summary>
        public static bool IsPhantomDead(PlayerControl player) =>
            player != null && PhantomDead.Contains(player.PlayerId);

        public static void Tick()
        {
            if (_phantomWon && _resultScreenRetries < MaxRetries) _resultScreenRetries++;
            else if (_resultScreenRetries >= MaxRetries) _phantomWon = false;

            var client = AmongUsClient.Instance;
            if (client == null) return;

            if (client.AmHost)
            {
                if (!_poolAssignmentDone && _poolAssignmentAttempts < MaxRetries)
                {
                    _poolAssignmentAttempts++;
                    TryAssignFromPool(RoleTeamTypes.Crewmate);
                }

                if (_pendingPlayerId.HasValue)
                {
                    var pending = FindPlayer(_pendingPlayerId.Value);
                    if (pending != null && RoleManager.Instance != null)
                    {
                        RoleManager.Instance.AssignRole(pending, PhantomRole.Id);
                        if (RoleRegistry.IsAssigned(pending, PhantomRole.Id))
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
                else
                {
                    CheckDeath();
                    CheckWin();
                }
            }

            // Every client (host included): keep dead phantoms faded. The game
            // restores material colors on cosmetic refreshes, so re-apply
            // idempotently each tick instead of relying on the one-shot RPC.
            if (PhantomDead.Count > 0)
            {
                foreach (var id in new List<byte>(PhantomDead))
                    Fade(FindPlayer(id));
            }
        }

        /// <summary>Host: convert a crewmate into the Phantom (neutral riding the crewmate pool).</summary>
        public static void TryAssignFromPool(RoleTeamTypes type)
        {
            if (type != RoleTeamTypes.Crewmate || _poolAssignmentDone) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            var requested = RoleConfig.Count(RoleConfig.PhantomCount);
            if (requested <= 0)
            {
                _poolAssignmentDone = true;
                return;
            }

            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && RoleRegistry.IsAssigned(player, PhantomRole.Id)) return;

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
                if (UnityEngine.Random.Range(0f, 100f) >= RoleConfig.Chance(RoleConfig.PhantomChance)) break;
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var target = candidates[index];
                candidates.RemoveAt(index);
                RoleManager.Instance.AssignRole(target, PhantomRole.Id);
                if (!RoleRegistry.IsAssigned(target, PhantomRole.Id)) continue;
                assigned++;
                TownOfUsRpcMux.Send(AssignRpc, target.PlayerId);
            }

            _poolAssignmentDone = true;
        }

        private const string AssignRpc = "townofus.PhantomAssign";

        private static void CheckDeath()
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;
                if (!IsPhantom(player)) continue;
                if (!player.Data.IsDead || PhantomDead.Contains(player.PlayerId)) continue;
                PhantomDead.Add(player.PlayerId);
                TownOfUsRpcMux.Send(DeathRpc, player.PlayerId);
                Fade(player);
            }
        }

        private static void CheckWin()
        {
            if (_phantomWon || ShipStatus.Instance == null) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected) continue;
                if (!IsPhantom(player) || !PhantomDead.Contains(player.PlayerId)) continue;
                if (player.Data.Tasks == null || player.Data.Tasks.Count == 0) continue;
                var allDone = true;
                for (int i = 0; i < player.Data.Tasks.Count; i++)
                    if (player.Data.Tasks.get_Item(i) == null || !player.Data.Tasks.get_Item(i).Complete) { allDone = false; break; }
                if (!allDone) continue;

                _phantomWon = true;
                _resultScreenRetries = 0;
                TownOfUsRpcMux.Send(WinRpc);
                ShipStatus.Instance.StartEndGame(GameOverReason.Custom, 0.5f);
                return;
            }
        }

        /// <summary>Semi-transparent body so the phantom can move unseen (host + clients).</summary>
        private static void Fade(PlayerControl player)
        {
            if (player == null) return;
            try
            {
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    var mats = renderer.materials;
                    if (mats == null) continue;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        mats[i].color = new Color(mats[i].color.r, mats[i].color.g, mats[i].color.b, 0.15f);
                    }
                }
            }
            catch { }
        }

        private static bool IsClaimedByCustomRole(PlayerControl player) =>
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Shifter.ShifterRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Glitch.GlitchRole.Id) ||
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

        [ReactorRpc(AssignRpc)]
        private static void OnAssignRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var target = FindPlayer(playerId);
            if (target != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(target, PhantomRole.Id);
                return;
            }
            _pendingPlayerId = playerId;
            _pendingRetries = 0;
        }

        [ReactorRpc(DeathRpc)]
        private static void OnDeath(byte senderId, byte phantomId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            PhantomDead.Add(phantomId);
            Fade(FindPlayer(phantomId));
        }

        [ReactorRpc(WinRpc)]
        private static void OnWin(byte senderId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _phantomWon = true;
            _resultScreenRetries = 0;
        }

        // ── End screen ───────────────────────────────────────────────────────
        public static bool HasPendingWin => _phantomWon;

        public static bool ApplyResultTitle(EndGameManager manager)
        {
            if (!_phantomWon || manager == null) return false;
            var applied = false;
            if (manager.WinText != null)
            {
                manager.WinText.text = "Phantom Wins";
                manager.WinText.color = new Color(0.75f, 0.75f, 0.85f, 1f);
                applied = true;
            }
            if (manager.AltWinText != null)
            {
                manager.AltWinText.text = "Phantom Wins";
                manager.AltWinText.color = new Color(0.75f, 0.75f, 0.85f, 1f);
                applied = true;
            }
            return applied;
        }

        public static void ConsumePendingWin() => _phantomWon = false;

        public static void Reset()
        {
            PhantomDead.Clear();
            _phantomWon = false;
            _resultScreenRetries = 0;
            _poolAssignmentDone = false;
            _poolAssignmentAttempts = 0;
            _pendingPlayerId = null;
            _pendingRetries = 0;
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) { }

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        private static void Local(string message)
        {
            try
            {
                SystemChat.Show(message);
            }
            catch { }
        }
    }

    // String "Update" form (same rationale as the Arsonist/Jester patches).
    [HarmonyPatch(typeof(EndGameManager), "Update")]
    internal static class EndGameManager_Update_PhantomPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!PhantomSystem.HasPendingWin || __instance == null) return;
            if (PhantomSystem.ApplyResultTitle(__instance)) PhantomSystem.ConsumePendingWin();
        }
    }

    // String "SetEverythingUp" form: the method is private in the 8.9 game, so
    // the interop omits it and nameof would fail to compile. Harmony resolves
    // the string via reflection at runtime (same rationale as "Update" above).
    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    internal static class EndGameManager_SetEverythingUp_PhantomPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!PhantomSystem.HasPendingWin || __instance == null) return;
            try
            {
                if (PhantomSystem.ApplyResultTitle(__instance)) PhantomSystem.ConsumePendingWin();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Phantom end-screen patch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    internal static class ExileController_Begin_PhantomPatch
    {
        private static void Prefix(ExileController __instance, GameData.PlayerInfo exiled, bool tie)
        {
            if (__instance == null || exiled == null || tie) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.PlayerId != exiled.PlayerId) continue;
                if (!RoleRegistry.IsAssigned(player, PhantomRole.Id)) return;
                var text = exiled.PlayerName + " was the Phantom.";
                if (__instance.Text != null) __instance.Text.Text = text;
                // completeString is protected in the 2026.8.9 interop.
                GameReflection.SetCompleteString(__instance, text);
                return;
            }
        }
    }

    // Exile reveal text is re-applied every frame by Core/ExileTextFix (polling
    // ExileController.Instance). The old ExileController_Animate_PhantomPatch
    // targeted the compiler-generated coroutine type ExileController.
    // _Animate_d__17.MoveNext, which the 2026.8.9 interop hides as a private
    // nested type — never reference compiler-generated coroutine types (see
    // PORTING.md).
}

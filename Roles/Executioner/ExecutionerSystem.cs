using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Executioner
{
    /// <summary>
    /// Executioner gameplay logic (ported from Town-Of-Us' Executioner.cs).
    ///
    /// Like the Jester, this Neutral rides on the vanilla Crewmate pool: the
    /// host converts a random unclaimed Crewmate into the Executioner and
    /// picks a secret target (any other living non-Impostor). When that
    /// target is voted out, the Executioner wins. Target assignment is synced
    /// over Manactor so every client agrees on who is whose target.
    /// </summary>
    internal static class ExecutionerSystem
    {
        private const string AssignRpc = "townofus.ExecutionerAssign";
        private const string WinRpc = "townofus.ExecutionerWin";
        private const string ConvertRpc = "townofus.ExecutionerConvert";
        private const int MaxRetries = 300;

        private static readonly Dictionary<byte, byte> TargetOf = new(); // executionerId -> targetId
        /// <summary>Players converted away from the Executioner (target died; now Jester/Crewmate).</summary>
        private static readonly HashSet<byte> Converted = new();
        private static bool _executionerWon;
        private static int _resultScreenRetries;
        private static bool _poolAssignmentDone;
        private static int _poolAssignmentAttempts;
        private static byte? _pendingExecutionerId;
        private static byte? _pendingTargetId;
        private static int _pendingRetries;

        public static bool IsExecutioner(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, ExecutionerRole.Id);

        /// <summary>True for an Executioner that has not converted away (still can win / show as Executioner).</summary>
        public static bool IsActiveExecutioner(PlayerControl player) =>
            IsExecutioner(player) && !Converted.Contains(player.PlayerId);

        /// <summary>True when the player converted to a plain Crewmate after their target died.</summary>
        public static bool IsConverted(PlayerControl player) =>
            player != null && Converted.Contains(player.PlayerId);

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();

        public static void Reset()
        {
            TargetOf.Clear();
            Converted.Clear();
            _executionerWon = false;
            _resultScreenRetries = 0;
            _poolAssignmentDone = false;
            _poolAssignmentAttempts = 0;
            _pendingExecutionerId = null;
            _pendingTargetId = null;
            _pendingRetries = 0;
        }

        public static void OnGameEnded(GameEndedEventArgs _) { }

        public static void Tick()
        {
            if (_executionerWon && _resultScreenRetries < MaxRetries) _resultScreenRetries++;
            else if (_resultScreenRetries >= MaxRetries) _executionerWon = false;

            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
            {
                if (!_poolAssignmentDone && _poolAssignmentAttempts < MaxRetries)
                {
                    _poolAssignmentAttempts++;
                    TryAssignFromPool(RoleTeamTypes.Crewmate);
                }
                // The target leaving the game also converts the Executioner
                // (no disconnect event exists, so poll the data flag).
                if (RoleConfig.ExecutionerConvertOnTargetDeath?.Value != false)
                {
                    foreach (var pair in TargetOf)
                    {
                        if (!Converted.Contains(pair.Key) && IsTargetGone(pair.Value))
                        {
                            var executioner = FindPlayer(pair.Key);
                            if (executioner != null) ConvertExecutioner(executioner);
                            break;
                        }
                    }
                }
            }

            if (!_pendingExecutionerId.HasValue) return;
            var player = FindPlayer(_pendingExecutionerId.Value);
            if (player != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(player, ExecutionerRole.Id);
                if (RoleRegistry.IsAssigned(player, ExecutionerRole.Id))
                {
                    var target = FindPlayer(_pendingTargetId ?? 255);
                    if (target != null) TargetOf[player.PlayerId] = target.PlayerId;
                    _pendingExecutionerId = null;
                    _pendingTargetId = null;
                    _pendingRetries = 0;
                    return;
                }
            }
            if (++_pendingRetries >= MaxRetries)
            {
                _pendingExecutionerId = null;
                _pendingTargetId = null;
                _pendingRetries = 0;
            }
        }

        public static void TryAssignFromPool(RoleTeamTypes type)
        {
            if (type != RoleTeamTypes.Crewmate || _poolAssignmentDone) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            var requested = RoleConfig.Count(RoleConfig.ExecutionerCount);
            if (requested <= 0)
            {
                _poolAssignmentDone = true;
                return;
            }

            // If a previous round already assigned Executioners, don't re-roll now.
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && RoleRegistry.IsAssigned(player, ExecutionerRole.Id)) return;

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
                if (UnityEngine.Random.Range(0f, 100f) >= RoleConfig.Chance(RoleConfig.ExecutionerChance)) break;
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var executioner = candidates[index];
                candidates.RemoveAt(index);

                var target = PickTarget(executioner);
                if (target == null) continue;

                RoleManager.Instance.AssignRole(executioner, ExecutionerRole.Id);
                if (!RoleRegistry.IsAssigned(executioner, ExecutionerRole.Id)) continue;
                TargetOf[executioner.PlayerId] = target.PlayerId;
                assigned++;
                TownOfUsRpcMux.Send(AssignRpc, executioner.PlayerId, target.PlayerId);
            }

            _poolAssignmentDone = true;
        }

        /// <summary>A random living, non-Impostor, unclaimed player (never the Executioner).</summary>
        private static PlayerControl PickTarget(PlayerControl executioner)
        {
            var targets = new List<PlayerControl>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player == executioner || player.Data == null) continue;
                if (player.Data.Disconnected || player.Data.IsDead) continue;
                if (player.Data.myRole == null || player.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor) continue;
                if (IsClaimedByCustomRole(player)) continue;
                targets.Add(player);
            }
            if (targets.Count == 0) return null;
            return targets[UnityEngine.Random.Range(0, targets.Count)];
        }

        /// <summary>True when the player already carries one of our custom roles.</summary>
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
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Arsonist.ArsonistRole.Id);

        public static void OnPlayerExiled(PlayerEventArgs args)
        {
            if (args?.Player == null) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || ShipStatus.Instance == null) return;

            // Find an Executioner whose target was just exiled.
            foreach (var pair in TargetOf)
            {
                if (pair.Value != args.Player.PlayerId) continue;
                var executioner = FindPlayer(pair.Key);
                if (executioner == null || !IsActiveExecutioner(executioner)) continue;
                _executionerWon = true;
                _resultScreenRetries = 0;
                TownOfUsRpcMux.Send(WinRpc, executioner.PlayerId, pair.Value);
                ShipStatus.Instance.StartEndGame(GameOverReason.Custom, 0.5f);
                return;
            }
        }

        // ── Conversion (target died by non-ejection means) ───────────────────
        /// <summary>
        /// GameEvents.BeforeMurder hook (host): when an Executioner's target is
        /// killed instead of voted out, the Executioner converts — becoming a
        /// Jester or a plain Crewmate (role configurable, ported from
        /// Town-Of-Us' "Executioner becomes on Target Dead" option).
        /// </summary>
        public static void OnBeforeMurder(MurderEventArgs args)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || args?.Target == null) return;
            if (RoleConfig.ExecutionerConvertOnTargetDeath?.Value == false) return;

            byte? executionerId = null;
            foreach (var pair in TargetOf)
            {
                if (pair.Value == args.Target.PlayerId) { executionerId = pair.Key; break; }
            }
            if (!executionerId.HasValue) return;
            var executioner = FindPlayer(executionerId.Value);
            if (executioner == null || !IsActiveExecutioner(executioner)) return;
            ConvertExecutioner(executioner);
        }

        private static bool IsTargetGone(byte targetId)
        {
            var target = FindPlayer(targetId);
            return target == null || target.Data == null || target.Data.Disconnected;
        }

        /// <summary>Converts the Executioner away from the role (host + clients).</summary>
        private static void ConvertExecutioner(PlayerControl executioner)
        {
            if (executioner == null || executioner.Data == null) return;
            Converted.Add(executioner.PlayerId);
            TargetOf.Remove(executioner.PlayerId);

            var mode = RoleConfig.ExecutionerConvertRole?.Value ?? "Jester";
            if (string.Equals(mode, "Crewmate", StringComparison.OrdinalIgnoreCase))
            {
                // Plain crewmate: vanilla role (the virtual Executioner registry
                // assignment lingers, but presentation/win hooks ignore it via
                // Converted, so the player is effectively a normal Crewmate).
                if (RoleManager.Instance != null) RoleManager.Instance.AssignRole(executioner, "Crewmate");
            }
            else
            {
                // Jester: the existing Jester machinery (assignment + RPC +
                // win-when-voted-out) takes over the player.
                TownOfUs.ManuAPI.Roles.Jester.JesterSystem.ConvertToJester(executioner);
            }
            TownOfUsRpcMux.Send(ConvertRpc, executioner.PlayerId, mode);
            // Jester mode notifies through JesterSystem.ConvertToJester already;
            // only the Crewmate path needs its own notification.
            if (string.Equals(mode, "Crewmate", StringComparison.OrdinalIgnoreCase))
                NotifyConverted(executioner, mode);
        }

        [ManactorRpc(ConvertRpc)]
        private static void OnConvertRpc(byte senderId, byte executionerId, string mode)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            Converted.Add(executionerId);
            TargetOf.Remove(executionerId);
            if (string.Equals(mode, "Crewmate", StringComparison.OrdinalIgnoreCase))
            {
                var player = FindPlayer(executionerId);
                if (player != null && RoleManager.Instance != null) RoleManager.Instance.AssignRole(player, "Crewmate");
                NotifyConverted(player, mode);
            }
        }

        private static void NotifyConverted(PlayerControl player, string mode)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || player == null || local.PlayerId != player.PlayerId) return;
            try
            {
                if (HudManager.Instance?.ChatPopup == null) return;
                if (string.Equals(mode, "Crewmate", StringComparison.OrdinalIgnoreCase))
                    SystemChat.Show("Your target died — you are now a plain Crewmate.");
                else
                    SystemChat.Show("Your target died — you became the Jester! Get yourself voted out to win.");
            }
            catch { }
        }

        [ManactorRpc(AssignRpc)]
        private static void OnAssignRpc(byte senderId, byte executionerId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var executioner = FindPlayer(executionerId);
            var target = FindPlayer(targetId);
            if (executioner != null && target != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(executioner, ExecutionerRole.Id);
                TargetOf[executionerId] = targetId;
                return;
            }
            _pendingExecutionerId = executionerId;
            _pendingTargetId = targetId;
            _pendingRetries = 0;
        }

        [ManactorRpc(WinRpc)]
        private static void OnWinRpc(byte senderId, byte executionerId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _executionerWon = true;
            _resultScreenRetries = 0;
            TargetOf[executionerId] = targetId;
        }

        public static bool HasPendingWin => _executionerWon;

        public static bool ApplyResultTitle(EndGameManager manager)
        {
            if (!_executionerWon || manager == null) return false;
            var applied = false;
            if (manager.WinText != null)
            {
                manager.WinText.text = "Executioner Wins";
                manager.WinText.color = new Color(0.45f, 0.9f, 0.85f, 1f);
                applied = true;
            }
            if (manager.AltWinText != null)
            {
                manager.AltWinText.text = "Executioner Wins";
                manager.AltWinText.color = new Color(0.45f, 0.9f, 0.85f, 1f);
                applied = true;
            }
            return applied;
        }

        public static void ConsumePendingWin() => _executionerWon = false;

        public static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    internal static class ExileController_Begin_ExecutionerPatch
    {
        private static void Prefix(ExileController __instance, GameData.PlayerInfo exiled, bool tie)
        {
            if (__instance == null || exiled == null || tie) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.PlayerId != exiled.PlayerId) continue;
                if (!ExecutionerSystem.IsActiveExecutioner(player)) return;
                var text = exiled.PlayerName + " was the Executioner.";
                if (__instance.Text != null) __instance.Text.Text = text;
                // completeString is protected in the 2026.8.9 interop.
                GameReflection.SetCompleteString(__instance, text);
                return;
            }
        }
    }

    // Exile reveal text is re-applied every frame by Core/ExileTextFix (polling
    // ExileController.Instance). The old ExileController_Animate_ExecutionerPatch
    // targeted the compiler-generated coroutine type ExileController.
    // _Animate_d__17.MoveNext, which the 2026.8.9 interop hides as a private
    // nested type — never reference compiler-generated coroutine types (see
    // PORTING.md).

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_ExecutionerPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance == PlayerControl.LocalPlayer) ExecutionerSystem.Tick();
        }
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_ExecutionerPatch
    {
        // Deferred to the FixedUpdate Tick loop — assigning synchronously inside
        // the game's native AssignRolesForTeam pass (ShipStatus.Start) touches
        // freshly-spawned players mid-transition and can fault the CLR (segfault).
        // Tick() runs the same pool a few frames later once the scene settles.
        private static void Postfix(RoleTeamTypes type, int max) { }
    }

    // String "Update" form: the local GameLibs interop missed EndGameManager.Update
    // while the runtime-generated interop has it (see PORTING.md / Jester patch).
    [HarmonyPatch(typeof(EndGameManager), "Update")]
    internal static class EndGameManager_Update_ExecutionerPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!ExecutionerSystem.HasPendingWin || __instance == null) return;
            if (ExecutionerSystem.ApplyResultTitle(__instance)) ExecutionerSystem.ConsumePendingWin();
        }
    }

    // String "SetEverythingUp" form: the method is private in the 8.9 game, so
    // the interop omits it and nameof would fail to compile. Harmony resolves
    // the string via reflection at runtime (same rationale as "Update" above).
    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    internal static class EndGameManager_SetEverythingUp_ExecutionerPatch
    {
        private static void Postfix(EndGameManager __instance)
        {
            if (!ExecutionerSystem.HasPendingWin || __instance == null) return;
            try
            {
                if (ExecutionerSystem.ApplyResultTitle(__instance)) ExecutionerSystem.ConsumePendingWin();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Executioner end-screen patch: " + e);
            }
        }
    }
}

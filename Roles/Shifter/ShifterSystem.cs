using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Shifter
{
    /// <summary>
    /// Shifter gameplay logic (ported from Town-Of-Us' Shifter.cs).
    ///
    /// A Neutral with no win condition: the Shift button swaps roles and tasks
    /// with the nearest non-Impostor. Shifting an Impostor fails and kills the
    /// Shifter. Swapping with a custom-role player gives the Shifter that role
    /// while the target becomes a baseline Crewmate; swapping with a plain
    /// Crewmate exchanges the two players' roles and tasks.
    ///
    /// The host is authoritative: it resolves the swap, reassigns roles through
    /// RoleManager (virtual roles), swaps task type ids through GameData.RpcSetTasks
    /// (the game's own broadcast), and mirrors the role reassignment to every
    /// client over Reactor so all registries agree.
    /// </summary>
    internal static class ShifterSystem
    {
        private const string SwapRpc = "townofus.ShifterSwap";
        private const string RequestShiftRpc = "townofus.ShifterRequestShift";
        private const string SuicideRpc = "townofus.ShifterSuicide";
        private const int MaxRetries = 300;

        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        /// <summary>Players who shifted away and are no longer the Shifter (mirrors
        /// the Executioner's Converted set: AssignRole(..., "Crewmate") leaves the
        /// virtual registry entry lingering, so presentation/abilities must gate on
        /// this set instead of the raw registry).</summary>
        private static readonly HashSet<byte> SwappedAway = new();
        private static bool _poolAssignmentDone;
        private static int _poolAssignmentAttempts;
        private static byte? _pendingPlayerId;
        private static int _pendingRetries;

        public static bool IsShifter(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, ShifterRole.Id) &&
            !SwappedAway.Contains(player.PlayerId);

        internal static bool CanShiftNow(PlayerControl shifter)
        {
            if (!IsShifter(shifter) || shifter.Data == null || shifter.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(shifter.PlayerId) &&
                   ClosestPlayerFinder.GetClosestTarget(shifter, out _);
        }

        public static void TryShift(PlayerControl shifter)
        {
            var client = AmongUsClient.Instance;
            if (client == null || shifter == null || shifter.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestShiftRpc, shifter.PlayerId);
                return;
            }
            if (!CanShiftNow(shifter)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(shifter, out var target)) return;
            if (target == shifter || target.Data == null) return;

            Cooldowns[shifter.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.ShiftCooldown, 30f));

            // Shifting an Impostor fails and kills the Shifter (Town-Of-Us ShiftKill).
            if (target.Data.myRole != null && target.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor)
            {
                KillManager.Kill(shifter, shifter);
                TownOfUsRpcMux.Send(SuicideRpc, shifter.PlayerId);
                Local("You tried to shift an Impostor and died.");
                return;
            }

            var targetRoleId = FindAssignedRoleId(target);
            var shifterRoleId = FindAssignedRoleId(shifter) ?? string.Empty;

            // Task swap first: both players exchange their task type ids through
            // the game's own broadcast RPC (host only).
            var shifterTasks = GetTaskTypeIds(shifter);
            var targetTasks = GetTaskTypeIds(target);
            try
            {
                if (GameData.Instance != null)
                {
                    GameData.Instance.RpcSetTasks(shifter.PlayerId, targetTasks);
                    GameData.Instance.RpcSetTasks(target.PlayerId, shifterTasks);
                }
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Shifter tasks: " + e.Message);
            }

            // The Shifter is no longer the Shifter after a successful swap. Gate
            // IsShifter on SwappedAway because AssignRole leaves the virtual
            // registry entry lingering (same pattern as Executioner.Converted).
            SwappedAway.Add(shifter.PlayerId);

            if (targetRoleId == null)
            {
                // Plain Crewmate: the target becomes the Shifter, the Shifter
                // becomes a baseline Crewmate (roles exchanged).
                if (RoleManager.Instance != null)
                {
                    RoleManager.Instance.AssignRole(target, ShifterRole.Id);
                    RoleManager.Instance.AssignRole(shifter, "Crewmate");
                }
            }
            else
            {
                // Custom role held by the target: the Shifter takes it, the
                // target is reduced to a baseline Crewmate.
                if (RoleManager.Instance != null)
                {
                    RoleManager.Instance.AssignRole(shifter, targetRoleId);
                    RoleManager.Instance.AssignRole(target, "Crewmate");
                }
            }

            TownOfUsRpcMux.Send(SwapRpc, shifter.PlayerId, target.PlayerId, targetRoleId ?? string.Empty);
            Local("You shifted with " + target.Data.PlayerName + ".");
        }

        // ── Round lifecycle / pool ───────────────────────────────────────────
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();

        public static void Reset()
        {
            Cooldowns.Clear();
            SwappedAway.Clear();
            _poolAssignmentDone = false;
            _poolAssignmentAttempts = 0;
            _pendingPlayerId = null;
            _pendingRetries = 0;
        }

        public static void OnGameEnded(GameEndedEventArgs _) { }

        public static void Tick()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            if (!_poolAssignmentDone && _poolAssignmentAttempts < MaxRetries)
            {
                _poolAssignmentAttempts++;
                TryAssignFromPool(RoleTeamTypes.Crewmate);
            }

            if (!_pendingPlayerId.HasValue) return;
            var player = PlayerUtils.FindById(_pendingPlayerId.Value);
            if (player != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(player, ShifterRole.Id);
                if (RoleRegistry.IsAssigned(player, ShifterRole.Id))
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
            var requested = RoleConfig.Count(RoleConfig.ShifterCount);
            if (requested <= 0)
            {
                _poolAssignmentDone = true;
                return;
            }

            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && RoleRegistry.IsAssigned(player, ShifterRole.Id)) return;

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
                if (UnityEngine.Random.Range(0f, 100f) >= RoleConfig.Chance(RoleConfig.ShifterChance)) break;
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var target = candidates[index];
                candidates.RemoveAt(index);
                RoleManager.Instance.AssignRole(target, ShifterRole.Id);
                if (!RoleRegistry.IsAssigned(target, ShifterRole.Id)) continue;
                assigned++;
                TownOfUsRpcMux.Send(AssignRpc, target.PlayerId);
            }

            _poolAssignmentDone = true;
        }

        private const string AssignRpc = "townofus.ShifterAssign";

        private static bool IsClaimedByCustomRole(PlayerControl player) =>
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
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Executioner.ExecutionerRole.Id) ||
            RoleRegistry.IsAssigned(player, TownOfUs.ManuAPI.Roles.Arsonist.ArsonistRole.Id);

        // ── RPCs ─────────────────────────────────────────────────────────────
        [ReactorRpc(RequestShiftRpc)]
        private static void OnRequestShift(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryShift(player);
                    return;
                }
            }
        }

        [ReactorRpc(SwapRpc)]
        private static void OnSwap(byte senderId, byte shifterId, byte targetId, string targetRoleId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var shifter = PlayerUtils.FindById(shifterId);
            var target = PlayerUtils.FindById(targetId);
            if (shifter == null || target == null || RoleManager.Instance == null) return;

            SwappedAway.Add(shifterId);
            if (string.IsNullOrEmpty(targetRoleId))
            {
                RoleManager.Instance.AssignRole(target, ShifterRole.Id);
                RoleManager.Instance.AssignRole(shifter, "Crewmate");
            }
            else
            {
                RoleManager.Instance.AssignRole(shifter, targetRoleId);
                RoleManager.Instance.AssignRole(target, "Crewmate");
            }
        }

        [ReactorRpc(SuicideRpc)]
        private static void OnSuicide(byte senderId, byte shifterId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            // The host's KillManager.Kill already broadcast the networked kill;
            // clients only need the notification.
            var shifter = PlayerUtils.FindById(shifterId);
            if (shifter != null && shifter.PlayerId == PlayerControl.LocalPlayer?.PlayerId)
                Local("You tried to shift an Impostor and died.");
        }

        [ReactorRpc(AssignRpc)]
        private static void OnAssignRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var target = PlayerUtils.FindById(playerId);
            if (target != null && RoleManager.Instance != null)
            {
                RoleManager.Instance.AssignRole(target, ShifterRole.Id);
                return;
            }
            _pendingPlayerId = playerId;
            _pendingRetries = 0;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        /// <summary>Returns the assigned custom-role id of the player, or null for a baseline Crewmate.</summary>
        private static string FindAssignedRoleId(PlayerControl player)
        {
            if (player == null) return null;
            if (RoleRegistry.IsAssigned(player, "townofus.Sheriff")) return "townofus.Sheriff";
            if (RoleRegistry.IsAssigned(player, "townofus.Engineer")) return "townofus.Engineer";
            if (RoleRegistry.IsAssigned(player, "townofus.Jester")) return "townofus.Jester";
            if (RoleRegistry.IsAssigned(player, "townofus.Medic")) return "townofus.Medic";
            if (RoleRegistry.IsAssigned(player, "townofus.Seer")) return "townofus.Seer";
            if (RoleRegistry.IsAssigned(player, "townofus.Vigilante")) return "townofus.Vigilante";
            if (RoleRegistry.IsAssigned(player, "townofus.Assassin")) return "townofus.Assassin";
            if (RoleRegistry.IsAssigned(player, "townofus.Janitor")) return "townofus.Janitor";
            if (RoleRegistry.IsAssigned(player, "townofus.Altruist")) return "townofus.Altruist";
            if (RoleRegistry.IsAssigned(player, "townofus.Mayor")) return "townofus.Mayor";
            if (RoleRegistry.IsAssigned(player, "townofus.Executioner")) return "townofus.Executioner";
            if (RoleRegistry.IsAssigned(player, "townofus.Arsonist")) return "townofus.Arsonist";
            if (RoleRegistry.IsAssigned(player, "townofus.Swapper")) return "townofus.Swapper";
            if (RoleRegistry.IsAssigned(player, "townofus.Morphling")) return "townofus.Morphling";
            if (RoleRegistry.IsAssigned(player, "townofus.Spy")) return "townofus.Spy";
            if (RoleRegistry.IsAssigned(player, "townofus.Camouflager")) return "townofus.Camouflager";
            if (RoleRegistry.IsAssigned(player, "townofus.Swooper")) return "townofus.Swooper";
            if (RoleRegistry.IsAssigned(player, "townofus.Underdog")) return "townofus.Underdog";
            if (RoleRegistry.IsAssigned(player, "townofus.Undertaker")) return "townofus.Undertaker";
            if (RoleRegistry.IsAssigned(player, "townofus.Investigator")) return "townofus.Investigator";
            if (RoleRegistry.IsAssigned(player, "townofus.TimeLord")) return "townofus.TimeLord";
            if (RoleRegistry.IsAssigned(player, "townofus.Snitch")) return "townofus.Snitch";
            if (RoleRegistry.IsAssigned(player, "townofus.Phantom")) return "townofus.Phantom";
            if (RoleRegistry.IsAssigned(player, "townofus.Glitch")) return "townofus.Glitch";
            return null;
        }

        /// <summary>Current task type ids of a player (indexes into ShipStatus.TaskTypes).</summary>
        private static byte[] GetTaskTypeIds(PlayerControl player)
        {
            var ids = new List<byte>();
            if (player?.Data?.Tasks != null)
            {
                for (int i = 0; i < player.Data.Tasks.Count; i++)
                {
                    var task = player.Data.Tasks.get_Item(i);
                    if (task == null) continue;
                    // TaskInfo.Id is the task-type index into ShipStatus.TaskTypes
                    // (the interop TaskInfo exposes the native field Id, not TaskType).
                    try { ids.Add((byte)task.Id); } catch { }
                }
            }
            return ids.ToArray();
        }

        private static PlayerControl PlayerUtils.FindById(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        private static DateTime GetCooldown(byte shifterId) =>
            Cooldowns.TryGetValue(shifterId, out var value) ? value : DateTime.MinValue;

        private static void Local(string message)
        {
            try
            {
                SystemChat.Show(message);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_ShifterPatch
    {
        // Deferred to the FixedUpdate Tick loop — assigning synchronously inside
        // the game's native AssignRolesForTeam pass (ShipStatus.Start) touches
        // freshly-spawned players mid-transition and can fault the CLR (segfault).
        // Tick() runs the same pool a few frames later once the scene settles.
        private static void Postfix(RoleTeamTypes type, int max) { }
    }
}

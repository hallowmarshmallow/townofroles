using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Sheriff
{
    /// <summary>
    /// Sheriff gameplay logic.
    ///
    /// Ported from Town-Of-Us' Kill.cs / CantReport.cs:
    ///  - the kill itself goes through KillManager (host-authoritative, networked),
    ///  - a companion Manactor RPC keeps every client's "who killed whom" table in sync,
    ///    which is what the self-report suppression needs on non-host clients.
    /// </summary>
    internal static class SheriffSystem
    {
        private const string KilledRpc = "townofus.SheriffKilled";
        private const string RequestShootRpc = "townofus.SheriffRequestShoot";

        // Cross-client "who killed whom" log (ported from Town-Of-Us' Murder.KilledPlayers),
        // synced via KilledRpc. Currently only KilledBySheriff drives the self-report
        // suppression; the full log is retained for future roles (Janitor clean-ups,
        // Altruist revives, Medic shields) that need the killer lookup.
        private static readonly HashSet<(byte Victim, byte Killer)> KilledPlayers = new();
        private static readonly HashSet<byte> KilledBySheriff = new();

        public static bool IsSheriff(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, SheriffRole.Id);

        /// <summary>True when the Sheriff has a shootable target in range.</summary>
        public static bool HasTarget(PlayerControl sheriff) =>
            sheriff != null && sheriff.Data != null && !sheriff.Data.IsDead &&
            ClosestPlayerFinder.GetClosestTarget(sheriff, out _);

        /// <summary>Fires the Sheriff's gun at the closest target in range.</summary>
        public static void TryShoot(PlayerControl sheriff)
        {
            if (sheriff == null || sheriff.Data == null || sheriff.Data.IsDead) return;
            var client = AmongUsClient.Instance;
            if (client == null) return;
            // Resolve the target and the shot on the host. Sending a raw custom
            // kill from a client bypassed the role's target/team validation.
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestShootRpc, sheriff.PlayerId);
                return;
            }
            if (!ClosestPlayerFinder.GetClosestTarget(sheriff, out var target)) return;

            var team = target.Data?.myRole?.RoleTeamType;
            if (team == RoleTeamTypes.Impostor || (team == RoleTeamTypes.Neutral && IsKillableNeutral(target)))
            {
                PerformKill(sheriff, target);
                return;
            }

            // Non-enemy target. A Crewmate dies too when KillOther is on; a neutral
            // the Sheriff is not allowed to kill never dies — it is a missed shot,
            // so only the Sheriff dies (ported Town-Of-Us semantics).
            if (team != RoleTeamTypes.Neutral && Options.KillOther) PerformKill(sheriff, target);
            PerformKill(sheriff, sheriff);
        }

        /// <summary>True when the Sheriff may shoot this neutral (gated by Options.KillsNeutrals).</summary>
        private static bool IsKillableNeutral(PlayerControl target)
        {
            if (!Options.KillsNeutrals) return false;
            if (RoleRegistry.IsAssigned(target, "townofus.Jester")) return true;
            if (RoleRegistry.IsAssigned(target, "townofus.Executioner")) return true;
            return false;
        }

        private static void PerformKill(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null || killer.Data == null || target.Data == null) return;

            var victim = target.Data.PlayerId;
            var murderer = killer.Data.PlayerId;

            // KillManager performs the networked kill (dead body, animation, sound...).
            // NOTE: the suicide leg calls KillManager.Kill(sheriff, sheriff) — if a lobby
            // test shows the host pipeline misbehaving with killer == target, switch the
            // self-kill to a KillRequest with CreateDeadBody/ShowKillAnimation = false.
            KillManager.Kill(killer, target);

            // Record locally and tell every client (including ourselves — dedup makes
            // the local add + RPC add idempotent).
            Record(victim, murderer);
            TownOfUsRpcMux.Send(KilledRpc, victim, murderer);
        }

        [ManactorRpc(KilledRpc)]
        private static void OnKilled(byte senderId, byte victim, byte murderer)
        {
            var client = AmongUsClient.Instance;
            if (client == null || (!client.AmHost && senderId != client.HostId)) return;
            Record(victim, murderer);
        }

        [ManactorRpc(RequestShootRpc)]
        private static void OnRequestShoot(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId) TryShoot(player);
                return;
            }
        }

        private static void Record(byte victim, byte murderer)
        {
            KilledPlayers.Add((victim, murderer));
            if (murderer != victim) KilledBySheriff.Add(victim);
        }

        /// <summary>
        /// GameEvents.BeforeReport hook: the Sheriff cannot report bodies they shot
        /// themselves (Town-Of-Us' CantReport.cs, gated by Options.BodyReport).
        /// </summary>
        public static void OnBeforeReport(ReportEventArgs args)
        {
            if (Options.BodyReport) return;
            if (args.IsEmergencyMeeting || args.Body == null || args.Reporter == null) return;
            if (!IsSheriff(args.Reporter)) return;
            // On non-host clients this depends on the kill-record RPC having arrived
            // before the player presses Report; the host is always authoritative here.
            if (KilledBySheriff.Contains(args.Body.PlayerId)) args.Cancelled = true;
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();

        public static void OnGameEnded(GameEndedEventArgs _) => Reset();

        public static void Reset()
        {
            KilledPlayers.Clear();
            KilledBySheriff.Clear();
        }
    }
}

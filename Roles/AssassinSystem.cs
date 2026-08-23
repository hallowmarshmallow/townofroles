using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Assassin
{
    internal static class AssassinSystem
    {
        private const string RequestGuessRpc = "townofus.AssassinRequestGuess";
        private const string GuessResultRpc = "townofus.AssassinGuessResult";
        private static readonly HashSet<byte> _guessedThisMeeting = new();
        internal static readonly string[] GuessableRoles =
        {
            "Sheriff", "Engineer", "Medic", "Seer", "Vigilante", "Altruist", "Mayor", "Swapper", "Spy",
            "Assassin", "Janitor", "Morphling", "Camouflager", "Swooper", "Underdog", "Undertaker",
            "Investigator", "Time Lord", "Snitch", "Phantom",
            "Shifter", "The Glitch", "Miner",
            "Jester", "Executioner", "Arsonist", "Crewmate", "Neutral"
        };

        public static bool IsAssassin(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, AssassinRole.Id);

        public static string AvailableRoles => string.Join(", ", GuessableRoles);

        public static void Reset()
        {
            _guessedThisMeeting.Clear();
        }

        // Assignment is performed by ManuAPI's RoleRegistry.AssignForTeam. This role
        // system intentionally does not maintain a second pool allocator.

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => _guessedThisMeeting.Clear();
        public static void OnMeetingEnded(MeetingEventArgs _) => _guessedThisMeeting.Clear();

        public static bool TryHandleGuess(PlayerControl sender, string[] args)
        {
            if (!IsAssassin(sender)) return false;
            if (MeetingHud.Instance == null)
            {
                Local("Assassin guesses can only be used during a meeting.");
                return true;
            }
            if (args == null || args.Length < 2)
            {
                Local("Usage: /guess <player name or id> <role>");
                Local("Guessable roles: " + AvailableRoles);
                return true;
            }

            var target = ResolveTarget(args.Take(args.Length - 1).ToArray());
            var guess = CanonicalizeRole(args[args.Length - 1]);
            if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
            {
                Local("That player is not a valid guess target.");
                return true;
            }
            if (target == sender || (target.Data.myRole != null && target.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor))
            {
                Local("You cannot guess yourself or another Impostor.");
                return true;
            }
            if (guess == null)
            {
                Local("Unknown role. Guessable roles: " + AvailableRoles);
                return true;
            }
            if (_guessedThisMeeting.Contains(target.PlayerId))
            {
                Local("That player has already been guessed this meeting.");
                return true;
            }

            var client = AmongUsClient.Instance;
            if (client == null) return true;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestGuessRpc, sender.PlayerId, target.PlayerId, guess);
                Local("Guess sent to the host.");
                return true;
            }

            ResolveGuess(sender, target, guess);
            return true;
        }

        public static bool TryGuessTarget(PlayerControl assassin, PlayerControl target, string guess)
        {
            if (!IsAssassin(assassin) || assassin.Data.IsDead || MeetingHud.Instance == null || target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected) return false;
            if (target == assassin || (target.Data.myRole != null && target.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor)) return false;
            var canonical = CanonicalizeRole(guess);
            if (canonical == null || _guessedThisMeeting.Contains(target.PlayerId)) return false;
            var client = AmongUsClient.Instance;
            if (client == null) return false;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestGuessRpc, assassin.PlayerId, target.PlayerId, canonical);
                return true;
            }
            ResolveGuess(assassin, target, canonical);
            return true;
        }

        public static bool IsEligibleTarget(PlayerControl assassin, PlayerControl target)
        {
            return IsAssassin(assassin) && target != null && target != assassin && target.Data != null &&
                   !target.Data.IsDead && !target.Data.Disconnected &&
                   target.Data.myRole != null && target.Data.myRole.RoleTeamType != RoleTeamTypes.Impostor;
        }

        private static void ResolveGuess(PlayerControl assassin, PlayerControl target, string guess)
        {
            if (assassin == null || assassin.Data == null || assassin.Data.IsDead || target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected || !IsAssassin(assassin)) return;
            // state is private in the 2026.8.9 interop — reflection adapter.
            var meeting = MeetingHud.Instance;
            var voteState = GameReflection.GetMeetingState(meeting);
            if (meeting == null || voteState == MeetingHud.VoteStates.Discussion || voteState == MeetingHud.VoteStates.Results ||
                target.Data.myRole == null || target.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor ||
                _guessedThisMeeting.Contains(target.PlayerId) ||
                (!AssassinSettingsSync.ActiveMultiKill && _guessedThisMeeting.Contains(assassin.PlayerId))) return;
            _guessedThisMeeting.Add(target.PlayerId);

            var actual = GetRoleName(target);
            bool correct = string.Equals(actual, guess, StringComparison.OrdinalIgnoreCase);
            if (correct && !AssassinSettingsSync.ActiveMultiKill) _guessedThisMeeting.Add(assassin.PlayerId);
            var victim = correct ? target : assassin;
            KillManager.Kill(assassin, victim);
            TownOfUsRpcMux.Send(GuessResultRpc, assassin.PlayerId, victim.PlayerId, correct, (byte)target.PlayerId);
            Local(correct
                ? $"Assassin guessed {target.Data.PlayerName} correctly: {actual}."
                : $"Wrong guess. The Assassin guessed {guess}; actual role was {actual}.");
        }

        [ReactorRpc(RequestGuessRpc)]
        private static void OnRequestGuess(byte senderId, byte assassinId, byte targetId, string guess)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != GetClientId(assassinId)) return;
            var assassin = PlayerUtils.FindById(assassinId);
            var target = PlayerUtils.FindById(targetId);
            var canonical = CanonicalizeRole(guess);
            if (assassin != null && target != null && canonical != null)
                TryGuessTarget(assassin, target, canonical);
        }

        [ReactorRpc(GuessResultRpc)]
        private static void OnGuessResult(byte senderId, byte assassinId, byte victimId, bool correct, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _guessedThisMeeting.Add(targetId);
        }

        private static string GetRoleName(PlayerControl player)
        {
            if (RoleRegistry.IsAssigned(player, "townofus.Sheriff")) return "Sheriff";
            if (RoleRegistry.IsAssigned(player, "townofus.Engineer")) return "Engineer";
            if (RoleRegistry.IsAssigned(player, "townofus.Jester")) return "Jester";
            if (RoleRegistry.IsAssigned(player, "townofus.Medic")) return "Medic";
            if (RoleRegistry.IsAssigned(player, "townofus.Seer")) return "Seer";
            if (RoleRegistry.IsAssigned(player, "townofus.Vigilante")) return "Vigilante";
            if (RoleRegistry.IsAssigned(player, "townofus.Assassin")) return "Assassin";
            if (RoleRegistry.IsAssigned(player, "townofus.Janitor")) return "Janitor";
            if (RoleRegistry.IsAssigned(player, "townofus.Altruist")) return "Altruist";
            if (RoleRegistry.IsAssigned(player, "townofus.Mayor")) return "Mayor";
            if (RoleRegistry.IsAssigned(player, "townofus.Executioner")) return "Executioner";
            if (RoleRegistry.IsAssigned(player, "townofus.Arsonist")) return "Arsonist";
            if (RoleRegistry.IsAssigned(player, "townofus.Swapper")) return "Swapper";
            if (RoleRegistry.IsAssigned(player, "townofus.Morphling")) return "Morphling";
            if (RoleRegistry.IsAssigned(player, "townofus.Spy")) return "Spy";
            if (RoleRegistry.IsAssigned(player, "townofus.Camouflager")) return "Camouflager";
            if (RoleRegistry.IsAssigned(player, "townofus.Swooper")) return "Swooper";
            if (RoleRegistry.IsAssigned(player, "townofus.Underdog")) return "Underdog";
            if (RoleRegistry.IsAssigned(player, "townofus.Undertaker")) return "Undertaker";
            if (RoleRegistry.IsAssigned(player, "townofus.Investigator")) return "Investigator";
            if (RoleRegistry.IsAssigned(player, "townofus.TimeLord")) return "Time Lord";
            if (RoleRegistry.IsAssigned(player, "townofus.Snitch")) return "Snitch";
            if (RoleRegistry.IsAssigned(player, "townofus.Phantom")) return "Phantom";
            if (RoleRegistry.IsAssigned(player, "townofus.Shifter")) return "Shifter";
            if (RoleRegistry.IsAssigned(player, "townofus.Glitch")) return "The Glitch";
            if (RoleRegistry.IsAssigned(player, "townofus.Miner")) return "Miner";
            if (player?.Data?.myRole == null) return "Unknown";
            if (player.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor) return "Impostor";
            if (player.Data.myRole.RoleTeamType == RoleTeamTypes.Neutral) return "Neutral";
            return "Crewmate";
        }

        private static string CanonicalizeRole(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            // Space-insensitive so "Time Lord" matches "t-lord", "timelord" etc.
            var compact = string.Concat(value.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            foreach (var role in GuessableRoles)
            {
                var roleCompact = string.Concat(role.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
                if (roleCompact == compact) return role;
            }
            return null;
        }

        private static PlayerControl ResolveTarget(string[] args)
        {
            if (args == null || args.Length == 0) return null;
            var query = string.Join(" ", args).Trim();
            if (byte.TryParse(query, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) return PlayerUtils.FindById(id);
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player?.Data != null && string.Equals(player.Data.PlayerName, query, StringComparison.OrdinalIgnoreCase)) return player;
            return null;
        }


        private static byte GetClientId(byte playerId)
        {
            var player = PlayerUtils.FindById(playerId);
            return player == null || player.GetClient() == null ? (byte)255 : (byte)player.GetClient().Id;
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
}

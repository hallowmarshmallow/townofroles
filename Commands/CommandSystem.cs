using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;
using TownOfUs.ManuAPI.Roles.Assassin;
using TownOfUs.ManuAPI.Roles.Arsonist;
using TownOfUs.ManuAPI.Roles.Modifiers;

namespace TownOfUs.ManuAPI.Commands
{
    internal static class CommandSystem
    {
        private const string ReviveRpc = "townofus.Revive";
        private const string RequestCustomCommandRpc = "townofus.RequestCustomCommand";
        private const string CustomCommandRpc = "townofus.CustomCommand";
        private const string SystemMessageRpc = "townofus.SystemMessage";
        public static bool TryHandle(PlayerControl sender, string text)
        {
            if (sender == null || string.IsNullOrWhiteSpace(text)) return false;
            if (!text.StartsWith("/", StringComparison.Ordinal)) return false;

            var parts = text.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var command = parts[0].Substring(1).ToLowerInvariant();
            var args = parts.Skip(1).ToArray();
            switch (command)
            {
                case "forcestart":
                    ForceStart(sender);
                    return true;
                case "nickname":
                case "nick":
                    Nickname(sender, args);
                    return true;
                case "gradient":
                    Gradient(sender, args);
                    return true;
                case "rainbow":
                    Rainbow(sender, args);
                    return true;
                case "color":
                    Color(sender, args);
                    return true;
                case "code":
                case "lobbycode":
                    LobbyCode(sender, args);
                    return true;
                case "system":
                case "systemmessage":
                    SystemMessage(sender, args);
                    return true;
                case "tpin":
                    Teleport(sender, args, true);
                    return true;
                case "tpout":
                    Teleport(sender, args, false);
                    return true;
                case "nogameend":
                case "noend":
                    NoGameEnd(sender, args);
                    return true;
                case "setrole":
                case "role":
                    SetRole(sender, args);
                    return true;
                case "revive":
                case "resurrect":
                    Revive(sender, args);
                    return true;
                case "guess":
                case "assassinate":
                    return AssassinGuess(sender, args);
                case "ignite":
                    return ArsonistIgnite(sender, args);
                case "meeting":
                    return ButtonBarryMeeting(sender, args);
                case "help":
                case "commands":
                    Help(sender);
                    return true;
                default:
                    return TryHandleCustomCommand(sender, command, args);
            }
        }

        private static void Help(PlayerControl sender)
        {
            var lines = new List<string>
            {
                "Town of Roles commands:",
                "/forcestart — start the game now (host)",
                "/nickname <name> — change your name",
                "/gradient [on|off] — hallowmarsh gradient",
                "/rainbow [on|off] — rainbow skin",
                "/color gradient|rainbow [on|off]",
                "/code <word>|off — custom lobby code (host)",
                "/system <msg> — host system broadcast",
                "/tpin|/tpout [player] — dropship teleport (host)",
                "/nogameend [on|off] — disable game end (host)",
                "/setrole [player] <role> — assign a custom role (host)",
                "/revive [player] — revive a dead player (host)",
                "/meeting — call an emergency meeting (Button Barry)",
                "/guess <player> <role> — Assassin guess",
                "/ignite — Arsonist ignite",
                "/help — this list",
            };
            var table = GetCustomCommandTable();
            if (table != null && table.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Custom commands:");
                foreach (var pair in table)
                    lines.Add("/" + pair.Key + " — " + pair.Value.Replace("{player}", "you").Replace("{args}", "…"));
            }
            Local(string.Join("\n", lines));
        }

        private static bool TryHandleCustomCommand(PlayerControl sender, string command, string[] args)
        {
            var table = GetCustomCommandTable();
            if (table == null || !table.TryGetValue(command, out var template)) return false;

            if (CommandConfig.CustomCommandHostOnly?.Value == true && !IsHost())
            {
                Local("Only the host can use /" + command + ".");
                return true;
            }

            // Cheap flood guard: at most one custom command every 400 ms.
            var now = DateTime.UtcNow;
            if ((now - _lastCustomCommandRequest).TotalMilliseconds < 400) return true;
            _lastCustomCommandRequest = now;

            try
            {
                if (IsHost())
                {
                    // The host broadcasts to the lobby and also displays the message
                    // directly, so the host always sees it regardless of whether the
                    // local RPC handler fires (the handler skips on the host).
                    var message = FormatCustomCommand(template, sender, args);
                    TownOfUsRpcMux.Send(CustomCommandRpc, message);
                    SystemChat.Show(message);
                }
                else
                {
                    // Clients ask the host to run the command so only the host's
                    // config decides what the lobby sees.
                    TownOfUsRpcMux.Send(RequestCustomCommandRpc, command, string.Join(" ", args));
                }
            }
            catch (Exception e)
            {
                Local("Custom command failed: " + e.Message);
            }
            return true;
        }

        [ManactorRpc(RequestCustomCommandRpc)]
        private static void OnRequestCustomCommandRpc(byte senderId, string command, string args)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            var table = GetCustomCommandTable();
            if (table == null || !table.TryGetValue(command ?? string.Empty, out var template)) return;

            var sender = ResolveTarget(new[] { senderId.ToString(CultureInfo.InvariantCulture) });
            if (sender == null || sender.Data == null || sender.Data.Disconnected) return;

            var split = string.IsNullOrEmpty(args)
                ? new string[0]
                : args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var message = FormatCustomCommand(template, sender, split);
            TownOfUsRpcMux.Send(CustomCommandRpc, message);
        }

        [ManactorRpc(CustomCommandRpc)]
        private static void OnCustomCommandRpc(byte senderId, string message)
        {
            // The host already displayed the message when it broadcast it; only
            // remote clients display here. Exactly one display per client.
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost) return;
            if (string.IsNullOrWhiteSpace(message)) return;
            SystemChat.Show(message);
        }

        private static Dictionary<string, string> GetCustomCommandTable()
        {
            var raw = CommandConfig.CustomCommands?.Value;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entries = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var separator = entry.IndexOf("=>", StringComparison.Ordinal);
                if (separator <= 0) continue;
                var name = entry.Substring(0, separator).Trim().ToLowerInvariant();
                if (name.Length == 0) continue;
                var text = entry.Substring(separator + 2).Trim();
                table[name] = text;
            }
            return table.Count == 0 ? null : table;
        }

        private static string FormatCustomCommand(string template, PlayerControl sender, string[] args)
        {
            var text = template ?? string.Empty;
            text = text.Replace("{player}", DisplayName(sender));
            text = text.Replace("{args}", string.Join(" ", args ?? new string[0]));
            if (text.Length > 200) text = text.Substring(0, 200);
            return text;
        }

        public static void TickLocalEffects()
        {
            VisualEffects.Tick();
            TryApplyPendingRevive();
        }

        private static void TryApplyPendingRevive()
        {
            var pending = CommandState.TakePendingRevive();
            if (!pending.HasValue) return;
            var target = ResolveTarget(new[] { pending.Value.ToString(CultureInfo.InvariantCulture) });
            if (target == null || target.Data == null)
            {
                CommandState.QueueRevive(pending.Value);
                return;
            }
            if (target.Data.IsDead) target.Revive();
        }

        private static void NoGameEnd(PlayerControl sender, string[] args)
        {
            if (!RequireHost(sender)) return;
            var enabled = ParseToggle(args, true);
            if (enabled == null)
            {
                Local("Usage: /nogameend [on|off]");
                return;
            }

            CommandState.SetNoGameEnd(enabled.Value);
            Local($"No-game-end mode {(enabled.Value ? "enabled" : "disabled")}.");
        }

        private static void SetRole(PlayerControl sender, string[] args)
        {
            if (!RequireHost(sender)) return;
            if (CommandConfig.AllowSetRole?.Value != true)
            {
                Local("/setrole is disabled in the Town Of Us config.");
                return;
            }
            if (args.Length < 1)
            {
                Local("Usage: /setrole <role> or /setrole <player> <role>");
                return;
            }

            PlayerControl target = sender;
            string roleName;
            if (args.Length == 1)
            {
                roleName = args[0];
            }
            else
            {
                // The final token is the role; all preceding tokens form the
                // player name, so names containing spaces remain addressable.
                target = ResolveTarget(args.Take(args.Length - 1).ToArray());
                roleName = args[args.Length - 1];
            }

            if (target == null || target.Data == null || target.Data.Disconnected || target.Data.IsDead)
            {
                Local("Target player is unavailable or dead.");
                return;
            }

            var canonicalRole = ResolveRoleName(roleName);
            if (canonicalRole == null)
            {
                Local("Unknown role. Available custom roles: Sheriff, Engineer, Jester, Medic, Seer, Vigilante, Assassin.");
                return;
            }

            var manager = RoleManager.Instance;
            if (manager == null)
            {
                Warn("Role manager is not ready.");
                return;
            }

            manager.AssignRole(target, canonicalRole);
            Local($"Assigned {canonicalRole} to {DisplayName(target)}.");
        }

        private static bool AssassinGuess(PlayerControl sender, string[] args)
        {
            if (RoleConfig.Assassin?.Value != true || !AssassinSystem.IsAssassin(sender)) return false;
            return AssassinSystem.TryHandleGuess(sender, args);
        }

        private static bool ArsonistIgnite(PlayerControl sender, string[] args)
        {
            if (RoleConfig.Arsonist?.Value != true || !ArsonistSystem.IsArsonist(sender)) return false;
            ArsonistSystem.TryIgnite(sender);
            return true;
        }

        private static bool ButtonBarryMeeting(PlayerControl sender, string[] args)
        {
            if (RoleConfig.ModifierButtonBarry?.Value != true || !ModifierSystem.Has(sender.PlayerId, ModifierSystem.ButtonBarry)) return false;
            if (AmongUsClient.Instance == null || MeetingHud.Instance != null)
            {
                Local("A meeting is already in progress.");
                return true;
            }
            try { sender.CmdReportDeadBody(null); } // null body = emergency meeting, from anywhere
            catch (Exception e) { Local("Could not call a meeting: " + e.Message); }
            return true;
        }

        private static void Revive(PlayerControl sender, string[] args)
        {
            if (!RequireHost(sender)) return;

            var target = ResolveTarget(args);
            if (target == null || target.Data == null || target.Data.Disconnected)
            {
                Local("Target player was not found or is disconnected.");
                return;
            }

            if (!target.Data.IsDead)
            {
                Local($"{DisplayName(target)} is already alive.");
                return;
            }

            // Classic Us 8.9 exposes the native zero-argument revive operation.
            // Use it instead of manually changing IsDead or destroying bodies.
            target.Revive();
            TownOfUsRpcMux.Send(ReviveRpc, target.PlayerId);
            Local($"Revived {DisplayName(target)}.");
        }

        [ManactorRpc(ReviveRpc)]
        private static void OnReviveRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost) return;
            if (senderId != client.HostId) return;

            var target = ResolveTarget(new[] { playerId.ToString(CultureInfo.InvariantCulture) });
            if (target == null || target.Data == null)
            {
                CommandState.QueueRevive(playerId);
                return;
            }
            if (target.Data.IsDead) target.Revive();
        }

        private static string ResolveRoleName(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "sheriff":
                case "townofus.sheriff":
                    return RoleConfig.Sheriff?.Value == true ? "townofus.Sheriff" : null;
                case "engineer":
                case "townofus.engineer":
                    return RoleConfig.Engineer?.Value == true ? "townofus.Engineer" : null;
                case "jester":
                case "townofus.jester":
                    return RoleConfig.Jester?.Value == true ? "townofus.Jester" : null;
                case "medic":
                case "townofus.medic":
                    return RoleConfig.Medic?.Value == true ? "townofus.Medic" : null;
                case "seer":
                case "townofus.seer":
                    return RoleConfig.Seer?.Value == true ? "townofus.Seer" : null;
                case "vigilante":
                case "townofus.vigilante":
                    return RoleConfig.Vigilante?.Value == true ? "townofus.Vigilante" : null;
                case "assassin":
                case "townofus.assassin":
                    return RoleConfig.Assassin?.Value == true ? "townofus.Assassin" : null;
                case "janitor":
                case "townofus.janitor":
                    return RoleConfig.Janitor?.Value == true ? "townofus.Janitor" : null;
                case "altruist":
                case "townofus.altruist":
                    return RoleConfig.Altruist?.Value == true ? "townofus.Altruist" : null;
                case "mayor":
                case "townofus.mayor":
                    return RoleConfig.Mayor?.Value == true ? "townofus.Mayor" : null;
                case "executioner":
                case "townofus.executioner":
                    return RoleConfig.Executioner?.Value == true ? "townofus.Executioner" : null;
                case "arsonist":
                case "townofus.arsonist":
                    return RoleConfig.Arsonist?.Value == true ? "townofus.Arsonist" : null;
                case "swapper":
                case "townofus.swapper":
                    return RoleConfig.Swapper?.Value == true ? "townofus.Swapper" : null;
                case "morphling":
                case "townofus.morphling":
                    return RoleConfig.Morphling?.Value == true ? "townofus.Morphling" : null;
                case "spy":
                case "townofus.spy":
                    return RoleConfig.Spy?.Value == true ? "townofus.Spy" : null;
                default:
                    return null;
            }
        }

        private static void ForceStart(PlayerControl sender)
        {
            if (!RequireHost(sender)) return;
            var client = AmongUsClient.Instance;
            if (client == null)
            {
                Warn("Force start is unavailable right now.");
                return;
            }

            if (PlayerControl.AllPlayerControls.Count == 0)
            {
                Warn("Force start is unavailable before the lobby is ready.");
                return;
            }

            client.StartGame();
            Local("Force-start requested.");
        }

        private static void Nickname(PlayerControl sender, string[] args)
        {
            if (args.Length == 0)
            {
                Local("Usage: /nickname <new name>");
                return;
            }

            var name = string.Join(" ", args).Trim();
            if (name.Length == 0 || name.Length > 24)
            {
                Local("Nickname must be between 1 and 24 characters.");
                return;
            }

            sender.RpcSetName(name);
            Local($"Nickname changed to {name}.");
        }

        private static void Gradient(PlayerControl sender, string[] args)
        {
            var enabled = ParseToggle(args, true);
            if (enabled == null)
            {
                Local("Usage: /gradient [on|off]");
                return;
            }

            VisualEffects.SetGradient(enabled.Value);
            Local($"hallowmarsh gradient {(enabled.Value ? "enabled" : "disabled")}.");
        }

        private static void Rainbow(PlayerControl sender, string[] args)
        {
            var enabled = ParseToggle(args, true);
            if (enabled == null)
            {
                Local("Usage: /rainbow [on|off]");
                return;
            }

            VisualEffects.SetRainbow(enabled.Value);
            Local($"Native rainbow color skin {(enabled.Value ? "enabled" : "disabled")}.");
        }

        private static void Color(PlayerControl sender, string[] args)
        {
            if (args.Length == 0)
            {
                Local("Usage: /color gradient [on|off] or /color rainbow [on|off]");
                return;
            }

            var mode = args[0].ToLowerInvariant();
            var rest = args.Skip(1).ToArray();
            if (mode == "gradient")
            {
                Gradient(sender, rest);
                return;
            }
            if (mode == "rainbow")
            {
                Rainbow(sender, rest);
                return;
            }

            Local("Use /color gradient or /color rainbow.");
        }

        private static void LobbyCode(PlayerControl sender, string[] args)
        {
            if (!RequireHost(sender)) return;

            if (args.Length == 0)
            {
                var current = Core.LobbyCode.ActiveCode;
                Local(string.IsNullOrEmpty(current)
                    ? "Usage: /code <word> or /code off"
                    : "Current lobby code: " + current);
                return;
            }

            var word = string.Join(" ", args).Trim();
            var upper = word.ToUpperInvariant();
            if (upper == "OFF" || upper == "DISABLE" || upper == "CLEAR" || upper == "NONE" || upper == "RESET")
            {
                Core.LobbyCode.Set(string.Empty);
                Local("Custom lobby code disabled.");
                return;
            }

            var code = Core.LobbyCode.Normalize(word);
            if (string.IsNullOrEmpty(code))
            {
                Local("Lobby code must be 1-6 letters or digits (A-Z, 0-9).");
                return;
            }

            Core.LobbyCode.Set(code);
            Local("Lobby code set to " + code + " (display alias — friends still join with the real code).");
        }

        private static void SystemMessage(PlayerControl sender, string[] args)
        {
            if (!RequireHost(sender)) return;
            if (args.Length == 0)
            {
                Local("Usage: /system <message>");
                return;
            }

            var message = string.Join(" ", args).Trim();
            if (message.Length > 120)
                message = message.Substring(0, 120);

            // Show the message locally AND broadcast it to every client through the
            // RPC mux, so the whole lobby sees the same native "SYSTEM ALERT" popup.
            // The host shows it directly (the RPC handler skips the host, exactly
            // like the custom-command path) so the message is never replaced by a
            // "sent" confirmation. Still host-only so clients cannot impersonate
            // server messages.
            SystemChat.Show(message);
            try { TownOfUsRpcMux.Send(SystemMessageRpc, message); }
            catch (Exception e) { Warn("Lobby broadcast failed: " + e.Message); }
        }

        [ManactorRpc(SystemMessageRpc)]
        private static void OnSystemMessageRpc(byte senderId, string message)
        {
            // The host already displayed the message when it broadcast it; only
            // remote clients display here. Exactly one display per client.
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost) return;
            if (string.IsNullOrWhiteSpace(message)) return;
            SystemChat.Show(message);
        }

        private static void Teleport(PlayerControl sender, string[] args, bool intoDropship)
        {
            if (!RequireHost(sender)) return;
            var target = ResolveTarget(args);
            if (target == null)
            {
                Local("Usage: /tpin [player] or /tpout [player]");
                return;
            }

            var ship = ShipStatus.Instance;
            if (ship == null || target.NetTransform == null || target.Data == null)
            {
                Warn("Teleport is unavailable on this screen/map.");
                return;
            }

            var count = Math.Max(1, PlayerControl.AllPlayerControls.Count);
            var position = ship.GetSpawnLocation(target.PlayerId, count, intoDropship);
            target.NetTransform.RpcSnapTo(position);
            Local($"Teleported {DisplayName(target)} {(intoDropship ? "into" : "out of")} the dropship.");
        }

        private static PlayerControl ResolveTarget(string[] args)
        {
            if (args.Length == 0) return PlayerControl.LocalPlayer;
            var query = string.Join(" ", args).Trim();
            byte id;
            if (byte.TryParse(query, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                for (var i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
                {
                    var player = PlayerControl.AllPlayerControls[i];
                    if (player != null && player.PlayerId == id) return player;
                }
                return null;
            }

            for (var i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
            {
                var player = PlayerControl.AllPlayerControls[i];
                if (player != null && player.Data != null &&
                    string.Equals(player.Data.PlayerName, query, StringComparison.OrdinalIgnoreCase))
                    return player;
            }
            return null;
        }


        private static bool? ParseToggle(string[] args, bool defaultValue)
        {
            if (args.Length == 0) return defaultValue;
            if (args.Length != 1) return null;
            switch (args[0].ToLowerInvariant())
            {
                case "on":
                case "enable":
                case "enabled":
                    return true;
                case "off":
                case "disable":
                case "disabled":
                    return false;
                default:
                    return null;
            }
        }

        private static DateTime _lastCustomCommandRequest = DateTime.MinValue;

        private static bool IsHost()
        {
            var client = AmongUsClient.Instance;
            return client != null && client.AmHost;
        }

        private static bool RequireHost(PlayerControl sender)
        {
            if (IsHost()) return true;
            Local("Only the current lobby host can use that command.");
            return false;
        }

        private static string DisplayName(PlayerControl player) =>
            player?.Data?.PlayerName ?? $"Player {player?.PlayerId.ToString() ?? "?"}";

        private static void Local(string message)
        {
            try
            {
                SystemChat.Show(message);
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogInfo(message + " (system alert failed: " + ex.Message + ")");
            }
        }

        private static void Warn(string message) => Local("⚠ " + message);
    }
}

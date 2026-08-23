using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Seer
{
    internal static class SeerSystem
    {
        private const string InvestigateRpc = "townofus.SeerInvestigate";
        private const string RequestInvestigateRpc = "townofus.SeerRequestInvestigate";
        private static readonly Dictionary<byte, int> UsesRemaining = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        // (seerId, targetId) -> (result text, result color). Drawn under the
        // target's name by the role presentation system instead of a popup.
        private static readonly Dictionary<(byte seerId, byte targetId), (string text, Color color)> Reveals = new();

        public static bool IsSeer(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, SeerRole.Id);

        public static void TryInvestigate(PlayerControl seer)
        {
            var client = AmongUsClient.Instance;
            if (client == null || seer == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestInvestigateRpc, seer.PlayerId);
                return;
            }
            if (!CanInvestigateNow(seer)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(seer, out var target)) return;

            var remaining = Math.Max(0, GetUses(seer.PlayerId) - 1);
            UsesRemaining[seer.PlayerId] = remaining;
            Cooldowns[seer.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.SeerCooldown));
            var reveal = ResolveResult(target);
            Reveals[(seer.PlayerId, target.PlayerId)] = reveal;
            TownOfUsRpcMux.Send(InvestigateRpc, seer.PlayerId, target.PlayerId, reveal.text, remaining);
        }

        internal static bool CanInvestigateNow(PlayerControl seer) =>
            IsSeer(seer) && !seer.Data.IsDead && GetUses(seer.PlayerId) > 0 &&
            DateTime.UtcNow >= GetCooldown(seer.PlayerId);

        private static int GetUses(byte id)
        {
            if (!UsesRemaining.TryGetValue(id, out var value))
            {
                value = RoleConfig.Count(RoleConfig.SeerUses);
                UsesRemaining[id] = value;
            }
            return value;
        }

        private static DateTime GetCooldown(byte id) =>
            Cooldowns.TryGetValue(id, out var value) ? value : DateTime.MinValue;

        /// <summary>
        /// Resolves what a Seer learns about a target: either the target's
        /// faction (Impostor / Neutral / Crewmate) or their role name, together
        /// with a color for the under-name reveal. The role name prefers the
        /// friendly custom-role display name when the target holds one.
        /// </summary>
        private static (string text, Color color) ResolveResult(PlayerControl target)
        {
            if (target?.Data?.myRole == null) return ("Unknown", new Color(0.6f, 0.6f, 0.6f, 1f));
            if (RoleConfig.RevealRole)
            {
                if (RolePresentation.TryGet(target, out var name, out var color))
                    return (name, color);
                var raw = target.Data.myRole.roleCodeName ?? target.Data.myRole.GetIl2CppType().Name;
                return (raw ?? "Unknown", Color.white);
            }
            switch (target.Data.myRole.RoleTeamType)
            {
                case RoleTeamTypes.Impostor: return ("Impostor", new Color(0.95f, 0.15f, 0.15f, 1f));
                case RoleTeamTypes.Neutral: return ("Neutral", new Color(0.75f, 0.5f, 0.9f, 1f));
                default: return ("Crewmate", new Color(0.2f, 0.85f, 0.5f, 1f));
            }
        }

        /// <summary>
        /// Returns the investigation result the given viewer has for a target,
        /// if any. The presentation system uses this to draw the result under
        /// the target's name instead of showing a chat/popup message.
        /// </summary>
        public static bool TryGetReveal(PlayerControl viewer, PlayerControl target, out string text, out Color color)
        {
            text = null;
            color = Color.white;
            if (viewer == null || target == null) return false;
            if (!Reveals.TryGetValue((viewer.PlayerId, target.PlayerId), out var reveal)) return false;
            text = reveal.text;
            color = reveal.color;
            return true;
        }

        public static void Reset()
        {
            UsesRemaining.Clear();
            Cooldowns.Clear();
            Reveals.Clear();
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();

        [ReactorRpc(RequestInvestigateRpc)]
        private static void OnRequestInvestigateRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryInvestigate(player);
                    return;
                }
            }
        }

        [ReactorRpc(InvestigateRpc)]
        private static void OnInvestigateRpc(byte senderId, byte seerPlayerId, byte targetPlayerId, string result, int usesRemaining)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            UsesRemaining[seerPlayerId] = Math.Max(0, usesRemaining);
            Cooldowns[seerPlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.SeerCooldown));
            var local = PlayerControl.LocalPlayer;
            if (local != null && local.PlayerId == seerPlayerId && IsSeer(local))
            {
                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    if (player == null || player.PlayerId != targetPlayerId) continue;
                    // Re-resolve locally so the color matches this client's role
                    // presentation (the host only sent the text). Fall back to the
                    // host's text if local resolution differs.
                    var reveal = ResolveResult(player);
                    Reveals[(seerPlayerId, targetPlayerId)] = (result ?? reveal.text, reveal.color);
                    break;
                }
            }
        }
    }
}

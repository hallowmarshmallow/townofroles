using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;
using UnityEngine;

namespace TownOfUs.ManuAPI.Roles.Altruist
{
    /// <summary>
    /// Altruist gameplay logic (ported from Town-Of-Us' Altruist.cs).
    ///
    /// The host resolves the target dead body, revives its player via the
    /// game's own PlayerControl.Revive(), removes the body, and kills the
    /// Altruist through KillManager — then broadcasts the revival so every
    /// client revives the same player. Uses are limited (configurable).
    /// </summary>
    internal static class AltruistSystem
    {
        private const string ReviveRpc = "townofus.AltruistRevive";
        private const string RequestReviveRpc = "townofus.AltruistRequestRevive";
        private static readonly Dictionary<byte, int> UsesRemaining = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();

        public static bool IsAltruist(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, AltruistRole.Id);

        internal static bool CanReviveNow(PlayerControl altruist)
        {
            if (!IsAltruist(altruist) || altruist.Data == null || altruist.Data.IsDead) return false;
            return GetUses(altruist.PlayerId) > 0 && DateTime.UtcNow >= GetCooldown(altruist.PlayerId) &&
                   FindClosestBody(altruist) != null;
        }

        public static void TryRevive(PlayerControl altruist)
        {
            var client = AmongUsClient.Instance;
            if (client == null || altruist == null || altruist.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestReviveRpc, altruist.PlayerId);
                return;
            }
            if (!CanReviveNow(altruist)) return;
            var body = FindClosestBody(altruist);
            if (body == null) return;

            var revived = PlayerUtils.FindById(body.ParentId);
            if (revived == null || revived.Data == null) return;

            var remaining = Math.Max(0, GetUses(altruist.PlayerId) - 1);
            UsesRemaining[altruist.PlayerId] = remaining;
            Cooldowns[altruist.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.AltruistCooldown));

            PerformRevive(revived, body);
            KillManager.Kill(altruist, altruist);
            TownOfUsRpcMux.Send(ReviveRpc, altruist.PlayerId, revived.PlayerId, remaining);
        }

        /// <summary>Revives the player and removes their dead body (host + clients).</summary>
        private static void PerformRevive(PlayerControl revived, DeadBody body)
        {
            if (revived == null || revived.Data == null) return;
            revived.Revive();
            Janitor.JanitorSystem.RemoveBody(body);
        }

        /// <summary>Nearest unreported dead body within vanilla kill distance.</summary>
        private static DeadBody FindClosestBody(PlayerControl player)
        {
            if (player == null || player.Data == null) return null;
            if (PlayerControl.GameOptions == null) return null; // lobby / pre-game frames
            var origin = player.GetTruePosition();
            var killDistance = GameOptionsData.KillDistances[PlayerControl.GameOptions.KillDistance];
            DeadBody best = null;
            var bestDistance = float.MaxValue;

            foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
            {
                if (body == null || body.Reported) continue;
                var distance = Vector2.Distance(origin, body.TruePosition);
                if (distance <= killDistance && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = body;
                }
            }
            return best;
        }

        private static int GetUses(byte altruistId)
        {
            if (!UsesRemaining.TryGetValue(altruistId, out var value))
            {
                value = RoleConfig.Count(RoleConfig.AltruistUses, 1);
                UsesRemaining[altruistId] = value;
            }
            return value;
        }

        private static DateTime GetCooldown(byte altruistId) =>
            Cooldowns.TryGetValue(altruistId, out var value) ? value : DateTime.MinValue;

        [ReactorRpc(RequestReviveRpc)]
        private static void OnRequestRevive(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryRevive(player);
                    return;
                }
            }
        }

        [ReactorRpc(ReviveRpc)]
        private static void OnRevive(byte senderId, byte altruistId, byte revivedId, int usesRemaining)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            UsesRemaining[altruistId] = Math.Max(0, usesRemaining);
            Cooldowns[altruistId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.AltruistCooldown));
            var revived = PlayerUtils.FindById(revivedId);
            if (revived == null || revived.Data == null) return;
            revived.Revive();
            foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
            {
                if (body == null || body.ParentId != revivedId) continue;
                Janitor.JanitorSystem.RemoveBody(body);
                return;
            }
        }

        private static PlayerControl PlayerUtils.FindById(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        public static void Reset()
        {
            UsesRemaining.Clear();
            Cooldowns.Clear();
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
    }
}

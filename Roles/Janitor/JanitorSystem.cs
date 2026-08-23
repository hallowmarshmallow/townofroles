using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;
using UnityEngine;

namespace TownOfUs.ManuAPI.Roles.Janitor
{
    /// <summary>
    /// Janitor gameplay logic (ported from Town-Of-Us' Janitor.cs).
    ///
    /// The host resolves the clean (nearest unreported dead body within kill
    /// distance), removes the body locally, and broadcasts the removed body's
    /// ParentId so every client hides the same body. A small cooldown prevents
    /// spam; there is no per-game use limit (matches the original role).
    /// </summary>
    internal static class JanitorSystem
    {
        private const string CleanRpc = "townofus.JanitorClean";
        private const string RequestCleanRpc = "townofus.JanitorRequestClean";
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();

        public static bool IsJanitor(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, JanitorRole.Id);

        internal static bool CanCleanNow(PlayerControl janitor)
        {
            if (!IsJanitor(janitor) || janitor.Data == null || janitor.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(janitor.PlayerId) && FindClosestBody(janitor) != null;
        }

        public static void TryClean(PlayerControl janitor)
        {
            var client = AmongUsClient.Instance;
            if (client == null || janitor == null || janitor.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestCleanRpc, janitor.PlayerId);
                return;
            }
            if (!CanCleanNow(janitor)) return;
            var body = FindClosestBody(janitor);
            if (body == null) return;

            Cooldowns[janitor.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.JanitorCleanCooldown, 10f));
            RemoveBody(body);
            TownOfUsRpcMux.Send(CleanRpc, body.ParentId);
        }

        private static DateTime GetCooldown(byte janitorId) =>
            Cooldowns.TryGetValue(janitorId, out var value) ? value : DateTime.MinValue;

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

        internal static void RemoveBody(DeadBody body)
        {
            if (body == null || body.gameObject == null) return;
            body.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(body.gameObject);
        }

        [ReactorRpc(RequestCleanRpc)]
        private static void OnRequestClean(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryClean(player);
                    return;
                }
            }
        }

        [ReactorRpc(CleanRpc)]
        private static void OnClean(byte senderId, byte parentId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
            {
                if (body == null || body.ParentId != parentId) continue;
                RemoveBody(body);
                return;
            }
        }

        public static void Reset() => Cooldowns.Clear();
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
    }
}

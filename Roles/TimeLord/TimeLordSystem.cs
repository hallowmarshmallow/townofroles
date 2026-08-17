using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.TimeLord
{
    /// <summary>
    /// Time Lord gameplay logic (ported from Town-Of-Us' TimeLord.cs).
    ///
    /// The host samples every player's position into a short ring buffer; on
    /// Rewind every alive player is snapped (RpcSnapTo — the game's own synced
    /// teleport) back to where they stood RewindSeconds ago. Host-authoritative,
    /// cooldown-gated.
    /// </summary>
    internal static class TimeLordSystem
    {
        private const string RequestRewindRpc = "townofus.TimeLordRequestRewind";
        private const int SampleCount = 90; // 30s at 3 samples/s
        private const int SamplesPerTick = 5; // accumulate a sample every 5 ticks (~1/s host tick pacing)

        private static readonly Dictionary<byte, Vector2[]> History = new();
        private static readonly Dictionary<byte, int> HistoryWrite = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static int _tickCount;

        public static bool IsTimeLord(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, TimeLordRole.Id);

        internal static bool CanRewindNow(PlayerControl timeLord)
        {
            if (!IsTimeLord(timeLord) || timeLord.Data == null || timeLord.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(timeLord.PlayerId);
        }

        public static void TryRewind(PlayerControl timeLord)
        {
            var client = AmongUsClient.Instance;
            if (client == null || timeLord == null || timeLord.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestRewindRpc, timeLord.PlayerId);
                return;
            }
            if (!CanRewindNow(timeLord)) return;

            var seconds = RoleConfig.Seconds(RoleConfig.RewindSeconds, 5f);
            Cooldowns[timeLord.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.RewindCooldown, 30f));
            ApplyRewind(seconds);
            Local("Time rewound!");
        }

        /// <summary>Host tick: record positions; rewind on schedule.</summary>
        public static void Tick()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (PlayerControl.GameOptions == null) return;

            _tickCount++;
            if (_tickCount % SamplesPerTick != 0) return;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected) continue;
                var id = player.PlayerId;
                if (!History.TryGetValue(id, out var ring))
                {
                    ring = new Vector2[SampleCount];
                    History[id] = ring;
                    HistoryWrite[id] = 0;
                }
                var index = HistoryWrite[id];
                ring[index] = player.GetTruePosition();
                HistoryWrite[id] = (index + 1) % SampleCount;
            }
        }

        /// <summary>Snap every alive player to the recorded position ~seconds ago.</summary>
        private static void ApplyRewind(float seconds)
        {
            if (seconds <= 0f) return;
            var steps = (int)(seconds * SamplesPerTick);
            if (steps >= SampleCount) steps = SampleCount - 1;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
                if (player.NetTransform == null) continue;
                if (!History.TryGetValue(player.PlayerId, out var ring)) continue;

                var write = HistoryWrite[player.PlayerId];
                // Circular read `steps` slots back. Vented players snap to their
                // pre-vent position; the game's vent animation reconciles.
                var target = ring[(write - steps + SampleCount) % SampleCount];
                if (target == Vector2.zero) continue; // not enough history yet
                try { player.NetTransform.RpcSnapTo(target); } catch { }
            }
        }

        [ManactorRpc(RequestRewindRpc)]
        private static void OnRequestRewind(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryRewind(player);
                    return;
                }
            }
        }

        private static DateTime GetCooldown(byte playerId) =>
            Cooldowns.TryGetValue(playerId, out var value) ? value : DateTime.MinValue;

        public static void Reset()
        {
            History.Clear();
            HistoryWrite.Clear();
            Cooldowns.Clear();
            _tickCount = 0;
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();

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

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
    /// teleport) back to where they stood RewindSeconds ago. With RewindRevive
    /// on, players killed inside the rewound window come back and their bodies
    /// disappear — the signature upstream behavior (RecordRewind.cs).
    /// Host-authoritative, cooldown-gated.
    /// </summary>
    internal static class TimeLordSystem
    {
        private const string RequestRewindRpc = "townofus.TimeLordRequestRewind";
        private const string ReviveRpc = "townofus.TimeLordRewindRevive";
        private const int SampleCount = 90;
        private const int SamplesPerTick = 5; // accumulate a position sample every 5 ticks

        private static readonly Dictionary<byte, Vector2[]> History = new();
        // Timestamp of each recorded sample (same ring layout as History).
        // Rewind looks up "where was this player at T-seconds ago" by time, not
        // by tick arithmetic — SamplesPerTick pacing depends on framerate, so
        // converting seconds→steps without timestamps drifted badly.
        private static readonly Dictionary<byte, float[]> HistoryTimes = new();
        private static readonly Dictionary<byte, int> HistoryWrite = new();
        // Host-side record of when each player died (unscaled time). RewindRevive
        // resurrects anyone whose death falls inside the rewound window.
        private static readonly Dictionary<byte, float> DeathTimes = new();
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
            if (RoleConfig.RewindRevive?.Value != false) ApplyRevive(seconds);
        }

        /// <summary>Host tick: track deaths; record positions on the sample cadence.</summary>
        public static void Tick()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (PlayerControl.GameOptions == null) return;

            // Track death times for RewindRevive.
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.Disconnected) continue;
                var id = player.PlayerId;
                if (player.Data.IsDead)
                {
                    if (!DeathTimes.ContainsKey(id)) DeathTimes[id] = Time.unscaledTime;
                }
                else if (DeathTimes.ContainsKey(id))
                {
                    DeathTimes.Remove(id); // revived by Altruist/etc.
                }
            }

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
                if (!HistoryTimes.TryGetValue(id, out var times))
                {
                    times = new float[SampleCount];
                    HistoryTimes[id] = times;
                }
                times[index] = Time.unscaledTime;
                HistoryWrite[id] = (index + 1) % SampleCount;
            }
        }

        /// <summary>Snap every alive player to their recorded position ~seconds ago.</summary>
        private static void ApplyRewind(float seconds)
        {
            if (seconds <= 0f) return;
            var cutoff = Time.unscaledTime - seconds;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
                if (player.NetTransform == null) continue;
                if (!History.TryGetValue(player.PlayerId, out var ring)) continue;
                if (!HistoryTimes.TryGetValue(player.PlayerId, out var times)) continue;

                var write = HistoryWrite[player.PlayerId];
                // Newest recorded sample at or before the cutoff (walk back from
                // the write head). Falls back to the oldest sample when history
                // is shorter than the rewind window.
                Vector2 target = ring[write];
                var found = false;
                for (int back = 1; back <= SampleCount; back++)
                {
                    int i = (write - back + SampleCount) % SampleCount;
                    if (times[i] <= 0f) break; // unwritten slot → end of history
                    target = ring[i];
                    found = true;
                    if (times[i] <= cutoff) break;
                }
                if (!found || target == Vector2.zero) continue; // no usable history
                try { player.NetTransform.RpcSnapTo(target); } catch { }
            }
        }

        /// <summary>
        /// RewindRevive (Town-Of-Us): players killed inside the rewind window
        /// come back — host revives them via the game's own Revive(), removes
        /// their bodies, and every client mirrors it over the companion RPC.
        /// </summary>
        private static void ApplyRevive(float seconds)
        {
            var cutoff = Time.unscaledTime - seconds;
            foreach (var pair in new Dictionary<byte, float>(DeathTimes))
            {
                if (pair.Value < cutoff) continue; // died before the window
                DeathTimes.Remove(pair.Key);

                var victim = FindPlayer(pair.Key);
                if (victim == null || victim.Data == null || !victim.Data.IsDead || victim.Data.Disconnected) continue;

                try { victim.Revive(); } catch { continue; }
                RemoveBody(pair.Key);
                TownOfUsRpcMux.Send(ReviveRpc, pair.Key);
                if (pair.Key == PlayerControl.LocalPlayer?.PlayerId)
                    Local("The timeline healed — you are alive again!");
            }
        }

        private static void RemoveBody(byte victimId)
        {
            foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
            {
                if (body != null && body.ParentId == victimId)
                    Janitor.JanitorSystem.RemoveBody(body);
            }
        }

        [ManactorRpc(ReviveRpc)]
        private static void OnReviveRpc(byte senderId, byte victimId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var victim = FindPlayer(victimId);
            if (victim == null || victim.Data == null || !victim.Data.IsDead) return;
            try { victim.Revive(); } catch { return; }
            RemoveBody(victimId);
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

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        public static void Reset()
        {
            History.Clear();
            HistoryTimes.Clear();
            DeathTimes.Clear();
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

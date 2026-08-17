using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Swooper
{
    /// <summary>
    /// Swooper gameplay logic (ported from Town-Of-Us' Swooper.cs).
    ///
    /// The Swooper becomes temporarily invisible. The host validates the use and
    /// broadcasts the duration; every client (host included) disables the
    /// Swooper's renderers while active and re-enables them on expiry. Renderer
    /// toggling (not alpha) is used because it is stable across the IL2CPP
    /// interop and cannot leave materials in a tinted state.
    /// </summary>
    internal static class SwooperSystem
    {
        private const string StartRpc = "townofus.SwooperStart";
        private const string RequestRpc = "townofus.SwooperRequest";

        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static readonly Dictionary<byte, DateTime> InvisibleUntil = new();
        private static readonly HashSet<byte> Hidden = new();

        public static bool IsSwooper(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, SwooperRole.Id);

        internal static bool CanSwoopNow(PlayerControl swooper)
        {
            if (!IsSwooper(swooper) || swooper.Data == null || swooper.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(swooper.PlayerId);
        }

        public static void TrySwoop(PlayerControl swooper)
        {
            var client = AmongUsClient.Instance;
            if (client == null || swooper == null || swooper.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestRpc, swooper.PlayerId);
                return;
            }
            if (!CanSwoopNow(swooper)) return;

            var duration = RoleConfig.Seconds(RoleConfig.SwoopDuration, 5f);
            Cooldowns[swooper.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.SwoopCooldown, 25f));
            InvisibleUntil[swooper.PlayerId] = DateTime.UtcNow.AddSeconds(duration);
            TownOfUsRpcMux.Send(StartRpc, swooper.PlayerId, duration);
            SetHidden(swooper, true);
            Local("You swooped into the shadows.");
        }

        /// <summary>Runs every frame on every client: hide active swoopers, restore on expiry.</summary>
        public static void Tick()
        {
            if (InvisibleUntil.Count == 0) return;
            var now = DateTime.UtcNow;
            foreach (var key in new List<byte>(InvisibleUntil.Keys))
            {
                var player = FindPlayer(key);
                if (player == null || player.Data == null || player.Data.IsDead)
                {
                    // Re-enable renderers even on death/despawn so a revived
                    // Swooper is not left invisible forever.
                    if (player != null) SetHidden(player, false);
                    InvisibleUntil.Remove(key);
                    Hidden.Remove(key);
                    continue;
                }
                if (now < InvisibleUntil[key])
                {
                    SetHidden(player, true);
                    continue;
                }
                InvisibleUntil.Remove(key);
                SetHidden(player, false);
                Hidden.Remove(key);
            }
        }

        private static void SetHidden(PlayerControl player, bool hidden)
        {
            if (player == null) return;
            try
            {
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    renderer.enabled = !hidden;
                }
            }
            catch { }
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequest(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TrySwoop(player);
                    return;
                }
            }
        }

        [ManactorRpc(StartRpc)]
        private static void OnStart(byte senderId, byte swooperId, float duration)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var swooper = FindPlayer(swooperId);
            if (swooper == null) return;
            InvisibleUntil[swooperId] = DateTime.UtcNow.AddSeconds(duration);
            SetHidden(swooper, true);
        }

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        private static DateTime GetCooldown(byte playerId) =>
            Cooldowns.TryGetValue(playerId, out var value) ? value : DateTime.MinValue;

        public static void Reset()
        {
            // Restore visibility for anyone still hidden before clearing state.
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player != null && Hidden.Contains(player.PlayerId))
                    SetHidden(player, false);
            }
            Hidden.Clear();
            InvisibleUntil.Clear();
            Cooldowns.Clear();
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => Reset();

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

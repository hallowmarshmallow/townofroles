using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Medic
{
    internal static class MedicSystem
    {
        private const string ShieldRpc = "townofus.MedicShield";
        private const string RequestProtectRpc = "townofus.MedicRequestProtect";
        private static readonly Dictionary<byte, int> UsesRemaining = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static readonly Dictionary<byte, byte> Shields = new();

        public static bool IsMedic(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, MedicRole.Id);

        public static void TryProtect(PlayerControl medic)
        {
            var client = AmongUsClient.Instance;
            if (client == null || medic == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestProtectRpc, medic.PlayerId);
                return;
            }
            if (!CanProtectNow(medic)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(medic, out var target)) return;

            var remaining = ApplyShield(medic.PlayerId, target.PlayerId);
            TownOfUsRpcMux.Send(ShieldRpc, medic.PlayerId, target.PlayerId, remaining);
        }

        internal static bool CanProtectNow(PlayerControl medic)
        {
            if (!IsMedic(medic) || medic.Data.IsDead) return false;
            var id = medic.PlayerId;
            return GetUses(id) > 0 && DateTime.UtcNow >= GetCooldown(id);
        }

        private static int GetUses(byte medicId)
        {
            if (!UsesRemaining.TryGetValue(medicId, out var value))
            {
                value = RoleConfig.Count(RoleConfig.MedicUses);
                UsesRemaining[medicId] = value;
            }
            return value;
        }

        private static DateTime GetCooldown(byte medicId) =>
            Cooldowns.TryGetValue(medicId, out var value) ? value : DateTime.MinValue;

        private static int ApplyShield(byte medicId, byte targetId)
        {
            var remaining = Math.Max(0, GetUses(medicId) - 1);
            UsesRemaining[medicId] = remaining;
            Shields[medicId] = targetId;
            Cooldowns[medicId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.MedicCooldown));
            return remaining;
        }

        public static void OnBeforeMurder(MurderEventArgs args)
        {
            if (args?.Target == null) return;
            byte? consumedMedic = null;
            foreach (var shield in Shields)
            {
                if (shield.Value == args.Target.PlayerId)
                {
                    consumedMedic = shield.Key;
                    break;
                }
            }
            if (!consumedMedic.HasValue) return;

            args.Cancelled = true;
            if (RoleConfig.MedicShieldBreaksOnKill?.Value != false)
                Shields.Remove(consumedMedic.Value);
        }

        public static void Reset()
        {
            UsesRemaining.Clear();
            Cooldowns.Clear();
            Shields.Clear();
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();

        [ManactorRpc(RequestProtectRpc)]
        private static void OnRequestProtectRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryProtect(player);
                    return;
                }
            }
        }

        [ManactorRpc(ShieldRpc)]
        private static void OnShieldRpc(byte senderId, byte medicId, byte targetId, int usesRemaining)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            UsesRemaining[medicId] = Math.Max(0, usesRemaining);
            Shields[medicId] = targetId;
            Cooldowns[medicId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.MedicCooldown));
        }
    }
}

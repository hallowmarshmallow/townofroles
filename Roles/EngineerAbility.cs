using System;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Engineer
{
    internal static class EngineerAbility
    {
        private static DateTime _cooldownUntil;

        internal static bool CanFixSab(PlayerControl engineer)
        {
            return engineer != null && engineer.Data != null && !engineer.Data.IsDead &&
                   EngineerSystem.IsEngineer(engineer) && DateTime.UtcNow >= _cooldownUntil && ShipStatus.Instance != null;
        }

        public static bool TryFixSab(PlayerControl engineer)
        {
            if (!CanFixSab(engineer)) return false;

            var ship = ShipStatus.Instance;
            if (ship == null) return false;
            ship.RpcRepairSystem(SystemTypes.Sabotage, 0);
            _cooldownUntil = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.EngineerFixCooldown, 30f));
            return true;
        }

        public static void Reset()
        {
            _cooldownUntil = DateTime.MinValue;
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
    }

    internal static class EngineerSystem
    {
        public static bool IsEngineer(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, EngineerRole.Id);
    }
}

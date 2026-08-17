using System;

namespace TownOfUs.ManuAPI.Roles.Sheriff
{
    internal static class SheriffAbilityHolder
    {
        private static DateTime _cooldownUntil;

        public static bool IsCoolingDown => DateTime.UtcNow < _cooldownUntil;

        public static bool TryStartCooldown()
        {
            if (IsCoolingDown) return false;
            _cooldownUntil = DateTime.UtcNow.AddSeconds(Options.KillCooldown);
            return true;
        }

        public static void Reset() => _cooldownUntil = DateTime.MinValue;
    }
}

using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Sheriff
{
    internal static class Options
    {
        public static float KillCooldown => RoleConfig.Seconds(RoleConfig.SheriffKillCooldown, 10f);
        public static bool KillOther => RoleConfig.SheriffKillOther?.Value ?? true;
        /// <summary>May the Sheriff shoot neutrals (Jester / Executioner)? Defaults to on.</summary>
        public static bool KillsNeutrals => RoleConfig.SheriffKillsNeutrals?.Value ?? true;
        public static bool BodyReport => RoleConfig.SheriffBodyReport?.Value ?? false;
    }
}

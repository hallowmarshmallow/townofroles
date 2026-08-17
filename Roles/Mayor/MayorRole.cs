using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Mayor
{
    /// <summary>
    /// Mayor — a Crewmate whose vote counts double in meetings (vote bank).
    /// Ported from the original Town-Of-Us Mayor role.
    /// </summary>
    internal sealed class MayorRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Mayor";

        public override string DisplayName => "Mayor";
        public override string RoleTypeName => Id;
        public override string Description => "Your vote counts double in meetings.";
        public override int Count => RoleConfig.Count(RoleConfig.MayorCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.MayorChance);
        public override string DescriptionShort => "Your votes count double.";
        // Mayor is passive (no ability button).
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.35f, 0.6f, 1f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Mayor.";
    }
}

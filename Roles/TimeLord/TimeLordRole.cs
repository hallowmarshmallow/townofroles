using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.TimeLord
{
    /// <summary>
    /// Time Lord — a Crewmate who can rewind everyone back to where they stood.
    /// Ported from the original Town-Of-Us TimeLord role.
    /// </summary>
    internal sealed class TimeLordRole : CustomCrewmateRole
    {
        public const string Id = "townofus.TimeLord";

        public override string DisplayName => "Time Lord";
        public override string RoleTypeName => Id;
        public override string Description => "Rewind time to undo player movement.";
        public override int Count => RoleConfig.Count(RoleConfig.TimeLordCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.TimeLordChance);
        public override string DescriptionShort => "Use Rewind to snap everyone back a few seconds.";
        public override Color TeamColor => new(0.55f, 0.65f, 0.95f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Time Lord.";
    }
}

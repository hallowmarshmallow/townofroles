using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Investigator
{
    /// <summary>
    /// Investigator — a Crewmate who sees the footprints of other players.
    /// Ported from the original Town-Of-Us Investigator role.
    /// </summary>
    internal sealed class InvestigatorRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Investigator";

        public override string DisplayName => "Investigator";
        public override string RoleTypeName => Id;
        public override string Description => "See the footprints of other players.";
        public override int Count => RoleConfig.Count(RoleConfig.InvestigatorCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.InvestigatorChance);
        public override string DescriptionShort => "Footprints show where other players have walked.";
        public override Color TeamColor => new(0.35f, 0.9f, 0.75f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Investigator.";
    }
}

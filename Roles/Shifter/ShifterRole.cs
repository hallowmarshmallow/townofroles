using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Shifter
{
    /// <summary>
    /// Shifter — a Neutral with no win condition who swaps roles and tasks with
    /// other players. Ported from the original Town-Of-Us Shifter role.
    /// </summary>
    internal sealed class ShifterRole : CustomRole
    {
        public const string Id = "townofus.Shifter";

        public override string DisplayName => "Shifter";
        public override string RoleTypeName => Id;
        public override RoleTeamTypes TeamType => RoleTeamTypes.Neutral;
        public override string Description => "Swap roles and tasks with other players.";
        public override int Count => RoleConfig.Count(RoleConfig.ShifterCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.ShifterChance);
        public override string DescriptionShort => "Shift with a nearby player to take their role and tasks. Shifting an Impostor kills you.";
        public override Color TeamColor => new(0.75f, 0.55f, 0.95f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Shifter.";
    }
}

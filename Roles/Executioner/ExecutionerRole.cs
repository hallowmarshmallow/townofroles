using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Executioner
{
    /// <summary>
    /// Executioner — a Neutral role with a secret target. Wins when that
    /// target is voted out. Ported from the original Town-Of-Us Executioner.
    /// </summary>
    internal sealed class ExecutionerRole : CustomRole
    {
        public const string Id = "townofus.Executioner";

        public override string DisplayName => "Executioner";
        public override string RoleTypeName => Id;
        public override RoleTeamTypes TeamType => RoleTeamTypes.Neutral;
        public override string Description => "Get your target voted out to win.";
        public override int Count => RoleConfig.Count(RoleConfig.ExecutionerCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.ExecutionerChance);
        public override string DescriptionShort => "You win when your target is voted out.";
        public override Color TeamColor => new(0.45f, 0.9f, 0.85f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Executioner.";
    }
}

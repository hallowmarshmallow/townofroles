using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Jester
{
    /// <summary>
    /// Jester — a Neutral role that wins when the lobby votes it out.
    /// </summary>
    internal sealed class JesterRole : CustomRole
    {
        public const string Id = "townofus.Jester";

        public override string DisplayName => "Jester";
        public override string RoleTypeName => Id;
        public override RoleTeamTypes TeamType => RoleTeamTypes.Neutral;
        public override string Description => "Get yourself voted out to win.";
        public override int Count => RoleConfig.Count(RoleConfig.JesterCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.JesterChance);
        public override string DescriptionShort => "You win when you are voted out.";
        public override Color TeamColor => new(0.86f, 0.35f, 0.95f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Jester.";
    }
}

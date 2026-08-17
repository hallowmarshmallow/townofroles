using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Phantom
{
    /// <summary>
    /// Phantom — a Neutral who, on death, becomes a fading phantom and must
    /// complete all remaining tasks to win. Ported from the original
    /// Town-Of-Us Phantom role.
    /// </summary>
    internal sealed class PhantomRole : CustomRole
    {
        public const string Id = "townofus.Phantom";

        public override string DisplayName => "Phantom";
        public override string RoleTypeName => Id;
        public override RoleTeamTypes TeamType => RoleTeamTypes.Neutral;
        public override string Description => "Complete all your tasks after death to win.";
        public override int Count => RoleConfig.Count(RoleConfig.PhantomCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.PhantomChance);
        public override string DescriptionShort => "When you die, keep doing tasks as a phantom. Finish them all to win.";
        public override Color TeamColor => new(0.75f, 0.75f, 0.85f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Phantom.";
    }
}

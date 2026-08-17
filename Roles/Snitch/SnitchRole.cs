using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Snitch
{
    /// <summary>
    /// Snitch — a Crewmate who sees arrows to the Impostors once all tasks are
    /// complete. Ported from the original Town-Of-Us Snitch role.
    /// </summary>
    internal sealed class SnitchRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Snitch";

        public override string DisplayName => "Snitch";
        public override string RoleTypeName => Id;
        public override string Description => "Find the Impostors once your tasks are done.";
        public override int Count => RoleConfig.Count(RoleConfig.SnitchCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.SnitchChance);
        public override string DescriptionShort => "Complete all your tasks to reveal the Impostors with arrows.";
        public override Color TeamColor => new(0.95f, 0.85f, 0.35f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Snitch.";
    }
}

using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Spy
{
    /// <summary>
    /// Spy — a Crewmate who is informed when someone is in a vent and when the
    /// Arsonist douses a player. Ported from the original Town-Of-Us Spy role.
    /// </summary>
    internal sealed class SpyRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Spy";

        public override string DisplayName => "Spy";
        public override string RoleTypeName => Id;
        public override string Description => "Get notified when someone vents or gets doused.";
        public override int Count => RoleConfig.Count(RoleConfig.SpyCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.SpyChance);
        public override string DescriptionShort => "You receive intel when a player vents or is doused.";
        // Spy is passive (no ability button).
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.85f, 0.7f, 0.35f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Spy.";
    }
}

using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Engineer
{
    /// <summary>
    /// Engineer — a Crewmate who can use the game's native vent system.
    /// No custom button or asset is required; CanVent makes Classic Us handle
    /// the existing vent UI and networked vent RPCs.
    /// </summary>
    internal sealed class EngineerRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Engineer";

        public override string DisplayName => "Engineer";
        public override string RoleTypeName => Id;
        public override string Description => "Use vents to move around the map.";
        public override int Count => RoleConfig.Count(RoleConfig.EngineerCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.EngineerChance);
        public override string DescriptionShort => "You can use vents.";
        public override bool CanVent => true;
        // Engineer uses a dedicated ManuAPI ability button, not the native Kill button.
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.2f, 0.85f, 0.95f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Engineer.";
    }
}

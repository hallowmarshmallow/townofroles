using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Seer
{
    internal sealed class SeerRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Seer";

        public override string DisplayName => "Seer";
        public override string RoleTypeName => Id;
        public override string Description => "Investigate players to reveal their faction.";
        public override int Count => RoleConfig.Count(RoleConfig.SeerCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.SeerChance);
        public override string DescriptionShort => "Investigate players.";
        // Seer uses a dedicated ManuAPI ability button, not the native Kill button.
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.65f, 0.45f, 1f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Seer.";
    }
}

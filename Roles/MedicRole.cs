using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Medic
{
    internal sealed class MedicRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Medic";

        public override string DisplayName => "Medic";
        public override string RoleTypeName => Id;
        public override string Description => "Protect one player from a kill.";
        public override int Count => RoleConfig.Count(RoleConfig.MedicCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.MedicChance);
        public override string DescriptionShort => "Protect a player once.";
        // Medic uses a dedicated ManuAPI ability button, not the native Kill button.
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.3f, 0.95f, 0.55f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Medic.";
    }
}

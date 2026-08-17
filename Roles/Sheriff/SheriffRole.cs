using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Sheriff
{
    internal sealed class SheriffRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Sheriff";

        public override string DisplayName => "Sheriff";
        public override string RoleTypeName => Id;
        public override string Description => "Shoot the impostors";
        public override int Count => RoleConfig.Count(RoleConfig.SheriffCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.SheriffChance);
        public override string DescriptionShort => "Use the Kill button to shoot your target.";
        // The Sheriff is a crewmate and fires through its dedicated "Shoot" ability
        // button (CustomRoleAbilities.SheriffButton), not the native Kill button. The
        // native Kill button's target detection is impostor-only, so leaving this true
        // shows a Kill button that never lights up for a crewmate Sheriff.
        public override bool CanUseKillButton => false;
        public override string KillAbilityName => "Shoot";
        public override Color TeamColor => new(0.95f, 0.8f, 0.2f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Sheriff.";
    }
}

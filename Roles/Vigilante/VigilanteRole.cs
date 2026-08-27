using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Vigilante
{
    internal sealed class VigilanteRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Vigilante";

        public override string DisplayName => "Vigilante";
        public override string RoleTypeName => Id;
        public override string Description => "Shoot an Impostor; shooting a Crewmate kills you.";
        public override int Count => RoleConfig.Count(RoleConfig.VigilanteCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.VigilanteChance);
        public override string DescriptionShort => "Shoot once.";
        public override bool CanUseKillButton => true;
        public override string KillAbilityName => "Shoot";
        public override Color TeamColor => new(0.95f, 0.65f, 0.25f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Vigilante.";
    }
}

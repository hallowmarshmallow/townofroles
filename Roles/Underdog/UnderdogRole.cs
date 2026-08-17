using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Underdog
{
    /// <summary>
    /// Underdog — a passive Impostor whose kill cooldown shrinks when outnumbered.
    /// Ported from the original Town-Of-Us Underdog role.
    /// </summary>
    internal sealed class UnderdogRole : CustomImpostorRole
    {
        public const string Id = "townofus.Underdog";

        public override string DisplayName => "Underdog";
        public override string RoleTypeName => Id;
        public override string Description => "Faster kills when the Impostors are outnumbered.";
        public override int Count => RoleConfig.Count(RoleConfig.UnderdogCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.UnderdogChance);
        public override string DescriptionShort => "Your kill cooldown is reduced while outnumbered.";
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.9f, 0.4f, 0.4f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Underdog.";
    }
}

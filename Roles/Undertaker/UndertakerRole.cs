using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Undertaker
{
    /// <summary>
    /// Undertaker — an Impostor who can drag dead bodies and hide them.
    /// Ported from the original Town-Of-Us Undertaker role.
    /// </summary>
    internal sealed class UndertakerRole : CustomImpostorRole
    {
        public const string Id = "townofus.Undertaker";

        public override string DisplayName => "Undertaker";
        public override string RoleTypeName => Id;
        public override string Description => "Drag dead bodies away so they cannot be reported.";
        public override int Count => RoleConfig.Count(RoleConfig.UndertakerCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.UndertakerChance);
        public override string DescriptionShort => "Use Drag to carry a body; use it again to drop.";
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.45f, 0.45f, 0.75f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Undertaker.";
    }
}

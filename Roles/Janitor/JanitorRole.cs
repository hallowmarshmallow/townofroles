using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Janitor
{
    /// <summary>
    /// Janitor — an Impostor who can clean dead bodies so they cannot be reported.
    /// Ported from the original Town-Of-Us Janitor role.
    /// </summary>
    internal sealed class JanitorRole : CustomImpostorRole
    {
        public const string Id = "townofus.Janitor";

        public override string DisplayName => "Janitor";
        public override string RoleTypeName => Id;
        public override string Description => "Clean dead bodies so they cannot be reported.";
        public override int Count => RoleConfig.Count(RoleConfig.JanitorCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.JanitorChance);
        public override string DescriptionShort => "Use Clean on a dead body to make it disappear.";
        // Janitor uses a dedicated ManuAPI ability button, not the native Kill button.
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.55f, 0.72f, 0.95f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Janitor.";
    }
}

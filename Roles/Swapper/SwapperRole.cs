using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Swapper
{
    /// <summary>
    /// Swapper — a Crewmate who can swap the votes of two players in a meeting.
    /// Ported from the original Town-Of-Us Swapper role.
    /// </summary>
    internal sealed class SwapperRole : CustomCrewmateRole
    {
        public const string Id = "townofus.Swapper";

        public override string DisplayName => "Swapper";
        public override string RoleTypeName => Id;
        public override string Description => "During meetings, swap the votes of two players.";
        public override int Count => RoleConfig.Count(RoleConfig.SwapperCount);
        public override float RoleChancePercent => RoleConfig.Chance(RoleConfig.SwapperChance);
        public override string DescriptionShort => "Use the Swap buttons in a meeting to pick two players; their votes are swapped.";
        // Swapper uses meeting buttons, not the native Kill button.
        public override string KillAbilityName => string.Empty;
        public override Color TeamColor => new(0.45f, 0.8f, 0.3f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Swapper.";
    }
}

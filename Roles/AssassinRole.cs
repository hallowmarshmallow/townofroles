using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Assassin
{
    /// <summary>
    /// Assassin — an Impostor who can guess a player's role during a meeting.
    /// A correct guess kills the target; a wrong guess kills the Assassin.
    /// </summary>
    internal sealed class AssassinRole : CustomImpostorRole
    {
        public const string Id = "townofus.Assassin";

        public override string DisplayName => "Assassin";
        public override string RoleTypeName => Id;
        public override string Description => "During meetings, guess another player's role. A correct guess kills them; a wrong guess kills you.";
        public override string DescriptionShort => "Use /guess <player> <role> during a meeting. Wrong guesses kill you.";
        public override int Count => AssassinSettingsSync.ActiveEnabled ? AssassinSettingsSync.ActiveCount : 0;
        public override float RoleChancePercent => AssassinSettingsSync.ActiveChance;
        public override string KillAbilityName => "Guess";
        public override string KillAbilityImageName => "Guess";
        public override Color TeamColor => new(0.95f, 0.15f, 0.18f, 1f);
        public override string EjectionText(string playerName) => $"{playerName} was the Assassin.";
    }
}

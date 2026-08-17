using HarmonyLib;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Commands
{
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CheckEndCriteria))]
    internal static class ShipStatus_CheckEndCriteria_CommandPatch
    {
        private static bool Prefix()
        {
            // Returning false skips only the normal win-condition check. We do not
            // block AmongUsClient.OnGameEnd, so disconnects/explicit host endings
            // still cleanly close the match.
            return !CommandState.NoGameEnd;
        }
    }
}

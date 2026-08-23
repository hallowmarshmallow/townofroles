using ClassicUs.ManuAPI;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Delegates to ManuAPI's <see cref="PlayerUtils.ClosestAlive"/>.
    /// Kept as a thin wrapper for call-site compatibility.
    /// </summary>
    internal static class ClosestPlayerFinder
    {
        public static bool GetClosestTarget(PlayerControl player, out PlayerControl target)
        {
            target = null;
            if (player == null || PlayerControl.GameOptions == null) return false;

            float killDistance = GameOptionsData.KillDistances[PlayerControl.GameOptions.KillDistance];
            target = PlayerUtils.ClosestAlive(player, killDistance);
            return target != null;
        }
    }
}
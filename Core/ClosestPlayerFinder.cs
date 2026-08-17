using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Shared "nearest alive player within kill range" helper, ported from Town-Of-Us'
    /// Utils.SetTarget. Used by the Sheriff today; Medic and other targeting roles will
    /// reuse it (see PORTING.md).
    /// </summary>
    internal static class ClosestPlayerFinder
    {
        /// <summary>
        /// Finds the nearest living, non-disconnected player around <paramref name="player"/>
        /// that is within the vanilla kill distance.
        /// </summary>
        public static bool GetClosestTarget(PlayerControl player, out PlayerControl target)
        {
            target = null;
            if (player == null || player.Data == null) return false;

            var origin = player.GetTruePosition();
            if (PlayerControl.GameOptions == null) return false; // lobby / pre-game frames
            var killDistance = GameOptionsData.KillDistances[PlayerControl.GameOptions.KillDistance];
            var best = float.MaxValue;

            foreach (var other in PlayerControl.AllPlayerControls)
            {
                if (other == null || other == player || other.Data == null) continue;
                if (other.Data.IsDead || other.Data.Disconnected) continue;

                var distance = Vector2.Distance(origin, other.GetTruePosition());
                if (distance <= killDistance && distance < best)
                {
                    best = distance;
                    target = other;
                }
            }

            return target != null;
        }
    }
}

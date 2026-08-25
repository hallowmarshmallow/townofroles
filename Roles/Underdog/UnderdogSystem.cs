using System;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Underdog
{
    /// <summary>
    /// Underdog gameplay logic (ported from Town-Of-Us' Underdog.cs).
    ///
    /// Passive Impostor role: while the Underdog is outnumbered (fewer alive
    /// Impostors than alive non-Impostors), their kill cooldown is reduced by the
    /// configured multiplier. The host clamps each alive Underdog's killTimer to
    /// the reduced value; when not outnumbered the vanilla cooldown applies.
    /// </summary>
    internal static class UnderdogSystem
    {
        public static bool IsUnderdog(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, UnderdogRole.Id);

        /// <summary>Host tick: apply the reduced cooldown while the Underdog is outnumbered.</summary>
        public static void Tick()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (PlayerControl.GameOptions == null) return;
            if (RoleConfig.Underdog?.Value != true) return;

            int aliveImpostors = 0;
            int aliveOthers = 0;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
                if (player.Data.myRole != null && player.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor)
                    aliveImpostors++;
                else
                    aliveOthers++;
            }
            if (aliveImpostors >= aliveOthers) return; // not outnumbered: vanilla cooldown

            var multiplier = RoleConfig.UnderdogCooldownMultiplier?.Value ?? 0.5f;
            if (multiplier <= 0f || multiplier >= 1f) return;
            var reduced = PlayerControl.GameOptions.KillCooldown * multiplier;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead) continue;
                if (!IsUnderdog(player)) continue;
                // Clamp only when the game has set a fresh cooldown above ours, so
                // we never fight the kill button's own countdown animation.
                if (player.killTimer > reduced + 0.05f)
                {
                    try { player.RpcSetKillTimer(reduced); } catch { }
                }
            }
        }

        public static void Reset() { }
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
    }
}

using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Camouflager
{
    /// <summary>
    /// Camouflager gameplay logic (ported from Town-Of-Us' Camouflager.cs).
    ///
    /// When the Camouflager uses Camouflage, every player's body turns grey for
    /// the configured duration so identities are hidden. The host validates the
    /// use, starts the timer, and broadcasts the duration; every client (host
    /// included) greys out all players while active and restores each player's
    /// own colors when it expires. Grey is applied with the game's own
    /// PlayerMaterial.SetColors so the shader stays consistent; restore re-applies
    /// each player's palette color through PlayerControl.SetPlayerMaterialColors.
    /// </summary>
    internal static class CamouflagerSystem
    {
        private const string StartRpc = "townofus.CamouflageStart";
        private const string RequestRpc = "townofus.CamouflageRequest";

        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static DateTime _camoUntil;
        private static bool _camoActive;

        public static bool IsCamouflager(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, CamouflagerRole.Id);

        /// <summary>
        /// True while the round-wide camouflage is live. Presentation layers
        /// (role name lines, Snitch red plates) must check this before writing
        /// anything into nameText — they run at 10 Hz and would otherwise
        /// overwrite the blanked names within a frame.
        /// </summary>
        internal static bool IsActive => _camoActive && DateTime.UtcNow < _camoUntil;

        internal static bool CanCamouflageNow(PlayerControl camouflager)
        {
            if (!IsCamouflager(camouflager) || camouflager.Data == null || camouflager.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(camouflager.PlayerId);
        }

        public static void TryCamouflage(PlayerControl camouflager)
        {
            var client = AmongUsClient.Instance;
            if (client == null || camouflager == null || camouflager.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestRpc, camouflager.PlayerId);
                return;
            }
            if (!CanCamouflageNow(camouflager)) return;

            var duration = RoleConfig.Seconds(RoleConfig.CamouflageDuration, 10f);
            Cooldowns[camouflager.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.CamouflageCooldown, 30f));
            _camoUntil = DateTime.UtcNow.AddSeconds(duration);
            _camoActive = true;
            ApplyCamo();
            TownOfUsRpcMux.Send(StartRpc, duration);
        }

        /// <summary>Runs every frame on every client: keep the grey while active, restore once on expiry.</summary>
        public static void Tick()
        {
            if (!_camoActive) return;
            if (DateTime.UtcNow < _camoUntil)
            {
                ApplyCamo();
                return;
            }
            RestoreAll();
            _camoActive = false;
        }

        private static void ApplyCamo()
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null) continue;
                // Blank the overhead name too (Town-Of-Us Utils.Camouflage sets
                // nameText.text = "") — grey bodies alone don't hide identities.
                try { if (player.nameText != null) player.nameText.text = string.Empty; } catch { }
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    try { PlayerMaterial.SetColors(Palette.DisabledGrey, renderer); } catch { }
                }
            }
        }

        private static void RestoreAll()
        {
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;
                var colorId = player.Data.ColorId;
                // Restore the overhead name from GameData (also undoes a blanked
                // name; Morphling-style RpcSetName changes live in the same field,
                // so Data.PlayerName is always the authoritative current value).
                try
                {
                    if (player.nameText != null && !string.IsNullOrEmpty(player.Data.PlayerName))
                        player.nameText.text = player.Data.PlayerName;
                }
                catch { }
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    try { PlayerControl.SetPlayerMaterialColors(colorId, renderer); } catch { }
                }
            }
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequest(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryCamouflage(player);
                    return;
                }
            }
        }

        [ManactorRpc(StartRpc)]
        private static void OnStart(byte senderId, float duration)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            _camoUntil = DateTime.UtcNow.AddSeconds(duration);
            _camoActive = true;
            ApplyCamo();
        }

        private static DateTime GetCooldown(byte playerId) =>
            Cooldowns.TryGetValue(playerId, out var value) ? value : DateTime.MinValue;

        public static void Reset()
        {
            // Never leave everyone grey: restore each player's own colors before
            // clearing state (meeting start / round end while camo is active).
            if (_camoActive) RestoreAll();
            Cooldowns.Clear();
            _camoActive = false;
            _camoUntil = DateTime.MinValue;
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => Reset();

    }
}

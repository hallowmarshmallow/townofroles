using System;
using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Morphling
{
    /// <summary>
    /// Morphling gameplay logic (ported from Town-Of-Us' Morphling.cs).
    ///
    /// Morph copies the nearest player's name (via the game's own RpcSetName,
    /// which mutates GameData and broadcasts — the host only) and body color
    /// (via PlayerControl.SetPlayerMaterialColors applied to the player's
    /// renderers — visual only). The host runs the revert timer from a cached
    /// copy of the Morphling's original outfit, and tells every client to
    /// recolor; the original name comes back through the host's RpcSetName.
    /// </summary>
    internal static class MorphlingSystem
    {
        private const string MorphRpc = "townofus.MorphlingMorph";
        private const string RequestMorphRpc = "townofus.MorphlingRequestMorph";
        private const string RevertRpc = "townofus.MorphlingRevert";

        private static readonly Dictionary<byte, DateTime> MorphUntil = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static readonly Dictionary<byte, (string Name, int Color)> OriginalOutfit = new();

        public static bool IsMorphling(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, MorphlingRole.Id);

        internal static bool CanMorphNow(PlayerControl morphling)
        {
            if (!IsMorphling(morphling) || morphling.Data == null || morphling.Data.IsDead) return false;
            return DateTime.UtcNow >= GetCooldown(morphling.PlayerId) &&
                   ClosestPlayerFinder.GetClosestTarget(morphling, out _);
        }

        public static void TryMorph(PlayerControl morphling)
        {
            var client = AmongUsClient.Instance;
            if (client == null || morphling == null || morphling.Data == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestMorphRpc, morphling.PlayerId);
                return;
            }
            if (!CanMorphNow(morphling)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(morphling, out var target)) return;
            if (target.Data == null) return;

            var targetName = target.Data.PlayerName;
            var targetColor = target.Data.ColorId;
            var ownName = morphling.Data.PlayerName;
            var ownColor = morphling.Data.ColorId;

            // Cache the original outfit BEFORE the name changes (RpcSetName
            // mutates Data.PlayerName, so reading it back later is wrong).
            OriginalOutfit[morphling.PlayerId] = (ownName, ownColor);
            MorphUntil[morphling.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.MorphlingMorphDuration, 10f));
            Cooldowns[morphling.PlayerId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.MorphlingMorphCooldown, 15f));

            ApplyName(morphling, targetName);
            Recolor(morphling, targetColor);
            TownOfUsRpcMux.Send(MorphRpc, morphling.PlayerId, targetName, targetColor);
            Local("You morphed into " + targetName + ".");
        }

        /// <summary>Host tick: revert any morph that has expired (from the cached outfit).</summary>
        public static void Tick()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (MorphUntil.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var key in new List<byte>(MorphUntil.Keys))
            {
                if (now < MorphUntil[key]) continue;
                var morphling = PlayerUtils.FindById(key);
                if (morphling == null || morphling.Data == null) continue;

                MorphUntil.Remove(key);
                var (ownName, ownColor) = OriginalOutfit.TryGetValue(key, out var cached)
                    ? cached
                    : (morphling.Data.PlayerName, morphling.Data.ColorId);
                OriginalOutfit.Remove(key);

                ApplyName(morphling, ownName);
                Recolor(morphling, ownColor);
                TownOfUsRpcMux.Send(RevertRpc, key, ownName, ownColor);
                return; // one revert per tick is plenty
            }
        }

        /// <summary>Host-only name change (networked). Never call on remote players from clients.</summary>
        private static void ApplyName(PlayerControl player, string name)
        {
            if (player == null || string.IsNullOrEmpty(name)) return;
            try
            {
                if (player.Data != null && player.Data.PlayerName != name)
                    player.RpcSetName(name);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Morphling name: " + e.Message);
            }
        }

        /// <summary>Visual-only body recolor (safe on any client).</summary>
        private static void Recolor(PlayerControl player, int colorId)
        {
            if (player == null) return;
            try
            {
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    try { PlayerControl.SetPlayerMaterialColors(colorId, renderer); } catch { }
                }
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Morphling recolor: " + e.Message);
            }
        }

        [ReactorRpc(RequestMorphRpc)]
        private static void OnRequestMorph(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryMorph(player);
                    return;
                }
            }
        }

        [ReactorRpc(MorphRpc)]
        private static void OnMorph(byte senderId, byte morphlingId, string targetName, int targetColor)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var morphling = PlayerUtils.FindById(morphlingId);
            if (morphling == null) return;
            // The name already arrived through the host's RpcSetName broadcast;
            // clients only need to recolor the body.
            Recolor(morphling, targetColor);
        }

        [ReactorRpc(RevertRpc)]
        private static void OnRevert(byte senderId, byte morphlingId, string ownName, int ownColor)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            var morphling = PlayerUtils.FindById(morphlingId);
            if (morphling == null) return;
            Recolor(morphling, ownColor);
        }

        private static PlayerControl PlayerUtils.FindById(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        private static DateTime GetCooldown(byte morphlingId) =>
            Cooldowns.TryGetValue(morphlingId, out var value) ? value : DateTime.MinValue;

        public static void Reset()
        {
            MorphUntil.Clear();
            Cooldowns.Clear();
            OriginalOutfit.Clear();
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => Reset();

        private static void Local(string message)
        {
            try
            {
                SystemChat.Show(message);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_MorphlingPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance == PlayerControl.LocalPlayer) MorphlingSystem.Tick();
        }
    }
}

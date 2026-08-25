using System;
using System.Collections.Generic;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Snitch
{
    /// <summary>
    /// Snitch gameplay logic (ported from Town-Of-Us' Snitch.cs).
    ///
    /// Once the Snitch has completed all their tasks, they see arrows pointing
    /// to every alive Impostor. Uses the game's own ArrowBehaviour component
    /// (which keeps the sprite near the screen edge and aims at its target);
    /// purely local to the Snitch's client — every client already knows each
    /// player's team and task progress, so no RPCs are needed.
    /// </summary>
    internal static class SnitchSystem
    {
        private static readonly Dictionary<byte, ArrowBehaviour> Arrows = new();
        // Impostors whose overhead name we tinted red (Town-Of-Us
        // SnitchMod.HighlightImpostors does arrows + red names; restore on clear).
        private static readonly HashSet<byte> NameHighlighted = new();

        public static bool IsSnitch(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, SnitchRole.Id);

        public static bool TasksComplete(PlayerControl player)
        {
            if (player == null || player.Data == null || player.Data.Tasks == null) return false;
            // All assigned tasks complete (per-player; GameData.TotalTasks is the
            // lobby-wide aggregate, not this player's workload).
            var tasks = player.Data.Tasks;
            if (tasks.Count == 0) return false;
            for (int i = 0; i < tasks.Count; i++)
                if (tasks[i] == null || !tasks[i].Complete) return false;
            return true;
        }

        /// <summary>Runs every frame on every client; only the Snitch with tasks done sees arrows.</summary>
        public static void Tick()
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) { ClearAll(); return; }
            if (!IsSnitch(local) || local.Data.IsDead) { ClearAll(); return; }
            if (MeetingHud.Instance != null || ExileController.Instance != null) { ClearAll(); return; }
            if (!TasksComplete(local)) { ClearAll(); return; }

            UpdateArrows(local);
        }

        private static void UpdateArrows(PlayerControl snitch)
        {
            // Live Impostor targets.
            var targets = new HashSet<byte>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
                if (player.Data.myRole == null || player.Data.myRole.RoleTeamType != RoleTeamTypes.Impostor) continue;
                targets.Add(player.PlayerId);
                if (Arrows.TryGetValue(player.PlayerId, out var arrow) && arrow != null)
                {
                    arrow.target = player.transform.position;
                    continue;
                }
                CreateArrow(player);
            }

            // Remove arrows for Impostors that are no longer targets.
            if (Arrows.Count > 0)
            {
                var stale = new List<byte>();
                foreach (var id in Arrows.Keys)
                    if (!targets.Contains(id)) stale.Add(id);
                for (int i = 0; i < stale.Count; i++) DestroyArrow(stale[i]);
            }

            // Red nameplates over every living Impostor (local-only visual).
            // Suppressed while a Camouflager camouflage is live — colored plates
            // would defeat the whole point of hiding identities.
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
                bool isTarget = !Camouflager.CamouflagerSystem.IsActive && targets.Contains(player.PlayerId);
                try
                {
                    if (player.nameText == null) continue;
                    player.nameText.color = isTarget ? Palette.ImpostorRed : Color.white;
                    if (isTarget) NameHighlighted.Add(player.PlayerId);
                }
                catch { }
            }
        }

        private static void CreateArrow(PlayerControl player)
        {
            try
            {
                var icon = RoleArt.Arrow;
                if (icon == null) return; // check before allocating the GameObject
                var go = new GameObject("ToU_SnitchArrow_" + player.PlayerId);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                sr.sortingOrder = 200;
                var arrow = go.AddComponent<ArrowBehaviour>();
                arrow.image = sr;
                arrow.target = player.transform.position;
                Arrows[player.PlayerId] = arrow;
            }
            catch { }
        }

        private static void DestroyArrow(byte playerId)
        {
            if (!Arrows.TryGetValue(playerId, out var arrow)) return;
            Arrows.Remove(playerId);
            if (arrow != null && arrow.gameObject != null && arrow.gameObject)
                UnityEngine.Object.Destroy(arrow.gameObject);
        }

        private static void ClearAll()
        {
            if (NameHighlighted.Count > 0)
            {
                foreach (var id in new List<byte>(NameHighlighted))
                {
                    var player = FindPlayerLocal(id);
                    if (player != null && player.nameText != null)
                    {
                        try { player.nameText.color = Color.white; } catch { }
                    }
                }
                NameHighlighted.Clear();
            }
            if (Arrows.Count == 0) return;
            foreach (var id in new List<byte>(Arrows.Keys)) DestroyArrow(id);
        }

        private static PlayerControl FindPlayerLocal(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        public static void Reset() => ClearAll();
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => Reset();
    }
}

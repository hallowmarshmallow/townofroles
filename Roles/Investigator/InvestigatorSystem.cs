using System;
using System.Collections.Generic;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Investigator
{
    /// <summary>
    /// Investigator gameplay logic (ported from Town-Of-Us' Investigator.cs).
    ///
    /// The Investigator sees the footprints of other players: every Footprint
    /// Interval, a small footprint sprite is spawned at each alive player's
    /// position, tinted with their color, fading over Footprint Duration. This is
    /// a purely local visual effect on the Investigator's own client — no RPCs
    /// needed, and nothing leaks to other players.
    /// </summary>
    internal static class InvestigatorSystem
    {
        private static readonly List<Footprint> Footprints = new();
        private static float _nextSpawn;

        public static bool IsInvestigator(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, InvestigatorRole.Id);

        /// <summary>Runs every frame on every client; only the Investigator renders footprints.</summary>
        public static void Tick()
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.IsDead) { ClearAll(); return; }
            if (!IsInvestigator(local)) { ClearAll(); return; }
            if (MeetingHud.Instance != null || ExileController.Instance != null) { ClearAll(); return; }

            var interval = RoleConfig.Seconds(RoleConfig.FootprintInterval, 3f);
            var duration = RoleConfig.Seconds(RoleConfig.FootprintDuration, 10f);
            if (Time.unscaledTime < _nextSpawn)
            {
                AgeAndCull(duration);
                return;
            }
            _nextSpawn = Time.unscaledTime + interval;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;
                if (player == local) continue;
                SpawnFootprint(player);
            }
            AgeAndCull(duration);
        }

        private static void SpawnFootprint(PlayerControl player)
        {
            try
            {
                var icon = RoleArt.Footprint;
                if (icon == null) return; // check before allocating the GameObject
                var go = new GameObject("ToU_Footprint_" + player.PlayerId);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                // AnonymousFootprints (original Town-Of-Us default): prints are
                // grey instead of tinted with the walker's color.
                if (RoleConfig.FootprintAnonymous?.Value != false)
                    sr.color = new Color(0.62f, 0.62f, 0.65f, 0.65f);
                else
                {
                    var colorId = player.Data.ColorId;
                    if (colorId >= 0 && colorId < Palette.PlayerColors.Length)
                    {
                        var c = Palette.PlayerColors[colorId];
                        sr.color = new Color(c.r, c.g, c.b, 0.65f);
                    }
                    else sr.color = new Color(0.62f, 0.62f, 0.65f, 0.65f);
                }
                sr.sortingOrder = -50; // on the ground, under players
                go.transform.position = player.GetTruePosition();
                go.transform.localScale = Vector3.one * 0.3f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                Footprints.Add(new Footprint { GameObject = go, Renderer = sr, SpawnedAt = Time.unscaledTime, BaseAlpha = sr.color.a });
            }
            catch { }
        }

        private static void AgeAndCull(float duration)
        {
            for (int i = Footprints.Count - 1; i >= 0; i--)
            {
                var fp = Footprints[i];
                if (fp.GameObject == null || !fp.GameObject) { Footprints.RemoveAt(i); continue; }
                var age = Time.unscaledTime - fp.SpawnedAt;
                if (age >= duration)
                {
                    UnityEngine.Object.Destroy(fp.GameObject);
                    Footprints.RemoveAt(i);
                    continue;
                }
                if (fp.Renderer != null)
                {
                    // Keep whichever base alpha the print spawned with (grey
                    // anonymous prints and colored prints share the fade).
                    var color = fp.Renderer.color;
                    float baseA = fp.BaseAlpha > 0.01f ? fp.BaseAlpha : 0.65f;
                    color.a = baseA * (1f - age / duration);
                    fp.Renderer.color = color;
                }
            }
        }

        private static void ClearAll()
        {
            if (Footprints.Count == 0) return;
            for (int i = 0; i < Footprints.Count; i++)
            {
                if (Footprints[i].GameObject != null && Footprints[i].GameObject)
                    UnityEngine.Object.Destroy(Footprints[i].GameObject);
            }
            Footprints.Clear();
        }

        public static void Reset() => ClearAll();
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => Reset();

        private sealed class Footprint
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public float SpawnedAt;
            public float BaseAlpha;
        }
    }
}

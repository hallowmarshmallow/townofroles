using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Creator-exclusive name color: a smooth cycling blue/pink name.
    ///
    /// Impersonation protection — Classic Us 8.9 exposes no account-bound
    /// identifier (no FriendCode / AuthId / ProductUserId), so identity is a
    /// handshake instead:
    ///   * The creator's client broadcasts a claim carrying a secret from its
    ///     cfg (townofus.CreatorClaim).
    ///   * A client only colors a player whose claim matches the secret in ITS
    ///     OWN cfg. Renaming alone can never spoof the color; only someone who
    ///     holds the same secret (e.g. a trusted lobby with your cfg) can.
    ///   * If no secret is configured anywhere, the feature falls back to the
    ///     legacy name match (purely cosmetic, spoofable by renaming).
    /// </summary>
    internal static class CreatorColor
    {
        private const string CreatorClaimRpc = "townofus.CreatorClaim";
        private const float ClaimIntervalSeconds = 15f;

        private static readonly Color Blue = new(0.30f, 0.62f, 1f, 1f);
        private static readonly Color Pink = new(1f, 0.45f, 0.62f, 1f);

        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<string> Name { get; private set; }
        public static ConfigEntry<string> Secret { get; private set; }
        public static ConfigEntry<float> Speed { get; private set; }

        private static int _claimedPlayerId = -1;
        private static DateTime _nextClaim = DateTime.MinValue;

        // Skipped while the ship/spawn transition is settling (set by GameStarted):
        // touching player bodies renderer-by-renderer on the frame players spawn
        // can fault the CLR before the scene has finished constructing them.
        private static float _settleUntil = float.MinValue;

        // Players whose body we tinted this session, so a player who stops
        // qualifying (game end / disconnect / config toggle) gets their own
        // palette colors restored instead of staying tinted forever.
        private static readonly HashSet<byte> TintedBodies = new();

        public static void Init(ConfigFile config)
        {
            Enabled = config.Bind(
                "CreatorColor", "Enabled", true,
                "Give the mod creator's in-game name a smooth cycling blue/pink color.");
            Name = config.Bind(
                "CreatorColor", "Name", "hallowmarsh",
                "Legacy (no-secret) name match. When Secret is empty, players with this name are colored — spoofable by renaming. Set Secret to turn on the verified handshake instead.");
            Secret = config.Bind(
                "CreatorColor", "Secret", "",
                "Handshake secret. When set, this client claims creator status (broadcast every 15s) and only colors players whose claim matches THIS secret. Share it only with trusted lobbies.");
            Speed = config.Bind(
                "CreatorColor", "Speed", 2.5f,
                "Cycling speed in radians per second (higher = faster blue/pink cycle).");
        }

        public static void Reset()
        {
            RestoreTintedBodies();
            _claimedPlayerId = -1;
            _nextClaim = DateTime.MinValue;
        }

        public static void OnGameStarted(GameStartedEventArgs _)
        {
            // Freeze per-frame body touching for a moment so the game can finish
            // spawning players before we tint anything.
            _settleUntil = Time.unscaledTime + 1.5f;
        }

        public static void OnGameEnded(GameEndedEventArgs _)
        {
            // PlayerIds are re-used between lobbies, so a stale claim must not
            // leak onto a fresh lobby's PlayerId.
            RestoreTintedBodies();
            _claimedPlayerId = -1;
        }

        [ReactorRpc(CreatorClaimRpc)]
        private static void OnCreatorClaimRpc(byte senderId, string secret)
        {
            var expected = Secret?.Value;
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(secret)) return;
            if (!string.Equals(expected, secret, StringComparison.Ordinal)) return;
            _claimedPlayerId = senderId;
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
        // Priority.Last: this build's HarmonyX sorts HIGHER priority postfixes
        // FIRST (PriorityComparer returns -priority.CompareTo(value)), so a High
        // creator patch would run BEFORE the Normal-priority role-presentation
        // patch and get its color overwritten every 0.1s — the stepped gradient
        // seen in-game. Last runs after everything, so the cycling color sticks.
        [HarmonyPriority(Priority.Last)]
        internal static class HudManager_Update_CreatorColorPatch
        {
            private static void Postfix()
            {
                if (Enabled?.Value != true)
                {
                    // Toggled off live: undo any body tint so nobody stays colored
                    // until the next game (RestoreUntinted is unreachable here).
                    RestoreTintedBodies();
                    return;
                }
                try
                {
                    if (Time.unscaledTime < _settleUntil) return; // spawn transition: no body touching yet
                    var speed = Mathf.Max(0.1f, Speed?.Value ?? 2.5f);
                    var t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) / 2f;
                    var color = Color.Lerp(Blue, Pink, t);

                    if (PlayerControl.LocalPlayer != null && PlayerControl.AllPlayerControls.Count > 0)
                        MaybeClaim();

                    PruneClaim();
                    ApplyColor(color);
                }
                catch
                {
                    // Cosmetic only — never let name styling crash gameplay.
                }
            }
        }

        private static void MaybeClaim()
        {
            var secret = Secret?.Value;
            if (string.IsNullOrEmpty(secret)) return;

            var now = DateTime.UtcNow;
            if (now < _nextClaim) return;
            _nextClaim = now.AddSeconds(ClaimIntervalSeconds);
            try
            {
                TownOfUsRpcMux.Send(CreatorClaimRpc, secret);
            }
            catch
            {
                // RPC not available (no lobby); retry on the next interval.
            }
        }

        private static void PruneClaim()
        {
            if (_claimedPlayerId < 0) return;
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == _claimedPlayerId) return;
            _claimedPlayerId = -1;
        }

        private static void ApplyColor(Color color)
        {
            var secret = Secret?.Value;
            var legacyName = (Name?.Value ?? string.Empty).Trim();
            var localId = PlayerControl.LocalPlayer?.PlayerId ?? -1;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.nameText == null) continue;
                if (!ShouldColor(player.PlayerId, player.Data.PlayerName, localId, secret, legacyName)) continue;
                SetColor(player.nameText, color);
                TintBody(player, color);
            }

            var meeting = MeetingHud.Instance;
            // playerStates is private in the 2026.8.9 interop.
            var states = meeting == null ? null : GameReflection.GetPlayerStates(meeting);
            if (states != null)
            {
                foreach (var area in states)
                {
                    if (area == null || area.NameText == null) continue;
                    var target = FindPlayer(area.TargetPlayerId);
                    if (target == null || target.Data == null) continue;
                    if (!ShouldColor(target.PlayerId, target.Data.PlayerName, localId, secret, legacyName)) continue;
                    SetColor(area.NameText, color);
                }
            }

            RestoreUntinted();
        }

        /// <summary>
        /// Tints the whole player body (body, hat, visor renderers) with the
        /// cycling color using the game's own PlayerMaterial.SetColors — the same
        /// path the Camouflager uses for its grey-out, so the shader stays
        /// consistent and hats/pets ride along.
        /// </summary>
        private static void TintBody(PlayerControl player, Color color)
        {
            if (player == null || player.gameObject == null) return;
            if (player.Data != null && player.Data.IsDead) return; // ghosts keep their look
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                try { PlayerMaterial.SetColors(color, renderer); } catch { }
            }
            TintedBodies.Add(player.PlayerId);
        }

        /// <summary>Restores palette colors on bodies tinted earlier that no longer qualify.</summary>
        private static void RestoreUntinted()
        {
            if (TintedBodies.Count == 0) return;
            var secret = Secret?.Value;
            var legacyName = (Name?.Value ?? string.Empty).Trim();
            var localId = PlayerControl.LocalPlayer?.PlayerId ?? -1;

            foreach (var id in new List<byte>(TintedBodies))
            {
                var player = FindPlayer(id);
                if (player == null || player.Data == null) { TintedBodies.Remove(id); continue; }
                // A dead player is no longer the visible tinted body (ghost look is
                // preserved by TintBody's dead check), so restore and converge.
                if (player.Data.IsDead) { RestoreBody(player); TintedBodies.Remove(id); continue; }
                if (ShouldColor(id, player.Data.PlayerName, localId, secret, legacyName)) continue;
                RestoreBody(player);
                TintedBodies.Remove(id);
            }
        }

        private static void RestoreTintedBodies()
        {
            if (TintedBodies.Count == 0) return;
            foreach (var id in new List<byte>(TintedBodies))
            {
                var player = FindPlayer(id);
                if (player != null) RestoreBody(player);
            }
            TintedBodies.Clear();
        }

        private static void RestoreBody(PlayerControl player)
        {
            if (player == null || player.Data == null || player.gameObject == null) return;
            var colorId = player.Data.ColorId;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                try { PlayerControl.SetPlayerMaterialColors(colorId, renderer); } catch { }
            }
        }

        private static bool ShouldColor(int playerId, string playerName, int localId, string secret, string legacyName)
        {
            var isSelf = playerId == localId;
            if (isSelf)
            {
                // You are who you are: a configured secret colors your own name
                // unconditionally; without one, the legacy name match applies.
                return !string.IsNullOrEmpty(secret)
                    || (legacyName.Length > 0
                        && string.Equals(playerName, legacyName, StringComparison.OrdinalIgnoreCase));
            }

            // Remote players: only a verified handshake claim (matching THIS
            // client's secret) or the legacy name match.
            if (playerId == _claimedPlayerId && !string.IsNullOrEmpty(secret)) return true;
            return string.IsNullOrEmpty(secret)
                && legacyName.Length > 0
                && string.Equals(playerName, legacyName, StringComparison.OrdinalIgnoreCase);
        }

        private static PlayerControl FindPlayer(byte id)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == id) return player;
            return null;
        }

        // Same interop-drift-safe reflection used by PresentationPatches:
        // the runtime TextMeshPro/TextRenderer proxies expose "color" even
        // when the compile-time interop differs.
        private static void SetColor(object renderer, Color value)
        {
            if (renderer == null) return;
            var type = renderer.GetType();
            var property = type.GetProperty("color") ?? type.GetProperty("Color");
            if (property != null && property.CanWrite)
            {
                try { property.SetValue(renderer, value, null); } catch { }
            }
        }
    }
}

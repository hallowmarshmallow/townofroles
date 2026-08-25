using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Medic
{
    internal static class MedicSystem
    {
        private const string ShieldRpc = "townofus.MedicShield";
        private const string RequestProtectRpc = "townofus.MedicRequestProtect";
        private const string BreakRpc = "townofus.MedicShieldBreak";
        private static readonly Dictionary<byte, int> UsesRemaining = new();
        private static readonly Dictionary<byte, DateTime> Cooldowns = new();
        private static readonly Dictionary<byte, byte> Shields = new();

        public static bool IsMedic(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, MedicRole.Id);

        public static void TryProtect(PlayerControl medic)
        {
            var client = AmongUsClient.Instance;
            if (client == null || medic == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestProtectRpc, medic.PlayerId);
                return;
            }
            if (!CanProtectNow(medic)) return;
            if (!ClosestPlayerFinder.GetClosestTarget(medic, out var target)) return;

            var remaining = ApplyShield(medic.PlayerId, target.PlayerId);
            TownOfUsRpcMux.Send(ShieldRpc, medic.PlayerId, target.PlayerId, remaining);
        }

        internal static bool CanProtectNow(PlayerControl medic)
        {
            if (!IsMedic(medic) || medic.Data.IsDead) return false;
            var id = medic.PlayerId;
            return GetUses(id) > 0 && DateTime.UtcNow >= GetCooldown(id);
        }

        /// <summary>medic id -> protected player id, exposed for the shield visuals.</summary>
        internal static IEnumerable<KeyValuePair<byte, byte>> ShieldPairs => Shields;

        private static int GetUses(byte medicId)
        {
            if (!UsesRemaining.TryGetValue(medicId, out var value))
            {
                value = RoleConfig.Count(RoleConfig.MedicUses);
                UsesRemaining[medicId] = value;
            }
            return value;
        }

        private static DateTime GetCooldown(byte medicId) =>
            Cooldowns.TryGetValue(medicId, out var value) ? value : DateTime.MinValue;

        private static int ApplyShield(byte medicId, byte targetId)
        {
            var remaining = Math.Max(0, GetUses(medicId) - 1);
            UsesRemaining[medicId] = remaining;
            Shields[medicId] = targetId;
            Cooldowns[medicId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.MedicCooldown));
            return remaining;
        }

        public static void OnBeforeMurder(MurderEventArgs args)
        {
            if (args?.Target == null) return;
            byte? consumedMedic = null;
            foreach (var shield in Shields)
            {
                if (shield.Value == args.Target.PlayerId)
                {
                    consumedMedic = shield.Key;
                    break;
                }
            }
            if (!consumedMedic.HasValue) return;

            args.Cancelled = true;
            if (RoleConfig.MedicShieldBreaksOnKill?.Value != false)
                Shields.Remove(consumedMedic.Value);
        }

        /// <summary>
        /// Host tick: a shield dies with its Medic or its wearer (Town-Of-Us
        /// ShowShield.cs breaks the shield when either side dies). Every client
        /// also refreshes the green shield visual from the synced state.
        /// </summary>
        public static void Tick()
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost && Shields.Count > 0)
            {
                foreach (var shield in new List<KeyValuePair<byte, byte>>(Shields))
                {
                    var medic = FindPlayer(shield.Key);
                    var target = FindPlayer(shield.Value);
                    bool medicDead = medic == null || medic.Data == null || medic.Data.IsDead;
                    bool targetDead = target == null || target.Data == null || target.Data.IsDead;
                    if (!medicDead && !targetDead) continue;
                    Shields.Remove(shield.Key);
                    TownOfUsRpcMux.Send(BreakRpc, shield.Key);
                }
            }
            MedicShieldVisuals.Sync();
        }

        [ManactorRpc(BreakRpc)]
        private static void OnShieldBreak(byte senderId, byte medicId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            Shields.Remove(medicId);
        }

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }

        /// <summary>
        /// Body Report (Town-Of-Us MedicMod/DeadBody.cs): when the Medic reports
        /// a body, they get a private clue — killer's name if very fresh,
        /// killer's darker/lighter color shade if less fresh, "suicide" for
        /// Sheriff self-kills, or "too old" past the color window.
        /// </summary>
        public static void OnAfterReport(ReportEventArgs args)
        {
            if (args == null || args.Body == null || args.Reporter == null) return;
            if (!IsMedic(args.Reporter)) return;
            if (!KillLog.TryGetLatest(args.Body.PlayerId, out var kill)) return;

            var ageSeconds = (DateTime.UtcNow - kill.Time).TotalSeconds;
            var nameWindow = RoleConfig.MedicReportNameDuration?.Value ?? 15;
            var colorWindow = RoleConfig.MedicReportColorDuration?.Value ?? 40;

            string message;
            if (ageSeconds > colorWindow)
            {
                message = $"Body Report: The corpse is too old to gain information from. (Killed {Math.Round(ageSeconds)}s ago)";
            }
            else if (kill.Killer == kill.Victim)
            {
                message = $"Body Report: The kill appears to have been a suicide! (Killed {Math.Round(ageSeconds)}s ago)";
            }
            else if (ageSeconds < nameWindow)
            {
                var killerName = FindPlayer(kill.Killer)?.Data?.PlayerName ?? "someone unknown";
                message = $"Body Report: The killer appears to be {killerName}! (Killed {Math.Round(ageSeconds)}s ago)";
            }
            else
            {
                message = $"Body Report: The killer appears to be a {ShadeOf(kill.Killer)} color. (Killed {Math.Round(ageSeconds)}s ago)";
            }

            try { SystemChat.Show(message); } catch { }
        }

        /// <summary>"darker"/"lighter" classification of a player's color (upstream table).</summary>
        private static string ShadeOf(byte playerId)
        {
            var player = FindPlayer(playerId);
            var id = player?.Data?.ColorId ?? -1;
            switch (id)
            {
                // darker shades
                case 0: case 1: case 2: case 6: case 8: case 9:
                case 12: case 18: case 19: case 21:
                    return "darker";
                default:
                    return "lighter";
            }
        }

        public static void Reset()
        {
            UsesRemaining.Clear();
            Cooldowns.Clear();
            Shields.Clear();
            MedicShieldVisuals.ResetAll();
        }

        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();

        [ManactorRpc(RequestProtectRpc)]
        private static void OnRequestProtectRpc(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != playerId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    TryProtect(player);
                    return;
                }
            }
        }

        [ManactorRpc(ShieldRpc)]
        private static void OnShieldRpc(byte senderId, byte medicId, byte targetId, int usesRemaining)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            UsesRemaining[medicId] = Math.Max(0, usesRemaining);
            Shields[medicId] = targetId;
            Cooldowns[medicId] = DateTime.UtcNow.AddSeconds(RoleConfig.Seconds(RoleConfig.MedicCooldown));
        }
    }

    /// <summary>
    /// Green shield ring around the Medic's protected player — the classic
    /// Town-Of-Us shield visual. Every client renders it locally from the
    /// synced Shields state: a soft green ring sprite generated at runtime,
    /// parented under the protected player and rendered just behind their body.
    /// </summary>
    internal static class MedicShieldVisuals
    {
        private static readonly Dictionary<byte, GameObject> Rings = new();
        private static Sprite _ringSprite;
        private static bool _spriteFailed;

        public static void Sync()
        {
            bool meetingOpen = MeetingHud.Instance != null || ExileController.Instance != null;
            var active = new HashSet<byte>();

            if (!meetingOpen && MedicSystem.ShieldPairs != null)
            {
                foreach (var pair in MedicSystem.ShieldPairs)
                {
                    var target = FindPlayer(pair.Value);
                    if (target == null || target.Data == null || target.Data.IsDead) continue;
                    active.Add(target.PlayerId);
                    EnsureRing(target);
                }
            }

            foreach (var id in new List<byte>(Rings.Keys))
            {
                var ring = Rings[id];
                if (ring == null || !ring)
                {
                    Rings.Remove(id);
                    continue;
                }
                bool shouldShow = active.Contains(id);
                if (ring.activeSelf != shouldShow) ring.SetActive(shouldShow);
            }
        }

        public static void ResetAll()
        {
            foreach (var ring in Rings.Values)
            {
                if (ring != null && ring) UnityEngine.Object.Destroy(ring);
            }
            Rings.Clear();
        }

        private static void EnsureRing(PlayerControl target)
        {
            try
            {
                if (Rings.TryGetValue(target.PlayerId, out var existing) && existing && existing.transform.parent == target.transform)
                    return;

                _ringSprite ??= BuildRingSprite();
                if (_ringSprite == null) { _spriteFailed = true; return; }
                if (_spriteFailed) return;

                var go = new GameObject("ToU_MedicShield_" + target.PlayerId);
                go.transform.SetParent(target.transform, false);
                go.transform.localPosition = new Vector3(0f, -0.22f, -0.01f);
                go.transform.localScale = Vector3.one * 0.55f;
                go.layer = target.gameObject.layer;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _ringSprite;
                renderer.sortingOrder = Mathf.Max(1, BodyOrderOf(target) - 1);

                Rings[target.PlayerId] = go;
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Medic shield visual: " + e.Message);
            }
        }

        private static int BodyOrderOf(PlayerControl player)
        {
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer == null || !renderer.sprite) continue;
                return renderer.sortingOrder;
            }
            return 5;
        }

        /// <summary>Soft anti-aliased green ring generated once per session.</summary>
        private static Sprite BuildRingSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            float radius = size * 0.42f;
            float halfWidth = size * 0.035f;
            float softness = size * 0.05f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Abs(dist - radius);
                    float alpha = Mathf.Clamp01((halfWidth + softness - t) / softness);
                    pixels[y * size + x] = new Color32(90, 255, 130, (byte)(alpha * 235));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null && player.PlayerId == playerId) return player;
            return null;
        }
    }

    // Drives the death-cleanup tick (same self-contained pattern as Spy/Morphling).
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_MedicPatch
    {
        private static void Postfix(PlayerControl __instance)
        {
            if (__instance == PlayerControl.LocalPlayer) MedicSystem.Tick();
        }
    }
}

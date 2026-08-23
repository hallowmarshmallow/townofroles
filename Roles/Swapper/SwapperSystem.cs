using System.Collections.Generic;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Swapper
{
    /// <summary>
    /// Swapper gameplay logic (ported from Town-Of-Us' Swapper.cs).
    ///
    /// The Swapper picks two players during a meeting (see SwapperMeetingUi).
    /// Votes cast for either of the two are counted for the other: the host's
    /// vote tally is a byte[] indexed by the voted player, so swapping the two
    /// tally entries reproduces the effect exactly. Selections are synced from
    /// non-host Swappers via a Reactor request RPC.
    /// </summary>
    internal static class SwapperSystem
    {
        private const string SwapRpc = "townofus.SwapperSwap";
        private const string RequestSwapRpc = "townofus.SwapperRequestSwap";
        private const byte None = 255;

        /// <summary>swapperId -> (first selected playerId, second selected playerId).</summary>
        private static readonly Dictionary<byte, (byte A, byte B)> Selections = new();

        public static bool IsSwapper(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, SwapperRole.Id);

        /// <summary>Called by the meeting UI when the local Swapper's pair changes.</summary>
        public static void UpdateSelection(byte swapperId, byte a, byte b)
        {
            var client = AmongUsClient.Instance;
            if (client == null) return;
            if (!client.AmHost)
            {
                TownOfUsRpcMux.Send(RequestSwapRpc, swapperId, a, b);
                return;
            }
            SetSelection(swapperId, a, b);
        }

        private static void SetSelection(byte swapperId, byte a, byte b)
        {
            if (a == None && b == None) Selections.Remove(swapperId);
            else Selections[swapperId] = (a, b);
        }

        public static void Reset() => Selections.Clear();
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
        public static void OnMeetingStarted(MeetingEventArgs _) => Reset();
        public static void OnMeetingEnded(MeetingEventArgs _) => Reset();

        [ReactorRpc(RequestSwapRpc)]
        private static void OnRequestSwap(byte senderId, byte swapperId, byte a, byte b)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.PlayerId != swapperId) continue;
                var owner = player.GetClient();
                if (owner != null && owner.Id == senderId)
                {
                    SetSelection(swapperId, a, b);
                    return;
                }
            }
        }

        [ReactorRpc(SwapRpc)]
        private static void OnSwap(byte senderId, byte swapperId, byte a, byte b)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            SetSelection(swapperId, a, b);
        }

        // Host-only tally swap (CalculateVotes is private; string form survives
        // interop drift — same rationale as the Mayor patch).
        [HarmonyPatch(typeof(MeetingHud), "CalculateVotes")]
        internal static class MeetingHud_CalculateVotes_SwapperPatch
        {
            private static void Postfix(MeetingHud __instance, Il2CppStructArray<byte> __result)
            {
                try
                {
                    var client = AmongUsClient.Instance;
                    if (client == null || !client.AmHost) return;
                    // playerStates is private in the 2026.8.9 interop.
                    if (__instance == null || GameReflection.GetPlayerStates(__instance) == null || __result == null) return;
                    if (Selections.Count == 0) return;

                    foreach (var pair in Selections)
                    {
                        var a = IndexOf(__instance, pair.Value.A);
                        var b = IndexOf(__instance, pair.Value.B);
                        if (a < 0 || b < 0 || a == b) continue;
                        (__result[a], __result[b]) = (__result[b], __result[a]);
                    }
                }
                catch (System.Exception e)
                {
                    BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Swapper vote swap: " + e.Message);
                }
            }

            private static int IndexOf(MeetingHud meeting, byte playerId)
            {
                if (playerId == None) return -1;
                var states = GameReflection.GetPlayerStates(meeting);
                if (states == null) return -1;
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i] != null && states[i].TargetPlayerId == playerId) return i;
                }
                return -1;
            }
        }
    }
}

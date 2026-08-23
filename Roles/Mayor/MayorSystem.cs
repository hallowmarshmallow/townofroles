using ClassicUs.ManuAPI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Mayor
{
    /// <summary>
    /// Mayor gameplay logic (ported from Town-Of-Us' Mayor.cs).
    ///
    /// The vote bank is applied host-side on MeetingHud.CalculateVotes: the
    /// tally array is indexed by the voted player (vanilla layout — length is
    /// playerStates.Length + 2, and the last slot holds skip votes), so a
    /// Postfix simply adds (VoteBank - 1) extra votes per Mayor cast. This
    /// avoids re-implementing the tally and stays correct across clients.
    ///
    /// During meetings the Mayor also gets an Abstain button (the bundled
    /// Abstain.png): pressing it casts a skip vote through the game's own
    /// CmdCastVote(253) pipeline (253 is VoteSkip in this build), so the
    /// Mayor can deliberately not vote while keeping the vote bank unused.
    /// </summary>
    internal static class MayorSystem
    {
        public static bool IsMayor(PlayerControl player) =>
            player != null && player.Data != null && RoleRegistry.IsAssigned(player, MayorRole.Id);

        public static int VoteBank => Mathf.Clamp(RoleConfig.Count(RoleConfig.MayorVoteBank, 2), 1, 15);
    }

    // "CalculateVotes" is private; the string form survives interop drift the
    // same way Jester's EndGameManager "Update" patch does (see PORTING.md).
    [HarmonyPatch(typeof(MeetingHud), "CalculateVotes")]
    internal static class MeetingHud_CalculateVotes_MayorPatch
    {
        private static void Postfix(MeetingHud __instance, Il2CppStructArray<byte> __result)
        {
            try
            {
                // The vote tally is computed host-side (the host broadcasts the
                // result via RpcVotingComplete); guard anyway so a client that
                // ever runs CalculateVotes can never double-count votes.
                var client = AmongUsClient.Instance;
                if (client == null || !client.AmHost) return;
                // playerStates is private in the 2026.8.9 interop.
                var states = GameReflection.GetPlayerStates(__instance);
                if (__instance == null || states == null || __result == null) return;
                var extra = MayorSystem.VoteBank - 1;
                if (extra <= 0) return;

                for (int i = 0; i < states.Length; i++)
                {
                    var area = states[i];
                    if (area == null || !area.DidVote) continue;
                    // 253 = VoteSkip in this build, 254 = no vote cast; anything
                    // else is the voted index.
                    if (area.VotedFor == 253 || area.VotedFor == 254) continue;
                    if (area.VotedFor >= __result.Length) continue;

                    var voter = PlayerUtils.FindById(area.TargetPlayerId);
                    if (voter == null || voter.Data == null || !MayorSystem.IsMayor(voter)) continue;

                    // Cap so an absurdly high vote bank cannot overflow a byte tally.
                    __result[area.VotedFor] = (byte)Mathf.Min(255, (int)__result[area.VotedFor] + extra);
                }
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Mayor vote bank: " + e.Message);
            }
        }

    }

    /// <summary>
    /// The Mayor's Abstain button, built into the meeting HUD with the same
    /// delegate-free ClickRouter wiring as the Assassin meeting buttons.
    /// </summary>
    internal static class MayorAbstainUi
    {
        private static GameObject _button;

        internal static void Build(MeetingHud meeting)
        {
            if (meeting == null) return;
            var mayor = PlayerControl.LocalPlayer;
            if (!MayorSystem.IsMayor(mayor) || mayor.Data == null || mayor.Data.IsDead) return;
            if (_button != null) return; // already built this meeting

            try
            {
                var hud = HudManager.Instance;
                if (hud == null || hud.KillButton == null) return;
                var clone = UnityEngine.Object.Instantiate(hud.KillButton.gameObject, hud.transform);
                clone.name = "TownOfUs_MayorAbstain";

                foreach (var comp in clone.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true))
                {
                    if (comp == null) continue;
                    if ((comp as PassiveButton) != null) continue;
                    comp.enabled = false;
                    UnityEngine.Object.Destroy(comp);
                }

                // Keep the round button background, hide the kill icon.
                var background = clone.GetComponent<SpriteRenderer>();
                if (background == null || background.sprite == null)
                {
                    foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (sr == null || sr.sprite == null) continue;
                        var size = sr.sprite.bounds.size;
                        float area = size.x * size.y;
                        if (area > 2f) { background = sr; break; }
                    }
                }
                var icon = RoleArt.Abstain;
                foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null) continue;
                    if (sr != background && icon != null) sr.sprite = icon;
                    sr.sortingOrder = 120;
                }

                // Position above the skip button (bottom-right of the meeting).
                clone.transform.localScale = Vector3.one * 0.9f;
                clone.transform.SetParent(meeting.transform, false);
                clone.transform.localPosition = new Vector3(3.4f, 2.6f, -30f);

                var passive = clone.GetComponentInChildren<PassiveButton>(true);
                if (passive != null)
                {
                    passive.gameObject.name = clone.name;
                    ClickRouter.Register(clone.name, () =>
                    {
                        try { Abstain(); }
                        catch (System.Exception e)
                        {
                            BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Mayor abstain: " + e.Message);
                        }
                    });
                }

                _button = clone;
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Mayor abstain button: " + e.Message);
            }
        }

        private static void Abstain()
        {
            var local = PlayerControl.LocalPlayer;
            var meeting = MeetingHud.Instance;
            if (local == null || meeting == null) return;
            // 253 = VoteSkip in this build (matches SkipVoteButton.TargetPlayerId).
            meeting.CmdCastVote(local.PlayerId, 253);
            TryDestroy();
        }

        internal static void Clear()
        {
            ClickRouter.Unregister("TownOfUs_MayorAbstain");
            TryDestroy();
        }

        private static void TryDestroy()
        {
            if (_button != null)
            {
                try { UnityEngine.Object.Destroy(_button); } catch { }
                _button = null;
            }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), "Start")]
    internal static class MeetingHud_Start_MayorAbstainPatch
    {
        private static void Postfix(MeetingHud __instance)
        {
            try { MayorAbstainUi.Build(__instance); }
            catch (System.Exception e) { BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Mayor abstain start: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), "Confirm")]
    internal static class MeetingHud_Confirm_MayorAbstainPatch
    {
        private static void Prefix() => MayorAbstainUi.Clear();
    }

    [HarmonyPatch(typeof(MeetingHud), "VotingComplete")]
    internal static class MeetingHud_VotingComplete_MayorAbstainPatch
    {
        private static void Postfix() => MayorAbstainUi.Clear();
    }
}

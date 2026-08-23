using System;
using System.Collections.Generic;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Roles.Assassin;
using TownOfUs.ManuAPI.Roles.Modifiers;
using TownOfUs.ManuAPI.Roles.Seer;

namespace TownOfUs.ManuAPI.Core
{
    internal static class PresentationPatches
    {
        internal static float NextUpdate { get; set; }
        internal static float NextMeetingUpdate { get; set; }
        private static bool _loggedMissingStates;

        internal static void UpdateWorld()
        {
            if (RoleConfig.PresentationEnabled?.Value != true) return;
            var viewer = PlayerControl.LocalPlayer;
            if (viewer == null || viewer.Data == null) return;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null || player.nameText == null) continue;
                if (SeerSystem.TryGetReveal(viewer, player, out var revealText, out var revealColor))
                {
                    // A Seer's investigation result for this target replaces the
                    // target's own role line (only the Seer sees it).
                    SetText(player.nameText, RolePresentation.WithRole(player.Data.PlayerName, revealText));
                    SetColor(player.nameText, revealColor);
                }
                else if (RolePresentation.TryGet(player, out var roleName, out var roleColor) && RolePresentation.CanSee(viewer, player))
                {
                    // The local player also sees their own modifiers next to the role.
                    if (player == viewer)
                    {
                        var mods = ModifierSystem.NamesFor(player.PlayerId);
                        if (mods.Length > 0)
                            roleName = roleName.Length > 0 ? roleName + " [" + mods + "]" : mods;
                    }
                    SetText(player.nameText, RolePresentation.WithRole(player.Data.PlayerName, roleName));
                    SetColor(player.nameText, roleColor);
                }
                else
                {
                    RestoreIfInjected(player.nameText, player.Data.PlayerName);
                }
            }
        }

        internal static void UpdateMeeting(MeetingHud meeting)
        {
            if (meeting == null) return;
            // playerStates is private in the 2026.8.9 interop.
            var states = GameReflection.GetPlayerStates(meeting);
            if (states == null)
            {
                if (!_loggedMissingStates)
                {
                    _loggedMissingStates = true;
                    BepInEx.Logging.Logger.CreateLogSource("TownOfUs")
                        .LogWarning("Meeting role names unavailable: MeetingHud.playerStates field not found (interop drift).");
                }
                return;
            }
            var viewer = PlayerControl.LocalPlayer;
            if (viewer == null || viewer.Data == null) return;

            // Only show role names while the meeting is still in Discussion or
            // waiting for votes (NotVoted). Once votes start tallying/shown
            // (Voted/Results/Proceeding) the game draws its own "voted for"
            // indicators over the name plate, so we restore the plain player
            // name to avoid the role line colliding with them.
            var state = GameReflection.GetMeetingState(meeting);
            var showRoles = state == MeetingHud.VoteStates.Discussion || state == MeetingHud.VoteStates.NotVoted;

            foreach (var area in states)
            {
                if (area == null || area.NameText == null) continue;
                var target = PlayerUtils.FindById(area.TargetPlayerId);
                if (target == null || target.Data == null) continue;

                if (showRoles && SeerSystem.TryGetReveal(viewer, target, out var revealText, out var revealColor))
                {
                    // A Seer's investigation result for this target replaces the
                    // target's own role line (only the Seer sees it).
                    SetText(area.NameText, RolePresentation.WithRole(target.Data.PlayerName, revealText));
                    SetColor(area.NameText, revealColor);
                    SetMeetingTypography(area.NameText, 1.18f);
                }
                else if (showRoles && RolePresentation.TryGet(target, out var roleName, out var roleColor) && RolePresentation.CanSee(viewer, target))
                {
                    // The local player also sees their own modifiers next to the role.
                    if (target == viewer)
                    {
                        var mods = ModifierSystem.NamesFor(target.PlayerId);
                        if (mods.Length > 0)
                            roleName = roleName.Length > 0 ? roleName + " [" + mods + "]" : mods;
                    }
                    SetText(area.NameText, RolePresentation.WithRole(target.Data.PlayerName, roleName));
                    SetColor(area.NameText, roleColor);
                    SetMeetingTypography(area.NameText, 1.18f);
                }
                else
                {
                    RestoreIfInjected(area.NameText, target.Data.PlayerName);
                    SetMeetingTypography(area.NameText, 1f);
                }
            }
        }

        internal static void Reset()
        {
            NextUpdate = 0f;
            NextMeetingUpdate = 0f;
        }

        private static void SetText(object renderer, string value)
        {
            if (renderer == null) return;
            var type = renderer.GetType();
            var property = type.GetProperty("text") ?? type.GetProperty("Text");
            if (property != null && property.CanWrite)
            {
                try { property.SetValue(renderer, value, null); } catch { }
            }
        }

        private static void RestoreIfInjected(object renderer, string nativeName)
        {
            if (renderer == null) return;
            var type = renderer.GetType();
            var property = type.GetProperty("text") ?? type.GetProperty("Text");
            if (property == null || !property.CanRead || !property.CanWrite) return;
            try
            {
                var current = property.GetValue(renderer, null) as string;
                if (current != null && current.Contains("\n")) property.SetValue(renderer, nativeName, null);
            }
            catch { }
        }

        private static void SetMeetingTypography(object renderer, float scale)
        {
            try
            {
                var component = renderer as UnityEngine.Component;
                if (component != null) component.transform.localScale = Vector3.one * scale;

                var type = renderer?.GetType();
                var richText = type?.GetProperty("richText") ?? type?.GetProperty("RichText");
                if (richText != null && richText.CanWrite) richText.SetValue(renderer, true, null);

                var autoSize = type?.GetProperty("enableAutoSizing") ?? type?.GetProperty("EnableAutoSizing");
                if (autoSize != null && autoSize.CanWrite) autoSize.SetValue(renderer, false, null);

            }
            catch { }
        }

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

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    internal static class HudManager_Update_PresentationPatch
    {
        private static void Postfix(HudManager __instance)
        {
            if (RoleConfig.PresentationEnabled?.Value != true) return;
            if (Time.unscaledTime < PresentationPatches.NextUpdate) return;
            PresentationPatches.NextUpdate = Time.unscaledTime + 0.1f;
            try
            {
                PresentationPatches.UpdateWorld();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Role presentation update: " + e.Message);
            }
        }
    }

    // Meeting name plates are driven from MeetingHud.Update rather than
    // HudManager.Update: this is the loop that is guaranteed to run for the
    // whole meeting (HudManager.Update can be starved during meeting cutscenes
    // on some builds), and it also lets us gate on the meeting vote state.
    [HarmonyPatch(typeof(MeetingHud), "Update")]
    internal static class MeetingHud_Update_PresentationPatch
    {
        private static void Postfix(MeetingHud __instance)
        {
            if (RoleConfig.PresentationEnabled?.Value != true) return;
            if (Time.unscaledTime < PresentationPatches.NextMeetingUpdate) return;
            PresentationPatches.NextMeetingUpdate = Time.unscaledTime + 0.1f;
            try
            {
                PresentationPatches.UpdateMeeting(__instance);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Meeting role presentation update: " + e.Message);
            }
        }
    }
}

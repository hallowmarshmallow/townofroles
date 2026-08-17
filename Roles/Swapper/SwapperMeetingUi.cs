using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Swapper
{
    /// <summary>
    /// Meeting UI for the Swapper: one toggle button beside each player row.
    /// Clicking fills the pair (first click = A, second = B, third on an
    /// already-selected row clears it). The A / B labels are rendered with a
    /// clone of the row's name text. All clicks route through ClickRouter
    /// (delegate-free, like the Assassin meeting buttons).
    /// </summary>
    internal static class SwapperMeetingUi
    {
        private const byte None = 255;

        private sealed class Entry
        {
            public GameObject Button;
            public Component Label;
        }

        private static readonly Dictionary<byte, Entry> Entries = new();
        private static byte _first = None;
        private static byte _second = None;

        internal static void Build(MeetingHud meeting)
        {
            var swapper = PlayerControl.LocalPlayer;
            if (meeting == null || swapper == null || swapper.Data == null || !SwapperSystem.IsSwapper(swapper) || swapper.Data.IsDead) return;

            Clear();
            _first = None;
            _second = None;
            // playerStates is private in the 2026.8.9 interop — reflection adapter.
            var states = GameReflection.GetPlayerStates(meeting);
            if (states == null) return;

            foreach (var area in states)
            {
                if (area == null) continue;
                var targetId = area.TargetPlayerId;
                if (targetId == swapper.PlayerId) continue;
                AddButton(area, targetId);
            }
            RefreshLabels();
        }

        internal static void Clear()
        {
            foreach (var entry in Entries.Values)
            {
                if (entry.Button != null) ClickRouter.Unregister(entry.Button.name);
                if (entry.Button != null) UnityEngine.Object.Destroy(entry.Button);
                if (entry.Label != null) UnityEngine.Object.Destroy(entry.Label.gameObject);
            }
            Entries.Clear();
            _first = None;
            _second = None;
        }

        private static void AddButton(PlayerVoteArea area, byte targetId)
        {
            try
            {
                if (area.Buttons == null || area.Buttons.transform.childCount == 0) return;
                var template = area.Buttons.transform.GetChild(0).gameObject;
                if (template == null) return;

                var button = UnityEngine.Object.Instantiate(template, area.transform);
                button.name = "TownOfUs_SwapperSwap_" + targetId;
                button.transform.localPosition = new Vector3(-0.35f, 0.15f, -2f);
                button.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                SetSprite(button, RoleArt.SwapperSwitch);
                ConfigureButton(button);
                SetClick(button, targetId);

                // A / B label overlay (clone of the row's name text).
                Component label = null;
                if (area.NameText != null)
                {
                    var clone = UnityEngine.Object.Instantiate(area.NameText.gameObject, area.transform);
                    clone.name = "TownOfUs_SwapperLabel_" + targetId;
                    clone.transform.localPosition = new Vector3(-0.35f, -0.15f, -2.2f);
                    label = clone.GetComponent<TextRenderer>();
                    if (label == null) label = clone.GetComponentInChildren<TextRenderer>(true);
                }

                Entries[targetId] = new Entry { Button = button, Label = label };
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Swapper meeting button: " + e.Message);
            }
        }

        private static void Toggle(byte targetId)
        {
            if (targetId == _first)
            {
                _first = None;
            }
            else if (targetId == _second)
            {
                _second = None;
            }
            else if (_first == None)
            {
                _first = targetId;
            }
            else if (_second == None)
            {
                _second = targetId;
            }
            else
            {
                Local("Swapper: both swap slots are full — clear one first.");
                return;
            }

            RefreshLabels();
            var swapper = PlayerControl.LocalPlayer;
            if (swapper != null && swapper.Data != null) SwapperSystem.UpdateSelection(swapper.PlayerId, _first, _second);
        }

        private static void RefreshLabels()
        {
            foreach (var pair in Entries)
            {
                var label = pair.Value.Label;
                if (label == null) continue;
                string text = pair.Key == _first ? "A" : pair.Key == _second ? "B" : string.Empty;
                var property = label.GetType().GetProperty("text") ?? label.GetType().GetProperty("Text");
                if (property != null && property.CanWrite)
                {
                    try { property.SetValue(label, text, null); } catch { }
                }
            }
        }

        private static void ConfigureButton(GameObject button)
        {
            if (button == null) return;
            button.layer = 5;
            var collider = button.GetComponent<BoxCollider2D>();
            var renderer = button.GetComponent<SpriteRenderer>() ?? button.GetComponentInChildren<SpriteRenderer>(true);
            if (collider != null && renderer != null)
            {
                collider.size = renderer.sprite != null ? renderer.sprite.bounds.size : new Vector2(0.5f, 0.3f);
                collider.offset = Vector2.zero;
            }
        }

        private static void SetSprite(GameObject button, Sprite sprite)
        {
            if (button == null || sprite == null) return;
            var renderer = button.GetComponent<SpriteRenderer>() ?? button.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null) renderer.sprite = sprite;
        }

        private static void SetClick(GameObject button, byte targetId)
        {
            var passive = button.GetComponent<PassiveButton>() ?? button.GetComponentInChildren<PassiveButton>(true);
            if (passive == null || button == null) return;
            passive.gameObject.name = button.name;
            ClickRouter.Register(button.name, () =>
            {
                try { Toggle(targetId); }
                catch (Exception e) { BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Swapper click: " + e.Message); }
            });
        }

        private static void Local(string message)
        {
            try
            {
                SystemChat.Show(message);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), "Start")]
    internal static class MeetingHud_Start_SwapperPatch
    {
        private static void Postfix(MeetingHud __instance)
        {
            try { SwapperMeetingUi.Build(__instance); }
            catch (Exception e) { BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Swapper meeting start: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), "Confirm")]
    internal static class MeetingHud_Confirm_SwapperPatch
    {
        private static void Prefix() => SwapperMeetingUi.Clear();
    }

    [HarmonyPatch(typeof(MeetingHud), "VotingComplete")]
    internal static class MeetingHud_VotingComplete_SwapperPatch
    {
        private static void Postfix() => SwapperMeetingUi.Clear();
    }
}

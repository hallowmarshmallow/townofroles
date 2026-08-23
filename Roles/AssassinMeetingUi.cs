using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Assets;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Assassin
{
    internal static class AssassinMeetingUi
    {
        private sealed class Entry
        {
            public GameObject Cycle;
            public GameObject Guess;
            public Component GuessText;
            public int GuessIndex = -1;
        }

        private static readonly Dictionary<byte, Entry> Entries = new();
        private static string[] Roles => AssassinSystem.GuessableRoles;

        internal static void Build(MeetingHud meeting)
        {
            if (!AssassinSettingsSync.ActiveMeetingUi || meeting == null) return;
            var assassin = PlayerControl.LocalPlayer;
            if (!AssassinSystem.IsAssassin(assassin) || assassin.Data.IsDead) return;

            Clear();
            // playerStates is private in the 2026.8.9 interop — reflection adapter.
            var states = GameReflection.GetPlayerStates(meeting);
            if (states == null) return;
            foreach (var area in states)
            {
                if (area == null) continue;
                var target = PlayerUtils.FindById(area.TargetPlayerId);
                if (!AssassinSystem.IsEligibleTarget(assassin, target)) continue;
                AddButtons(area, target);
            }
        }

        internal static void Clear()
        {
            foreach (var entry in Entries.Values)
            {
                UnregisterButton(entry.Cycle);
                UnregisterButton(entry.Guess);
                Destroy(entry.Cycle);
                Destroy(entry.Guess);
                if (entry.GuessText != null) Destroy(entry.GuessText.gameObject);
            }
            Entries.Clear();
        }

        private static void AddButtons(PlayerVoteArea area, PlayerControl target)
        {
            try
            {
                if (area.Buttons == null || area.Buttons.transform.childCount == 0) return;
                var template = area.Buttons.transform.GetChild(0).gameObject;
                if (template == null) return;
                var cycle = UnityEngine.Object.Instantiate(template, area.transform);
                cycle.name = "TownOfUs_AssassinCycle_" + target.PlayerId;
                cycle.transform.localPosition = new Vector3(-0.35f, 0.15f, -2f);
                cycle.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                SetSprite(cycle, RoleArt.Cycle);
                ConfigureButton(cycle);
                SetClick(cycle, () => Cycle(target.PlayerId));

                var guess = UnityEngine.Object.Instantiate(template, area.transform);
                guess.name = "TownOfUs_AssassinGuess_" + target.PlayerId;
                guess.transform.localPosition = new Vector3(-0.35f, -0.15f, -2f);
                guess.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                SetSprite(guess, RoleArt.Guess);
                ConfigureButton(guess);
                SetClick(guess, () => Guess(target.PlayerId));

                var entry = new Entry { Cycle = cycle, Guess = guess };
                Entries[target.PlayerId] = entry;
                SetGuessText(area, entry, "Guess");
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Assassin meeting button: " + e.Message);
            }
        }

        private static void Cycle(byte targetId)
        {
            if (!Entries.TryGetValue(targetId, out var entry)) return;
            entry.GuessIndex = (entry.GuessIndex + 1) % Roles.Length;
            SetGuessText(null, entry, Roles[entry.GuessIndex]);
            Local("Assassin guess for player " + targetId + ": " + Roles[entry.GuessIndex]);
        }

        private static void Guess(byte targetId)
        {
            if (!Entries.TryGetValue(targetId, out var entry) || entry.GuessIndex < 0)
            {
                Local("Cycle to a role before pressing Guess.");
                return;
            }

            var target = PlayerUtils.FindById(targetId);
            if (AssassinSystem.TryGuessTarget(PlayerControl.LocalPlayer, target, Roles[entry.GuessIndex]))
            {
                if (!AssassinSettingsSync.ActiveMultiKill)
                    Clear();
                else
                {
                    UnregisterButton(entry.Cycle);
                    UnregisterButton(entry.Guess);
                    Destroy(entry.Cycle);
                    Destroy(entry.Guess);
                    Entries.Remove(targetId);
                }
            }
        }

        private static void SetGuessText(PlayerVoteArea area, Entry entry, string value)
        {
            if (entry == null) return;
            if (entry.GuessText == null && area != null && area.NameText != null)
            {
                var clone = UnityEngine.Object.Instantiate(area.NameText.gameObject, area.transform);
                clone.name = "TownOfUs_AssassinGuessText";
                clone.transform.localPosition = new Vector3(0.55f, -0.12f, -0.1f);
                entry.GuessText = clone.GetComponent<TextRenderer>();
                if (entry.GuessText == null) entry.GuessText = clone.GetComponentInChildren<TextRenderer>(true);
            }
            if (entry.GuessText == null) return;
            var property = entry.GuessText.GetType().GetProperty("text") ?? entry.GuessText.GetType().GetProperty("Text");
            if (property != null && property.CanWrite)
            {
                try { property.SetValue(entry.GuessText, value, null); } catch { }
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

        private static void SetClick(GameObject button, Action action)
        {
            var passive = button.GetComponent<PassiveButton>() ?? button.GetComponentInChildren<PassiveButton>(true);
            if (passive == null || button == null) return;
            // Delegate-free click dispatch (see ClickRouter): name this button's
            // PassiveButton GameObject with the unique id and route the native
            // ReceiveClickDown pipeline through the ClickRouter. OnClick is
            // deliberately left untouched — marshalling a managed UnityAction
            // triggers the game's protection.
            passive.gameObject.name = button.name;
            ClickRouter.Register(button.name, () =>
            {
                try { action(); }
                catch (Exception e) { BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Assassin click: " + e.Message); }
            });
        }

        private static void UnregisterButton(GameObject button)
        {
            if (button != null) ClickRouter.Unregister(button.name);
        }

        private static void Destroy(GameObject gameObject)
        {
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
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
    internal static class MeetingHud_Start_AssassinPatch
    {
        private static void Postfix(MeetingHud __instance)
        {
            try { AssassinMeetingUi.Build(__instance); } catch (Exception e) { BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Assassin meeting start: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), "Confirm")]
    internal static class MeetingHud_Confirm_AssassinPatch
    {
        private static void Prefix() => AssassinMeetingUi.Clear();
    }

    [HarmonyPatch(typeof(MeetingHud), "VotingComplete")]
    internal static class MeetingHud_VotingComplete_AssassinPatch
    {
        private static void Postfix() => AssassinMeetingUi.Clear();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Page swap for Classic Us' native customize/settings window.
    ///
    /// Clicking the corner arrow hides the game's own settings rows and spawns
    /// a native-styled page in their place: faction headers (clones of the
    /// game's own roleOptionPrefab) for Crewmate / Impostor / Neutral, each
    /// followed by one native count row (keyvaluePrefab NumberOption clone)
    /// per enabled role, with working + / - buttons. Everything is a clone of
    /// the game's own row prefabs, so the typography and layout match the
    /// native menu exactly — no overlay window on top, no managed delegates
    /// (all clicks route through ClickRouter).
    ///
    /// A role's config rows (cooldown, uses, toggles — see GameConfigOverlay.
    /// Extras) render below the role only while its count is &gt;= 1: at 0 the
    /// row is collapsed, and stepping the count past the 0 &lt;-&gt; 1 boundary
    /// rebuilds the page so the config expands/collapses in place.
    ///
    /// Rows are parented directly under menu.menu.transform (not a container)
    /// so SettingsScroll's wheel handler shifts them like the game's own rows,
    /// which matters once config rows make the page taller than the viewport.
    /// Clicking the arrow again restores the game's rows.
    /// </summary>
    internal static class NativeRolesPage
    {
        private static readonly Color CrewColor = new(0.35f, 0.8f, 1f, 1f);
        private static readonly Color ImpColor = new(1f, 0.35f, 0.35f, 1f);
        private static readonly Color NeutralColor = new(1f, 0.8f, 0.3f, 1f);

        private struct RoleDef
        {
            public string Key;      // RoleSettingsSync channel prefix, e.g. "Sheriff"
            public string Display;  // Label shown in the menu
            public Color Color;     // Label tint (faction color)
            public Func<bool> Enabled; // cfg toggle for the role
        }

        private static readonly (string Header, Color Color, RoleDef[] Roles)[] Factions =
        {
            ("Crewmate Roles", CrewColor, new[]
            {
                new RoleDef { Key = "Sheriff", Display = "Sheriff", Color = CrewColor, Enabled = () => RoleConfig.Sheriff?.Value == true },
                new RoleDef { Key = "Engineer", Display = "Engineer", Color = CrewColor, Enabled = () => RoleConfig.Engineer?.Value == true },
                new RoleDef { Key = "Medic", Display = "Medic", Color = CrewColor, Enabled = () => RoleConfig.Medic?.Value == true },
                new RoleDef { Key = "Seer", Display = "Seer", Color = CrewColor, Enabled = () => RoleConfig.Seer?.Value == true },
                new RoleDef { Key = "Vigilante", Display = "Vigilante", Color = CrewColor, Enabled = () => RoleConfig.Vigilante?.Value == true },
                new RoleDef { Key = "Altruist", Display = "Altruist", Color = CrewColor, Enabled = () => RoleConfig.Altruist?.Value == true },
                new RoleDef { Key = "Mayor", Display = "Mayor", Color = CrewColor, Enabled = () => RoleConfig.Mayor?.Value == true },
                new RoleDef { Key = "Swapper", Display = "Swapper", Color = CrewColor, Enabled = () => RoleConfig.Swapper?.Value == true },
                new RoleDef { Key = "Spy", Display = "Spy", Color = CrewColor, Enabled = () => RoleConfig.Spy?.Value == true },
                new RoleDef { Key = "Investigator", Display = "Investigator", Color = CrewColor, Enabled = () => RoleConfig.Investigator?.Value == true },
                new RoleDef { Key = "TimeLord", Display = "Time Lord", Color = CrewColor, Enabled = () => RoleConfig.TimeLord?.Value == true },
                new RoleDef { Key = "Snitch", Display = "Snitch", Color = CrewColor, Enabled = () => RoleConfig.Snitch?.Value == true },
            }),
            ("Impostor Roles", ImpColor, new[]
            {
                new RoleDef { Key = "Assassin", Display = "Assassin", Color = ImpColor, Enabled = () => RoleConfig.Assassin?.Value == true },
                new RoleDef { Key = "Janitor", Display = "Janitor", Color = ImpColor, Enabled = () => RoleConfig.Janitor?.Value == true },
                new RoleDef { Key = "Morphling", Display = "Morphling", Color = ImpColor, Enabled = () => RoleConfig.Morphling?.Value == true },
                new RoleDef { Key = "Camouflager", Display = "Camouflager", Color = ImpColor, Enabled = () => RoleConfig.Camouflager?.Value == true },
                new RoleDef { Key = "Swooper", Display = "Swooper", Color = ImpColor, Enabled = () => RoleConfig.Swooper?.Value == true },
                new RoleDef { Key = "Underdog", Display = "Underdog", Color = ImpColor, Enabled = () => RoleConfig.Underdog?.Value == true },
                new RoleDef { Key = "Undertaker", Display = "Undertaker", Color = ImpColor, Enabled = () => RoleConfig.Undertaker?.Value == true },
                new RoleDef { Key = "Miner", Display = "Miner", Color = ImpColor, Enabled = () => RoleConfig.Miner?.Value == true },
            }),
            ("Neutral Roles", NeutralColor, new[]
            {
                new RoleDef { Key = "Jester", Display = "Jester", Color = NeutralColor, Enabled = () => RoleConfig.Jester?.Value == true },
                new RoleDef { Key = "Executioner", Display = "Executioner", Color = NeutralColor, Enabled = () => RoleConfig.Executioner?.Value == true },
                new RoleDef { Key = "Arsonist", Display = "Arsonist", Color = NeutralColor, Enabled = () => RoleConfig.Arsonist?.Value == true },
                new RoleDef { Key = "Phantom", Display = "Phantom", Color = NeutralColor, Enabled = () => RoleConfig.Phantom?.Value == true },
                new RoleDef { Key = "Shifter", Display = "Shifter", Color = NeutralColor, Enabled = () => RoleConfig.Shifter?.Value == true },
                new RoleDef { Key = "Glitch", Display = "The Glitch", Color = NeutralColor, Enabled = () => RoleConfig.Glitch?.Value == true },
            }),
            ("Modifiers", NeutralColor, new[]
            {
                new RoleDef { Key = "Modifiers.Torch", Display = "Torch", Color = NeutralColor, Enabled = () => true },
                new RoleDef { Key = "Modifiers.Diseased", Display = "Diseased", Color = NeutralColor, Enabled = () => true },
                new RoleDef { Key = "Modifiers.Flash", Display = "Flash", Color = NeutralColor, Enabled = () => true },
                new RoleDef { Key = "Modifiers.Tiebreaker", Display = "Tiebreaker", Color = NeutralColor, Enabled = () => true },
                new RoleDef { Key = "Modifiers.Drunk", Display = "Drunk", Color = NeutralColor, Enabled = () => true },
                new RoleDef { Key = "Modifiers.Giant", Display = "Giant", Color = NeutralColor, Enabled = () => true },
                new RoleDef { Key = "Modifiers.ButtonBarry", Display = "Button Barry", Color = NeutralColor, Enabled = () => true },
            }),
        };

        private const string PagePrefix = "ToU_Page_";

        private static SettingMenu _menu;
        private static readonly List<string> _ids = new();
        private static bool _active;

        public static bool IsActive => _active;

        /// <summary>Drops the page for a destroyed window (called when a fresh window instance is detected).</summary>
        public static void Reset()
        {
            _active = false;
            if (_menu != null)
            {
                DestroyPageRows(_menu);
                _menu = null;
            }
            ClickRouter.UnregisterAll(_ids);
            _ids.Clear();
        }

        public static void Toggle(SettingMenu menu)
        {
            try
            {
                if (_active) Hide();
                else Show(menu);
            }
            catch (Exception e)
            {
                Log("toggle: " + e.Message);
                Reset();
            }
        }

        /// <summary>
        /// Called from the SettingMenu.OnEnable postfix. The game destroys and
        /// rebuilds its rows on every OnEnable (including returning to the Game
        /// tab), so an active page must be re-applied on top of the fresh rows.
        /// </summary>
        public static void OnMenuEnabled(SettingMenu menu)
        {
            if (!_active || menu == null) return;
            try
            {
                Hide();
                Show(menu);
            }
            catch (Exception e)
            {
                Log("re-sync: " + e.Message);
                Reset();
            }
        }

        private static void Show(SettingMenu menu)
        {
            if (menu == null || menu.menu == null || menu.menu.transform == null) return;
            if (menu.menu.gameObject != null && !menu.menu.gameObject.activeInHierarchy) return; // Game tab must be on screen
            Hide();

            var keyTemplate = menu.keyvaluePrefab;
            if (keyTemplate == null) keyTemplate = FindNumberOptionRow(menu);
            var headerTemplate = menu.roleOptionPrefab;
            // Resolve templates BEFORE hiding anything: if neither exists we bail
            // with the window untouched instead of leaving it stuck row-less.
            if (keyTemplate == null && headerTemplate == null) return;

            _menu = menu;

            // "Clear the elements except for the window": hide the native rows
            // (kept intact; they are restored on Hide).
            if (menu.AllItems != null)
            {
                for (int i = 0; i < menu.AllItems.Count; i++)
                {
                    var t = menu.AllItems.get_Item(i);
                    if (t != null && t.gameObject != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
                }
            }

            BuildContent(menu);
            _active = true;
            Log("roles page shown");
        }

        /// <summary>
        /// Builds every row of the roles page as a direct child of
        /// menu.menu.transform (so SettingsScroll scrolls them). Also called by
        /// RebuildContent so config rows collapse (count 0) and expand (count
        /// &gt;= 1) without touching the hidden native rows.
        /// </summary>
        private static void BuildContent(SettingMenu menu)
        {
            var keyTemplate = menu.keyvaluePrefab;
            if (keyTemplate == null) keyTemplate = FindNumberOptionRow(menu);
            var headerTemplate = menu.roleOptionPrefab;
            if (keyTemplate == null && headerTemplate == null) return;

            var parent = menu.menu.transform;
            float step = menu.YOffset > 0f ? menu.YOffset : 0.45f;
            float y = ComputeStartY(menu);

            for (int f = 0; f < Factions.Length; f++)
            {
                var faction = Factions[f];
                if (!HasEnabled(faction.Roles)) continue;

                if (headerTemplate != null)
                {
                    BuildHeader(headerTemplate, parent, faction.Header, faction.Color, y);
                    y -= step;
                }

                if (faction.Header == "Modifiers")
                {
                    // Modifiers render as an On/Off toggle row + a Chance row
                    // (they have probability, not a count).
                    for (int r = 0; r < faction.Roles.Length; r++)
                    {
                        var role = faction.Roles[r];
                        if (keyTemplate != null)
                        {
                            BuildConfigRow(keyTemplate, parent, role.Key + ".Enabled", role.Display, "bool", y);
                            y -= step;
                            BuildConfigRow(keyTemplate, parent, role.Key + ".Probability", "Chance", "percent", y);
                            y -= step;
                        }
                    }
                    continue;
                }

                for (int r = 0; r < faction.Roles.Length; r++)
                {
                    var role = faction.Roles[r];
                    if (role.Enabled == null || !role.Enabled()) continue;
                    if (keyTemplate != null) BuildCountRow(keyTemplate, parent, role, y);
                    y -= step;

                    // The role's own config only exists while its count is >= 1:
                    // at 0 the row is collapsed, and crossing to 1 expands it.
                    if (RoleSettingsSync.GetInt(role.Key + ".Count") >= 1
                        && GameConfigOverlay.Extras.TryGetValue(role.Key, out var extras))
                    {
                        for (int e = 0; e < extras.Length; e++)
                        {
                            if (keyTemplate != null)
                                BuildConfigRow(keyTemplate, parent, role.Key + "." + extras[e].Field,
                                    extras[e].Label, extras[e].Kind, y);
                            y -= step;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Rebuilds the page content after a count change so per-role config
        /// rows expand/collapse at the 0 &lt;-&gt; 1 boundary. Rows are recreated
        /// (same names), so ClickRouter registrations are refreshed too.
        /// </summary>
        private static void RebuildContent()
        {
            if (_menu == null) return;
            try
            {
                ClickRouter.UnregisterAll(_ids);
                _ids.Clear();
                DestroyPageRows(_menu);
                BuildContent(_menu);
            }
            catch (Exception e)
            {
                Log("rebuild: " + e.Message);
            }
        }

        private static void Hide()
        {
            _active = false;
            ClickRouter.UnregisterAll(_ids);
            _ids.Clear();
            if (_menu != null)
            {
                // Deactivate before Destroy: Destroy is deferred to end of frame,
                // so without this the old rows stay rendered/clickable for one
                // frame while the native rows are already restored (flash).
                DestroyPageRows(_menu);

                // Restore the native rows (they were only hidden, never destroyed).
                if (_menu.AllItems != null)
                {
                    for (int i = 0; i < _menu.AllItems.Count; i++)
                    {
                        var t = _menu.AllItems.get_Item(i);
                        if (t != null && t.gameObject != null && !t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    }
                }
                _menu = null;
            }
            Log("roles page hidden");
        }

        /// <summary>Destroys every row this page created under the menu.</summary>
        private static void DestroyPageRows(SettingMenu menu)
        {
            if (menu == null || menu.menu == null || menu.menu.transform == null) return;
            var parent = menu.menu.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null || child.name == null) continue;
                if (!child.name.StartsWith(PagePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static bool HasEnabled(RoleDef[] roles)
        {
            for (int i = 0; i < roles.Length; i++)
                if (roles[i].Enabled != null && roles[i].Enabled()) return true;
            return false;
        }

        /// <summary>
        /// A faction header row: clone of the game's roleOptionPrefab with the
        /// title set. Display-only — every PassiveButton it inherits is
        /// neutralized.
        /// </summary>
        private static void BuildHeader(RoleGameOption template, Transform parent, string title, Color color, float y)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = PagePrefix + "Header_" + title.Replace(" ", "");

            // Grab the title TMP from the CLONE (never the template) before
            // stripping the RoleGameOption behaviour.
            var clonedHeader = clone.GetComponent<RoleGameOption>();
            TextMeshPro titleText = null;
            TextMeshPro chanceText = null;
            if (clonedHeader != null)
            {
                titleText = clonedHeader.TitleText;
                chanceText = clonedHeader.ChanceText;
            }
            if (titleText == null) titleText = clone.GetComponentInChildren<TextMeshPro>(true);

            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if ((comp as TextMeshPro) != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            if (chanceText != null && chanceText.gameObject != null) chanceText.gameObject.SetActive(false);
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = color;
            }

            // Headers are display-only: kill any click wiring they inherited,
            // and drop the colliders too — a stray collider from the game's
            // clickable role row would swallow clicks aimed at rows below.
            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null) continue;
                try { pb.OnClick.RemoveAllListeners(); } catch { }
                pb.enabled = false;
            }
            foreach (var col in clone.GetComponentsInChildren<Collider2D>(true))
                if (col != null) UnityEngine.Object.Destroy(col);

            var t = clone.transform;
            t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z);
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// A role count row: clone of the game's keyvaluePrefab (NumberOption)
        /// with the OptionBehaviour stripped and + / - routed through ClickRouter
        /// (same proven pattern as NativeSettingsRows).
        /// </summary>
        private static void BuildCountRow(NumberOption template, Transform parent, RoleDef role, float y)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = PagePrefix + "Row_" + role.Key;

            var clonedNumber = clone.GetComponent<NumberOption>();
            var label = clonedNumber != null ? clonedNumber.TitleText : null;
            var value = clonedNumber != null ? clonedNumber.ValueText : null;
            if (label == null || value == null)
            {
                var tmps = clone.GetComponentsInChildren<TextMeshPro>(true);
                if (tmps != null && tmps.Length > 0 && label == null) label = tmps[0];
                if (tmps != null && tmps.Length > 1 && value == null) value = tmps[1];
            }

            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if ((comp as PassiveButton) != null) continue;
                if ((comp as TextMeshPro) != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            if (label != null)
            {
                label.text = role.Display;
                label.color = role.Color;
            }
            if (value != null)
                value.text = RoleSettingsSync.GetInt(role.Key + ".Count").ToString(CultureInfo.InvariantCulture);

            PassiveButton minus = null, plus = null;
            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null) continue;
                var n = pb.gameObject.name;
                if (n.IndexOf("Minus", StringComparison.OrdinalIgnoreCase) >= 0) minus = pb;
                else if (n.IndexOf("Plus", StringComparison.OrdinalIgnoreCase) >= 0
                      || n.IndexOf("More", StringComparison.OrdinalIgnoreCase) >= 0) plus = pb;
            }

            if (minus != null && plus != null)
            {
                minus.gameObject.name = PagePrefix + "Row_" + role.Key + ".Minus";
                plus.gameObject.name = PagePrefix + "Row_" + role.Key + ".Plus";
                _ids.Add(minus.gameObject.name);
                _ids.Add(plus.gameObject.name);
                ClickRouter.Register(minus.gameObject.name, () => StepCount(role.Key, -1));
                ClickRouter.Register(plus.gameObject.name, () => StepCount(role.Key, +1));
            }

            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null || pb == minus || pb == plus) continue;
                try { pb.OnClick.RemoveAllListeners(); } catch { }
                pb.enabled = false;
            }

            var t = clone.transform;
            t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z);
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Count stepper: clamps 0..15 and rebuilds the page so the role's
        /// config rows expand (count reaches 1) or collapse (count drops to 0).
        /// </summary>
        private static void StepCount(string key, int delta)
        {
            if (!RoleSettingsSync.CanEdit) return;
            int current = RoleSettingsSync.GetInt(key + ".Count");
            int next = Mathf.Clamp(current + delta, 0, 15);
            RoleSettingsSync.SetInt(key + ".Count", next);
            RebuildContent();
        }

        /// <summary>
        /// A per-role config row: same native keyvaluePrefab clone as the count
        /// row, slightly indented, label dimmed, value formatted per kind
        /// (int / float seconds / On-Off / string), steppers routed through
        /// ClickRouter.
        /// </summary>
        private static void BuildConfigRow(NumberOption template, Transform parent, string channel, string labelText, string kind, float y)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = PagePrefix + "Cfg_" + channel.Replace(".", "_");

            var clonedNumber = clone.GetComponent<NumberOption>();
            var label = clonedNumber != null ? clonedNumber.TitleText : null;
            var value = clonedNumber != null ? clonedNumber.ValueText : null;
            if (label == null || value == null)
            {
                var tmps = clone.GetComponentsInChildren<TextMeshPro>(true);
                if (tmps != null && tmps.Length > 0 && label == null) label = tmps[0];
                if (tmps != null && tmps.Length > 1 && value == null) value = tmps[1];
            }

            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if ((comp as PassiveButton) != null) continue;
                if ((comp as TextMeshPro) != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            if (label != null)
            {
                label.text = labelText;
                label.color = new Color(0.78f, 0.78f, 0.82f, 1f); // dimmed: a sub-setting under the role
            }
            if (value != null)
                value.text = FormatConfigValue(channel, kind);

            PassiveButton minus = null, plus = null;
            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null) continue;
                var n = pb.gameObject.name;
                if (n.IndexOf("Minus", StringComparison.OrdinalIgnoreCase) >= 0) minus = pb;
                else if (n.IndexOf("Plus", StringComparison.OrdinalIgnoreCase) >= 0
                      || n.IndexOf("More", StringComparison.OrdinalIgnoreCase) >= 0) plus = pb;
            }

            if (minus != null && plus != null)
            {
                minus.gameObject.name = PagePrefix + "Cfg_" + channel.Replace(".", "_") + ".Minus";
                plus.gameObject.name = PagePrefix + "Cfg_" + channel.Replace(".", "_") + ".Plus";
                _ids.Add(minus.gameObject.name);
                _ids.Add(plus.gameObject.name);
                ClickRouter.Register(minus.gameObject.name, () => StepConfig(channel, kind, value, -1));
                ClickRouter.Register(plus.gameObject.name, () => StepConfig(channel, kind, value, +1));
            }

            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null || pb == minus || pb == plus) continue;
                try { pb.OnClick.RemoveAllListeners(); } catch { }
                pb.enabled = false;
            }

            var t = clone.transform;
            t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z); // flush like any ordinary row
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
        }

        private static string FormatConfigValue(string channel, string kind)
        {
            switch (kind)
            {
                case "bool": return RoleSettingsSync.GetBool(channel) ? "On" : "Off";
                case "int": return RoleSettingsSync.GetInt(channel).ToString(CultureInfo.InvariantCulture);
                case "float": return RoleSettingsSync.GetFloat(channel).ToString("0.#", CultureInfo.InvariantCulture) + "s";
                case "percent": return RoleSettingsSync.GetFloat(channel).ToString("0", CultureInfo.InvariantCulture) + "%";
                default: return RoleSettingsSync.GetString(channel, "Faction");
            }
        }

        private static void StepConfig(string channel, string kind, TextMeshPro valueText, int dir)
        {
            if (!RoleSettingsSync.CanEdit) return;
            if (kind == "bool")
            {
                RoleSettingsSync.SetBool(channel, dir > 0);
            }
            else if (kind == "int")
            {
                int v = Mathf.Clamp(RoleSettingsSync.GetInt(channel) + dir, 0, 15);
                RoleSettingsSync.SetInt(channel, v);
            }
            else if (kind == "float")
            {
                float v = Mathf.Max(0f, RoleSettingsSync.GetFloat(channel) + dir);
                RoleSettingsSync.SetFloat(channel, v);
            }
            else if (kind == "percent")
            {
                float v = Mathf.Clamp(RoleSettingsSync.GetFloat(channel) + dir * 5f, 0f, 100f);
                RoleSettingsSync.SetFloat(channel, v);
            }
            else // string: cycle the two options (Faction/Role, Jester/Crewmate)
            {
                string cur = RoleSettingsSync.GetString(channel, "Faction");
                string next = cur == "Faction" ? "Role" : cur == "Role" ? "Faction"
                            : cur == "Jester" ? "Crewmate" : "Jester";
                RoleSettingsSync.SetString(channel, next);
            }
            if (valueText != null) valueText.text = FormatConfigValue(channel, kind);
        }

        /// <summary>
        /// The page rows live in the same parent as the native rows, so they
        /// use the same local-space Y as the native list (top row down).
        /// </summary>
        private static float ComputeStartY(SettingMenu menu)
        {
            if (menu.AllItems != null)
            {
                for (int i = 0; i < menu.AllItems.Count; i++)
                {
                    var item = menu.AllItems.get_Item(i);
                    if (item != null) return item.localPosition.y;
                }
            }
            return menu.YStart;
        }

        private static NumberOption FindNumberOptionRow(SettingMenu menu)
        {
            if (menu.AllItems == null) return null;
            for (int i = 0; i < menu.AllItems.Count; i++)
            {
                var item = menu.AllItems.get_Item(i);
                if (item == null) continue;
                var number = item.GetComponentInChildren<NumberOption>(true);
                if (number != null) return number;
            }
            return null;
        }

        private static void Log(string message) =>
            BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogInfo("Native roles page: " + message);
    }
}

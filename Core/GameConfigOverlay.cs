using System;
using System.Collections.Generic;
using ClassicUs.ManuAPI;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Town Of Us role-settings tabs shown when the lobby game-config menu opens.
    ///
    /// Three tabs — Crewmate Roles / Impostor Roles / Neutral Roles — each listing
    /// every enabled role with an On/Off toggle, a Count stepper, a Chance stepper,
    /// and an expandable section for the role's own config (cooldowns, uses, ...).
    ///
    /// Deliberately does NOT inject rows into the native menu (that path froze on
    /// Classic Us 8.9). Instead it overlays the config screen with UI built from
    /// the same primitives as the update modal (FullScreen backdrop, kill-button
    /// clones, TMP with a borrowed font), so the native menu is never touched.
    /// </summary>
    internal static class GameConfigOverlay
    {
        private static readonly Color CrewColor = new(0.35f, 0.8f, 1f, 1f);
        private static readonly Color ImpColor = new(1f, 0.35f, 0.35f, 1f);
        private static readonly Color NeutralColor = new(1f, 0.8f, 0.3f, 1f);

        private static readonly (string Key, string Display, int Faction, Color Color)[] Roles =
        {
            ("Sheriff", "Sheriff", 0, CrewColor),
            ("Engineer", "Engineer", 0, CrewColor),
            ("Medic", "Medic", 0, CrewColor),
            ("Seer", "Seer", 0, CrewColor),
            ("Vigilante", "Vigilante", 0, CrewColor),
            ("Altruist", "Altruist", 0, CrewColor),
            ("Mayor", "Mayor", 0, CrewColor),
            ("Swapper", "Swapper", 0, CrewColor),
            ("Spy", "Spy", 0, CrewColor),
            ("Investigator", "Investigator", 0, CrewColor),
            ("TimeLord", "Time Lord", 0, CrewColor),
            ("Snitch", "Snitch", 0, CrewColor),
            ("Assassin", "Assassin", 1, ImpColor),
            ("Janitor", "Janitor", 1, ImpColor),
            ("Morphling", "Morphling", 1, ImpColor),
            ("Camouflager", "Camouflager", 1, ImpColor),
            ("Swooper", "Swooper", 1, ImpColor),
            ("Underdog", "Underdog", 1, ImpColor),
            ("Undertaker", "Undertaker", 1, ImpColor),
            ("Miner", "Miner", 1, ImpColor),
            ("Jester", "Jester", 2, NeutralColor),
            ("Executioner", "Executioner", 2, NeutralColor),
            ("Arsonist", "Arsonist", 2, NeutralColor),
            ("Phantom", "Phantom", 2, NeutralColor),
            ("Shifter", "Shifter", 2, NeutralColor),
            ("Glitch", "The Glitch", 2, NeutralColor),
        };

        private static readonly (string Key, string Display)[] ModifiersList =
        {
            ("Modifiers.Torch", "Torch"),
            ("Modifiers.Diseased", "Diseased"),
            ("Modifiers.Flash", "Flash"),
            ("Modifiers.Tiebreaker", "Tiebreaker"),
            ("Modifiers.Drunk", "Drunk"),
            ("Modifiers.Giant", "Giant"),
            ("Modifiers.ButtonBarry", "Button Barry"),
        };

        // Shared with NativeRolesPage: the per-role config fields rendered as
        // native rows under a role when its count is >= 1.
        internal static readonly Dictionary<string, (string Field, string Label, string Kind)[]> Extras = new()
        {
            ["Sheriff"] = new[]
            {
                ("KillCooldown", "Kill Cooldown", "float"),
                ("KillOther", "Kill Other", "bool"),
                ("BodyReport", "Report Own Body", "bool"),
            },
            ["Engineer"] = new[] { ("FixCooldown", "Fix Cooldown", "float") },
            ["Medic"] = new[]
            {
                ("Uses", "Shields", "int"),
                ("Cooldown", "Cooldown", "float"),
                ("ShieldBreaksOnKill", "Breaks On Kill", "bool"),
            },
            ["Seer"] = new[]
            {
                ("Uses", "Investigations", "int"),
                ("Cooldown", "Cooldown", "float"),
                ("RevealMode", "Reveal", "string"),
            },
            ["Vigilante"] = new[]
            {
                ("Shots", "Shots", "int"),
                ("Cooldown", "Cooldown", "float"),
            },
            ["Assassin"] = new[]
            {
                ("MultiKill", "Multi-Kill", "bool"),
                ("MeetingUi", "Meeting Buttons", "bool"),
            },
            ["Janitor"] = new[] { ("CleanCooldown", "Clean Cooldown", "float") },
            ["Altruist"] = new[]
            {
                ("Uses", "Revives", "int"),
                ("Cooldown", "Cooldown", "float"),
            },
            ["Mayor"] = new[] { ("VoteBank", "Vote Bank", "int") },
            ["Arsonist"] = new[] { ("DouseCooldown", "Douse Cooldown", "float") },
            ["Executioner"] = new[]
            {
                ("ConvertOnTargetDeath", "Convert On Target Death", "bool"),
                ("ConvertRole", "Convert To", "string"),
            },
            ["Morphling"] = new[]
            {
                ("MorphCooldown", "Morph Cooldown", "float"),
                ("MorphDuration", "Morph Duration", "float"),
            },
            ["Camouflager"] = new[]
            {
                ("CamouflageCooldown", "Camouflage Cooldown", "float"),
                ("CamouflageDuration", "Camouflage Duration", "float"),
            },
            ["Swooper"] = new[]
            {
                ("SwoopCooldown", "Swoop Cooldown", "float"),
                ("SwoopDuration", "Swoop Duration", "float"),
            },
            ["Underdog"] = new[] { ("CooldownMultiplier", "Cooldown Multiplier", "float") },
            ["Undertaker"] = new[] { ("DragCooldown", "Drag Cooldown", "float") },
            ["Investigator"] = new[]
            {
                ("FootprintInterval", "Footprint Interval", "float"),
                ("FootprintDuration", "Footprint Duration", "float"),
            },
            ["TimeLord"] = new[]
            {
                ("RewindCooldown", "Rewind Cooldown", "float"),
                ("RewindSeconds", "Rewind Seconds", "float"),
            },
            ["Shifter"] = new[] { ("ShiftCooldown", "Shift Cooldown", "float") },
            ["Glitch"] = new[]
            {
                ("MimicCooldown", "Mimic Cooldown", "float"),
                ("MimicDuration", "Mimic Duration", "float"),
                ("HackCooldown", "Hack Cooldown", "float"),
                ("HackDuration", "Hack Duration", "float"),
                ("KillCooldown", "Kill Cooldown", "float"),
            },
            ["Miner"] = new[] { ("MineCooldown", "Mine Cooldown", "float") },
        };

        private static GameObject _root;
        private static TextMeshPro _title;
        private static TextMeshPro _hint;
        private static TextMeshPro _textSource;
        private static TMP_FontAsset _font;
        private static Material _material;
        private static readonly List<GameObject> _content = new();
        private static readonly List<string> _registeredIds = new();
        private static readonly HashSet<string> _expanded = new();
        private static int _tab;
        private static Component _trackedMenu;

        public static bool IsVisible => _root != null && _root && _root.activeSelf;

        /// <summary>Called from the menu hooks when the lobby config screen opens.</summary>
        public static void OnConfigOpened(object menu)
        {
            try
            {
                if (RoleConfig.GameConfigOverlay?.Value == false) return;
                if (RoleConfig.NativeMenuRows?.Value == true) return; // arrow mode: opened via the settings-window arrow instead
                ShowCore(menu);
            }
            catch (Exception e)
            {
                Log("open: " + e.Message);
                Hide();
            }
        }

        private static void ShowCore(object menu)
        {
            if (!HudManager.InstanceExists) return;
            if (LobbyBehaviour.Instance == null) return; // only show in a lobby
            if (IsVisible) return;

            _trackedMenu = menu as Component;
            EnsureCreated(HudManager.Instance);
            if (_root == null) return;

            _tab = 0;
            Render();
            _root.SetActive(true);
            Log("config overlay shown");
        }

        /// <summary>Called from the HudManager.Update patch; closes with the menu.</summary>
        public static void Poll()
        {
            try
            {
                if (!IsVisible) return;
                if (_trackedMenu == null || !_trackedMenu.gameObject.activeInHierarchy) Hide();
            }
            catch
            {
                Hide();
            }
        }

        public static void Hide()
        {
            try
            {
                if (_root != null && _root.activeSelf) _root.SetActive(false);
            }
            catch { }
        }

        // ── Rendering ─────────────────────────────────────────────────────────
        private static void EnsureCreated(HudManager hud)
        {
            if (_root != null && _root) return;

            _root = new GameObject("ToU_ConfigOverlay");
            _root.transform.SetParent(hud.transform, false);
            _root.transform.localPosition = Vector3.zero;

            // Fullscreen click-blocking backdrop (the game's own FullScreen quad).
            GameObject src = null;
            if (hud.FullScreen != null)
            {
                var comp = hud.FullScreen.TryCast<Component>();
                if (comp != null) src = comp.gameObject;
            }

            GameObject backdrop;
            if (src != null)
            {
                backdrop = UnityEngine.Object.Instantiate(src, _root.transform);
                backdrop.name = "ToU_Backdrop";
                foreach (var comp in backdrop.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp == null) continue;
                    if (comp.TryCast<SpriteRenderer>() != null) continue;
                    if (comp.TryCast<Collider2D>() != null) continue;
                    comp.enabled = false;
                    UnityEngine.Object.Destroy(comp);
                }
            }
            else
            {
                backdrop = new GameObject("ToU_Backdrop");
                backdrop.transform.SetParent(_root.transform, false);
                var sr = backdrop.AddComponent<SpriteRenderer>();
                var bc = backdrop.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(12f, 7f);
            }
            // One unit behind our buttons/text (z=0) so the collider blocks the
            // native menu underneath without swallowing clicks meant for us.
            backdrop.transform.localPosition = new Vector3(0f, 0f, -1f);
            var renderer = backdrop.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(0f, 0f, 0f, 0.9f);
                renderer.sortingOrder = 100;
            }

            // A collider guarantees the native menu behind cannot be clicked.
            if (backdrop.GetComponent<Collider2D>() == null)
            {
                var box = backdrop.AddComponent<BoxCollider2D>();
                box.size = new Vector2(14f, 8f);
            }

            if (hud.GameSettingsTMP != null)
            {
                _textSource = hud.GameSettingsTMP;
                _font = hud.GameSettingsTMP.font;
                _material = hud.GameSettingsTMP.fontSharedMaterial;
            }

            _title = MakeText("Title", "Town Of Us — Game Config", new Vector3(0f, 2.55f, 0f), 3.4f, Color.white, 115, persistent: true);
            _hint = MakeText("Hint", "", new Vector3(0f, 2.05f, 0f), 1.5f, new Color(0.75f, 0.75f, 0.75f, 1f), 115, persistent: true);
        }

        private static void Render()
        {
            ClearContent();
            var hud = HudManager.Instance;
            if (hud == null) return;

            if (_title != null) _title.text = "Town Of Us — Game Config";
            if (_hint != null)
                _hint.text = RoleSettingsSync.CanEdit
                    ? "Editing role pool (host) — press Done to close"
                    : "Role settings synced from host — read only";

            MakeButton("TabCrewmate", "Crewmate", new Vector3(-3f, 1.55f, 0f), 1.5f, 1.8f, () => SetTab(0), _tab == 0);
            MakeButton("TabImpostor", "Impostor", new Vector3(-1f, 1.55f, 0f), 1.5f, 1.8f, () => SetTab(1), _tab == 1);
            MakeButton("TabNeutral", "Neutral", new Vector3(1f, 1.55f, 0f), 1.5f, 1.8f, () => SetTab(2), _tab == 2);
            MakeButton("TabModifiers", "Modifiers", new Vector3(3f, 1.55f, 0f), 1.5f, 1.8f, () => SetTab(3), _tab == 3);

            float y = 0.85f;
            if (_tab == 3)
            {
                for (int i = 0; i < ModifiersList.Length; i++)
                    y = BuildModifierRow(ModifiersList[i].Key, ModifiersList[i].Display, y);
            }
            else
            {
                for (int i = 0; i < Roles.Length; i++)
                {
                    var role = Roles[i];
                    if (role.Faction != _tab) continue;
                    y = BuildRoleRow(role.Key, role.Display, role.Color, y);
                }
            }

            MakeButton("Done", "Done", new Vector3(0f, -2.7f, 0f), 1.1f, 2.4f, Hide, true);
        }

        private static float BuildModifierRow(string key, string display, float y)
        {
            bool enabled = RoleSettingsSync.GetBool(key + ".Enabled");
            float probability = RoleSettingsSync.GetFloat(key + ".Probability");

            MakeText("Name_" + key.Replace(".", "_"), display, new Vector3(-3.7f, y, 0f), 1.6f, NeutralColor, 115);
            MakeButton(key + ".Enabled", enabled ? "On" : "Off", new Vector3(-2.15f, y, 0f), 0.7f, 1.6f,
                () => { RoleSettingsSync.SetBool(key + ".Enabled", !RoleSettingsSync.GetBool(key + ".Enabled")); Render(); }, enabled);

            MakeButton(key + ".Minus", "-", new Vector3(1.2f, y, 0f), 0.55f, 1.9f,
                () => Step(key + ".Probability", "float", -5f), true);
            MakeText("Prob_" + key.Replace(".", "_"), probability.ToString("0") + "%", new Vector3(2.2f, y, 0f), 1.4f, Color.white, 115);
            MakeButton(key + ".Plus", "+", new Vector3(3.1f, y, 0f), 0.55f, 1.9f,
                () => Step(key + ".Probability", "float", 5f), true);

            return y - 0.55f;
        }

        private static float BuildRoleRow(string key, string display, Color color, float y)
        {
            bool enabled = RoleSettingsSync.GetBool(key + ".Enabled");
            int count = RoleSettingsSync.GetInt(key + ".Count");
            float chance = RoleSettingsSync.GetFloat(key + ".Chance");
            bool expanded = _expanded.Contains(key);

            MakeText("Name_" + key, display, new Vector3(-3.7f, y, 0f), 1.7f, color, 115);
            MakeButton(key + ".Enabled", enabled ? "On" : "Off", new Vector3(-2.15f, y, 0f), 0.75f, 1.7f,
                () => { RoleSettingsSync.SetBool(key + ".Enabled", !RoleSettingsSync.GetBool(key + ".Enabled")); Render(); }, enabled);

            MakeButton(key + ".CountMinus", "-", new Vector3(-1.0f, y, 0f), 0.55f, 1.9f,
                () => Step(key + ".Count", "int", -1), true);
            MakeText("Count_" + key, "Count: " + count, new Vector3(-0.2f, y, 0f), 1.4f, Color.white, 115);
            MakeButton(key + ".CountPlus", "+", new Vector3(0.6f, y, 0f), 0.55f, 1.9f,
                () => Step(key + ".Count", "int", 1), true);

            MakeButton(key + ".ChanceMinus", "-", new Vector3(1.65f, y, 0f), 0.55f, 1.9f,
                () => Step(key + ".Chance", "float", -5f), true);
            MakeText("Chance_" + key, chance.ToString("0") + "%", new Vector3(2.45f, y, 0f), 1.4f, Color.white, 115);
            MakeButton(key + ".ChancePlus", "+", new Vector3(3.25f, y, 0f), 0.55f, 1.9f,
                () => Step(key + ".Chance", "float", 5f), true);

            MakeButton(key + ".Expand", expanded ? "▲" : "▼", new Vector3(4.15f, y, 0f), 0.65f, 1.8f,
                () =>
                {
                    if (!_expanded.Add(key)) _expanded.Remove(key);
                    Render();
                }, true);

            float next = y - 0.55f;
            if (expanded && Extras.TryGetValue(key, out var extras))
            {
                for (int i = 0; i < extras.Length; i++)
                    next = BuildExtraRow(key, extras[i].Field, extras[i].Label, extras[i].Kind, next);
            }
            return next;
        }

        private static float BuildExtraRow(string key, string field, string label, string kind, float y)
        {
            var full = key + "." + field;
            MakeText("L_" + full, label, new Vector3(-3.3f, y, 0f), 1.35f, new Color(0.82f, 0.82f, 0.82f, 1f), 115);

            if (kind == "bool")
            {
                bool v = RoleSettingsSync.GetBool(full);
                MakeButton(full + ".Toggle", v ? "On" : "Off", new Vector3(2.3f, y, 0f), 0.7f, 1.6f,
                    () => { RoleSettingsSync.SetBool(full, !RoleSettingsSync.GetBool(full)); Render(); }, v);
            }
            else if (kind == "string")
            {
                string v = RoleSettingsSync.GetString(full, "Faction");
                MakeButton(full + ".Cycle", v, new Vector3(2.3f, y, 0f), 1.0f, 1.5f,
                    () =>
                    {
                        RoleSettingsSync.SetString(full, CycleString(v));
                        Render();
                    }, true);
            }
            else if (kind == "int")
            {
                int v = RoleSettingsSync.GetInt(full);
                MakeButton(full + ".Minus", "-", new Vector3(1.5f, y, 0f), 0.5f, 1.7f,
                    () => Step(full, "int", -1), true);
                MakeText("V_" + full, v.ToString(), new Vector3(2.4f, y, 0f), 1.35f, Color.white, 115);
                MakeButton(full + ".Plus", "+", new Vector3(3.3f, y, 0f), 0.5f, 1.7f,
                    () => Step(full, "int", 1), true);
            }
            else
            {
                float v = RoleSettingsSync.GetFloat(full);
                MakeButton(full + ".Minus", "-", new Vector3(1.5f, y, 0f), 0.5f, 1.7f,
                    () => Step(full, "float", -1f), true);
                MakeText("V_" + full, v.ToString("0.#") + "s", new Vector3(2.4f, y, 0f), 1.35f, Color.white, 115);
                MakeButton(full + ".Plus", "+", new Vector3(3.3f, y, 0f), 0.5f, 1.7f,
                    () => Step(full, "float", 1f), true);
            }

            return y - 0.45f;
        }

        /// <summary>Cycles two-option string settings (Faction/Role, Jester/Crewmate).</summary>
        private static string CycleString(string current)
        {
            if (current == "Faction") return "Role";
            if (current == "Role") return "Faction";
            if (current == "Jester") return "Crewmate";
            return "Jester";
        }

        private static void Step(string name, string kind, float delta)
        {
            if (!RoleSettingsSync.CanEdit) return;
            if (kind == "int") RoleSettingsSync.SetInt(name, RoleSettingsSync.GetInt(name) + (int)delta);
            else RoleSettingsSync.SetFloat(name, RoleSettingsSync.GetFloat(name) + delta);
            Render();
        }

        private static void SetTab(int tab)
        {
            _tab = tab;
            Render();
        }

        // ── Primitives (UpdateModal-proven) ───────────────────────────────────
        private static TextMeshPro MakeText(string name, string text, Vector3 pos, float fontSize, Color color, int sort, bool persistent = false)
        {
            GameObject go;
            TextMeshPro tmp;

            // Clone the game's own working world-space TMP (the lobby
            // game-settings text) rather than AddComponent<TextMeshPro>. A
            // freshly AddComponent'd TMP initializes with its native default
            // font size (36) and ignores the fontSize set before it is awake,
            // which rendered the overlay's text as giant overlapping glyphs. A
            // clone arrives fully initialized (font, mesh, material, awake
            // state) so fontSize applies normally — same reason the cloned
            // KillButton labels render at the right size.
            if (_textSource != null)
            {
                go = UnityEngine.Object.Instantiate(_textSource.gameObject, _root.transform);
                go.name = name;
                go.transform.localPosition = pos;
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                // Text labels must never intercept clicks: the source is not
                // clickable, but defensively drop any collider the clone
                // inherited (PassiveButtonManager routes clicks by collider).
                foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
                    if (col != null) UnityEngine.Object.Destroy(col);
                tmp = go.GetComponent<TextMeshPro>();
                if (tmp == null) tmp = go.GetComponentInChildren<TextMeshPro>(true);
                if (tmp == null)
                {
                    UnityEngine.Object.Destroy(go);
                    return null;
                }
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = Vector3.one;
                tmp = go.AddComponent<TextMeshPro>();
                if (_font != null) tmp.font = _font;
                if (_material != null) tmp.fontSharedMaterial = _material;
            }

            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.sortingOrder = sort;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            if (!persistent) _content.Add(go);
            return tmp;
        }

        private static GameObject MakeButton(string id, string label, Vector3 pos, float scale, float fontSize, Action onClick, bool bright)
        {
            var hud = HudManager.Instance;
            if (hud == null || hud.KillButton == null) return null;

            var clone = UnityEngine.Object.Instantiate(hud.KillButton.gameObject, _root.transform);
            clone.name = "ToU_Btn_" + id;

            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.TryCast<PassiveButton>() != null) continue;
                if (comp.TryCast<TextMeshPro>() != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            // The KillButton clone carries the kill icon and cooldown-overlay
            // sprites as well as the round button background. Keep only the
            // background: prefer the root-level SpriteRenderer
            // (KillButtonManager.renderer), falling back to the largest sprite,
            // so the config buttons show clean + / - / On-Off labels instead of
            // a skull icon.
            SpriteRenderer background = clone.GetComponent<SpriteRenderer>();
            if (background == null || background.sprite == null)
            {
                background = null;
                float largest = -1f;
                foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.sprite == null) continue;
                    var size = sr.sprite.bounds.size;
                    float area = size.x * size.y;
                    if (area > largest) { largest = area; background = sr; }
                }
            }
            foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && sr != background) sr.enabled = false;

            clone.transform.localPosition = pos;
            clone.transform.localScale = Vector3.one * scale;

            foreach (var sr in clone.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                sr.color = bright ? new Color(0.16f, 0.22f, 0.3f, 0.95f) : new Color(0.16f, 0.22f, 0.3f, 0.55f);
                sr.sortingOrder = 110;
            }

            var tmp = clone.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
            {
                tmp.text = label;
                tmp.fontSize = Mathf.Max(fontSize, tmp.fontSize);
                tmp.color = bright ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                tmp.sortingOrder = 120;
            }

            // Delegate-free click dispatch (see ClickRouter): the game's native
            // pipeline calls PassiveButton.ReceiveClickDown for every collider
            // under the mouse, so we name this button's PassiveButton GameObject
            // with the unique id and let the ClickRouter prefix route the click.
            // OnClick is deliberately left untouched — marshalling a managed
            // UnityAction via AddListener triggers the game's protection.
            var passive = clone.GetComponentInChildren<PassiveButton>(true);
            if (passive != null && onClick != null)
            {
                passive.gameObject.name = clone.name;
                ClickRouter.Register(clone.name, onClick);
                _registeredIds.Add(clone.name);
            }

            clone.SetActive(true);
            _content.Add(clone);
            return clone;
        }

        private static void ClearContent()
        {
            ClickRouter.UnregisterAll(_registeredIds);
            _registeredIds.Clear();
            for (int i = 0; i < _content.Count; i++)
            {
                if (_content[i] != null) UnityEngine.Object.Destroy(_content[i]);
            }
            _content.Clear();
        }

        private static void Log(string message) =>
            BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogInfo("Config overlay: " + message);
    }

    // NOTE: SettingMenu has no "Start" on Classic Us 8.9 (patched "Start" there
    // throws at plugin load). OnEnable fires every time the menu becomes active,
    // which is exactly when the overlay should appear.
    [HarmonyPatch(typeof(SettingMenu), nameof(SettingMenu.OnEnable))]
    internal static class SettingMenu_OnEnable_ConfigOverlayPatch
    {
        private static void Postfix(SettingMenu __instance) => GameConfigOverlay.OnConfigOpened(__instance);
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.SetupFromData))]
    internal static class GameSettingMenu_SetupFromData_ConfigOverlayPatch
    {
        private static void Postfix(GameSettingMenu __instance) => GameConfigOverlay.OnConfigOpened(__instance);
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.OnEnable))]
    internal static class GameOptionsMenu_OnEnable_ConfigOverlayPatch
    {
        private static void Postfix(GameOptionsMenu __instance) => GameConfigOverlay.OnConfigOpened(__instance);
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    internal static class HudManager_Update_ConfigOverlayPatch
    {
        private static void Postfix() => GameConfigOverlay.Poll();
    }
}

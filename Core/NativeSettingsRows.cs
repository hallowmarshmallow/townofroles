using System;
using System.Globalization;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Native role-count rows injected into Classic Us' own game-config menu.
    ///
    /// After the game has built and positioned its option rows (a
    /// SettingMenu.OnEnable postfix), one native-style row is appended per
    /// enabled Town Of Us role. Each row is a clone of the game's own
    /// keyvaluePrefab (NumberOption), so it shares the exact native typography
    /// and + / - button layout.
    ///
    /// The clone's OptionBehaviour is stripped so it never fights our values
    /// through the game's option plumbing, and clicks are dispatched through
    /// ClickRouter (the delegate-free router): the buttons are renamed to
    /// unique ids and the router's PassiveButton.ReceiveClickDown prefix routes
    /// them, so no managed delegate is ever marshalled into Il2Cpp. Values are
    /// read/written through RoleSettingsSync channels ("&lt;Role&gt;.Count"),
    /// which are host-authoritative and RPC-synced to clients.
    /// </summary>
    internal static class NativeSettingsRows
    {
        private static readonly Color CrewColor = new(0.35f, 0.8f, 1f, 1f);
        private static readonly Color ImpColor = new(1f, 0.35f, 0.35f, 1f);
        private static readonly Color NeutralColor = new(1f, 0.8f, 0.3f, 1f);

        private struct RoleRow
        {
            public string Key;      // RoleSettingsSync channel prefix, e.g. "Sheriff"
            public string Display;  // Label shown in the menu
            public Color Color;     // Label tint (faction color)
            public Func<bool> Enabled; // cfg toggle for the role
        }

        private static readonly RoleRow[] Roles =
        {
            new RoleRow { Key = "Sheriff", Display = "Sheriff", Color = CrewColor, Enabled = () => RoleConfig.Sheriff?.Value == true },
            new RoleRow { Key = "Engineer", Display = "Engineer", Color = CrewColor, Enabled = () => RoleConfig.Engineer?.Value == true },
            new RoleRow { Key = "Medic", Display = "Medic", Color = CrewColor, Enabled = () => RoleConfig.Medic?.Value == true },
            new RoleRow { Key = "Seer", Display = "Seer", Color = CrewColor, Enabled = () => RoleConfig.Seer?.Value == true },
            new RoleRow { Key = "Vigilante", Display = "Vigilante", Color = CrewColor, Enabled = () => RoleConfig.Vigilante?.Value == true },
            new RoleRow { Key = "Assassin", Display = "Assassin", Color = ImpColor, Enabled = () => RoleConfig.Assassin?.Value == true },
            new RoleRow { Key = "Jester", Display = "Jester", Color = NeutralColor, Enabled = () => RoleConfig.Jester?.Value == true },
        };

        /// <summary>
        /// Called from a SettingMenu.OnEnable postfix after the game has built
        /// its own option rows. Inert unless RoleConfig.NativeMenuRows is on.
        /// </summary>
        public static void Inject(SettingMenu menu)
        {
            if (RoleConfig.NativeMenuRows?.Value != true) return;
            if (menu == null) return;
            try
            {
                if (menu.menu == null || menu.menu.transform == null) return;
                var parent = menu.menu.transform;

                // Template: the game's own keyvalue row prefab (NumberOption).
                // Fall back to any already-built native NumberOption row.
                var template = menu.keyvaluePrefab;
                if (template == null) template = FindNumberOptionRow(menu);
                if (template == null) return;

                float y = ComputeStartY(menu);
                float step = menu.YOffset > 0f ? menu.YOffset : 0.45f;

                for (int i = 0; i < Roles.Length; i++)
                {
                    var role = Roles[i];
                    if (role.Enabled == null || !role.Enabled()) continue;
                    y -= step;
                    BuildRow(template, parent, role, y);
                }
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Native settings rows: " + e.Message);
            }
        }

        private static void BuildRow(NumberOption template, Transform parent, RoleRow role, float y)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = "ToU_Row_" + role.Key;

            var t = clone.transform;
            t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z);
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;

            // Grab the cloned label/value TMPs before stripping the
            // OptionBehaviour (which would re-render from the game's options).
            var clonedNumber = clone.GetComponent<NumberOption>();
            var label = clonedNumber != null ? clonedNumber.TitleText : null;
            var value = clonedNumber != null ? clonedNumber.ValueText : null;
            if (label == null || value == null)
            {
                var tmps = clone.GetComponentsInChildren<TextMeshPro>(true);
                if (tmps != null && tmps.Length > 0 && label == null) label = tmps[0];
                if (tmps != null && tmps.Length > 1 && value == null) value = tmps[1];
            }

            // Strip everything except the visuals and the clickable buttons:
            // the OptionBehaviour and any other behaviour would fight our
            // values through the game's option plumbing.
            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.TryCast<PassiveButton>() != null) continue;
                if (comp.TryCast<TextMeshPro>() != null) continue;
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

            // Route the + / - buttons through ClickRouter (delegate-free): the
            // prefix returns false for registered ids, so the native listeners
            // cloned from the row (which would edit game options) never fire.
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
                minus.gameObject.name = "ToU_Row_" + role.Key + ".Minus";
                plus.gameObject.name = "ToU_Row_" + role.Key + ".Plus";
                ClickRouter.Register(minus.gameObject.name, () => Step(role, value, -1));
                ClickRouter.Register(plus.gameObject.name, () => Step(role, value, +1));
            }

            // Neutralize every other PassiveButton the clone inherited: their
            // prefab-wired OnClick listeners call NumberOption.Increase/
            // Decrease on the component we just destroyed — a click would
            // dereference a destroyed object. The registered + / - buttons are
            // safe because the ClickRouter prefix blocks their listeners.
            foreach (var pb in clone.GetComponentsInChildren<PassiveButton>(true))
            {
                if (pb == null || pb == minus || pb == plus) continue;
                try { pb.OnClick.RemoveAllListeners(); } catch { }
                pb.enabled = false;
            }
        }

        private static void Step(RoleRow role, TextMeshPro valueText, int delta)
        {
            if (!RoleSettingsSync.CanEdit) return;
            int current = RoleSettingsSync.GetInt(role.Key + ".Count");
            int next = Mathf.Clamp(current + delta, 0, 15);
            RoleSettingsSync.SetInt(role.Key + ".Count", next);
            if (valueText != null) valueText.text = next.ToString(CultureInfo.InvariantCulture);
        }

        private static float ComputeStartY(SettingMenu menu)
        {
            if (menu.AllItems != null)
            {
                for (int i = menu.AllItems.Count - 1; i >= 0; i--)
                {
                    var item = menu.AllItems[i];
                    if (item != null && item.gameObject != null && item.gameObject.activeSelf)
                        return item.localPosition.y;
                }
            }
            return menu.YStart;
        }

        private static NumberOption FindNumberOptionRow(SettingMenu menu)
        {
            if (menu.AllItems == null) return null;
            for (int i = 0; i < menu.AllItems.Count; i++)
            {
                var item = menu.AllItems[i];
                if (item == null) continue;
                var number = item.GetComponentInChildren<NumberOption>(true);
                if (number != null) return number;
            }
            return null;
        }
    }
}

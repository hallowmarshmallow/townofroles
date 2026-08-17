#!/usr/bin/env python3
"""Apply local Classic Us 8.9 compatibility patches to a ManuAPI source tree."""
from pathlib import Path
import re
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("/tmp/manuapi/ManuAPI")
role_patches = root / "Roles" / "RolePatches.cs"
events = root / "Events" / "GameEventPatches.cs"
gamemodes = root / "GameModes" / "GameModePatches.cs"
settings = root / "Options" / "SettingsMenuAPI.cs"
for path in (role_patches, events, gamemodes, settings):
    if not path.is_file():
        raise SystemExit(f"ERROR: expected ManuAPI source file missing: {path}")

role_patches.write_text(role_patches.read_text().replace("_BeginTeam_d__35", "_BeginTeam_d__36"))
for path in (events, gamemodes):
    path.write_text(path.read_text().replace("nameof(MeetingHud.Start)", '"Start"'))

text = settings.read_text()
# Normalize any prior guard declaration, regardless of its old key type.
text = re.sub(r"^        private static readonly HashSet<[^>]+> _builtMenus = new\(\);\n", "", text, flags=re.MULTILINE)
marker = "        private static readonly List<Registration> _registrations = new();\n"
if marker not in text:
    raise SystemExit("ERROR: SettingsMenuAPI registration list not found")
text = text.replace(
    marker,
    marker + "        private static readonly HashSet<GameSettingMenu> _builtMenus = new();\n",
    1,
)

# Normalize the entire BuildAll prologue. OnEnable may run more than once, so
# the guard prevents duplicate rows and repeated scroller expansion.
prologue = re.compile(
    r"            if \(menu == null \|\| menu\.AllItems == null \|\| menu\.AllItems\.Count == 0\) return;\n"
    r"(?:            if \([^\n]*_builtMenus[^\n]*\n)?"
    r"            var parent = menu\.AllItems\[0\]\.parent;\n"
    r"(?:            if \([^\n]*parent[^\n]*\n)?"
    r"            var template = menu\.keyvaluePrefab;\n"
    r"(?:            if \([^\n]*template[^\n]*\n)?"
)
new = """            if (menu == null || menu.AllItems == null || menu.AllItems.Count == 0) return;
            if (_builtMenus.Contains(menu)) return;
            var parent = menu.AllItems[0].parent;
            var template = menu.keyvaluePrefab;
            if (parent == null || template == null) return;
            _builtMenus.Add(menu);
"""
text, count = prologue.subn(new, text, count=1)
if count != 1 and "if (_builtMenus.Contains(menu)) return;" not in text:
    raise SystemExit("ERROR: SettingsMenuAPI BuildAll prologue not found")

# SettingMenu.Start is not present in Classic Us 8.9 interop. OnEnable is the
# valid lifecycle method; BuildAll is safe there because of the guard above.
text = re.sub(
    r'\[HarmonyPatch\(typeof\(SettingMenu\),\s*(?:nameof\(SettingMenu\.OnEnable\)|nameof\(SettingMenu\.Start\)|"Start")\)\]',
    '[HarmonyPatch(typeof(SettingMenu), nameof(SettingMenu.OnEnable))]',
    text,
    count=1,
)
text = re.sub(r"SettingMenu_(?:Start|OnEnable)_Patch", "SettingMenu_OnEnable_Patch", text, count=1)
text = re.sub(r"SettingMenu_(?:Start|OnEnable)_Plugin", "SettingMenu_OnEnable_Patch", text, count=1)
text = text.replace("SettingsMenuAPI.BuildAll (Start): ", "SettingsMenuAPI.BuildAll (OnEnable): ", 1)
settings.write_text(text)

checks = {
    role_patches: "_BeginTeam_d__36",
    events: 'typeof(MeetingHud), "Start"',
    gamemodes: 'typeof(MeetingHud), "Start"',
    settings: 'private static readonly HashSet<GameSettingMenu> _builtMenus',
}
for path, needle in checks.items():
    if needle not in path.read_text():
        raise SystemExit(f"ERROR: ManuAPI 8.9 patch did not apply: {path} -> {needle}")
if '[HarmonyPatch(typeof(SettingMenu), nameof(SettingMenu.OnEnable))]' not in settings.read_text():
    raise SystemExit("ERROR: settings menu OnEnable patch did not apply")
if 'SettingMenu_OnEnable_Patch' not in settings.read_text():
    raise SystemExit("ERROR: settings menu patch class did not normalize")
print(f"ManuAPI 8.9 patches verified in {root}")

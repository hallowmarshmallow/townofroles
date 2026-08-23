#!/usr/bin/env python3
"""Apply local Classic Us 8.17 compatibility patches to a ManuAPI source tree.

Fixes the API drift between 2026.8.16 (the code's original target) and
2026.8.17 (the decompiled game). Mirrors tools/patch_manuapi_89.py.
"""
from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("/tmp/manuapi/ManuAPI")

targets = {
    "Abilities/AbilityButton.cs": None,
    "Roles/RoleRegistry.cs": None,
    "Roles/RolePatches.cs": None,
    "Roles/CustomRole.cs": None,
    "Assets/AssetBundleManager.cs": None,
    "Options/SettingsMenuAPI.cs": None,
}
for rel in targets:
    p = root / rel
    if not p.is_file():
        raise SystemExit(f"ERROR: expected ManuAPI source file missing: {p}")

# 1. TryCast<T>() on MonoBehaviour/Il2Cpp types -> C# `is` pattern
#    (the 8.17 interop types don't expose Il2CppInterop.TryCast on all bases)
ability = root / "Abilities/AbilityButton.cs"
t = ability.read_text()
t = t.replace("if (comp.TryCast<PassiveButton>() != null) continue;", "if (comp is PassiveButton) continue;")
t = t.replace("if (comp.TryCast<TextMeshPro>() != null) continue;", "if (comp is TextMeshPro) continue;")
t = t.replace("if (comp.TryCast<AspectPosition>() != null) continue;", "if (comp is AspectPosition) continue;")
ability.write_text(t)

# 2. GetIl2CppType() -> GetType() (plain .NET reflection works on interop types)
for rel in ("Roles/RoleRegistry.cs", "Roles/RolePatches.cs", "Roles/CustomRole.cs"):
    p = root / rel
    p.write_text(p.read_text().replace(".GetIl2CppType()", ".GetType()"))

# 3. AssetBundle.LoadFromFile removed in 8.17 -> drop the method body, and
#    LoadAsset<T> -> LoadAsset(string, Type)
abm = root / "Assets/AssetBundleManager.cs"
t = abm.read_text()
t = t.replace(
    "var bundle = AssetBundle.LoadFromFile(filePath);",
    "var bundle = AssetBundle.LoadFromFileAsync(filePath).assetBundle;",
)
t = t.replace(
    "return bundle.LoadAsset<T>(assetName);",
    "return bundle.LoadAsset(assetName, Il2CppType.Of<T>()).Cast<T>();",
)
abm.write_text(t)

# 4. SettingsMenuAPI.TryCast -> `is` / `as` pattern
settings = root / "Options/SettingsMenuAPI.cs"
t = settings.read_text()
t = t.replace(
    "var numOption = comp.TryCast<NumberOption>();",
    "var numOption = comp as NumberOption;",
)
t = t.replace(
    "var menu = SettingMenu.Instance.TryCast<SettingMenu>();",
    "var menu = SettingMenu.Instance as SettingMenu;",
)
settings.write_text(t)

# Verify
checks = {
    ability: "comp is PassiveButton",
    root / "Roles/RoleRegistry.cs": ".GetType()",
    root / "Roles/RolePatches.cs": ".GetType()",
    root / "Roles/CustomRole.cs": ".GetType()",
    abm: "LoadFromFileAsync",
    settings: "as NumberOption",
}
for p, needle in checks.items():
    if needle not in p.read_text():
        raise SystemExit(f"ERROR: ManuAPI 8.17 patch did not apply: {p} -> {needle}")

print(f"ManuAPI 8.17 patches verified in {root}")

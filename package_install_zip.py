#!/usr/bin/env python3
"""Build drop-in install zips for TownOfUs.ManuAPI.

Default target is Windows. Set TOU_PLATFORM=linux for the native Linux
archive. Set SKIP_TOWN_OF_US=1 to make a ManuAPI/Reactor-only diagnostic zip.
"""
import os
import shutil
import stat
import sys
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
STAGE = "/tmp/tou-install-stage"
PLATFORM = os.environ.get("TOU_PLATFORM", "windows").lower()
if PLATFORM not in ("windows", "linux"):
    raise SystemExit("TOU_PLATFORM must be windows or linux")
IS_LINUX = PLATFORM == "linux"
SKIP_TOWN_OF_US = os.environ.get("SKIP_TOWN_OF_US") == "1"
# Classic Us ships its own BepInEx 6.0.0-be.755 fork (winhttp.dll / libdoorstop.so)
# inside the game install, and builds.bepinex.dev is unreachable from CI, so the
# packager does NOT bundle the loader by default. Set SKIP_BEPINEX=0 only when a
# BEPINEX_ZIP (the official be.755 IL2CPP archive) is available locally.
SKIP_BEPINEX = os.environ.get("SKIP_BEPINEX", "1") != "0"

# Derive the mod version from source so the archive name tracks the build.
with open(os.path.join(ROOT, "TownOfUsPlugin.cs"), encoding="utf-8") as _f:
    _m = __import__("re").search(r'Version = "([^"]+)"', _f.read())
VERSION = _m.group(1) if _m else "0.0.0"

platform_suffix = "-linux" if IS_LINUX else ""
diagnostic_suffix = "-manuapi-only" if SKIP_TOWN_OF_US else "-full"
OUT_FULL = os.path.join(
    ROOT, f"TownOfUs.ManuAPI-v{VERSION}{platform_suffix}{diagnostic_suffix}.zip"
)
if SKIP_TOWN_OF_US:
    plugins_name = f"TownOfUs.ManuAPI-v{VERSION}{platform_suffix}-manuapi-only-plugins.zip"
elif IS_LINUX:
    plugins_name = f"TownOfUs.ManuAPI-v{VERSION}-linux-plugins.zip"
else:
    # Preserve the original Windows plugins-only filename for existing installs.
    plugins_name = f"TownOfUs.ManuAPI-v{VERSION}.zip"
OUT_PLUGINS = os.path.join(ROOT, plugins_name)
PLUGINS = os.path.join(STAGE, "BepInEx", "plugins")

# BepInEx 6.0.0-be.755 — the build used by the Classic Us mod stack.
# Linux uses libdoorstop.so + run_bepinex.sh; Windows uses winhttp.dll.
DEFAULT_BEPINEX_ZIP = (
    "/tmp/bepinex-linux-755.zip" if IS_LINUX else "/tmp/bepinex-il2cpp-755.zip"
)
BEPINEX_ZIP = os.environ.get("BEPINEX_ZIP", DEFAULT_BEPINEX_ZIP)

MANUAPI_PKG = os.environ.get(
    "MANUAPI_PKG",
    os.path.join(ROOT, "packages", "linux", "classicus.manuapi.1.5.2.nupkg")
    if IS_LINUX
    else os.path.join(ROOT, "packages", "ClassicUs.ManuAPI.1.7.1.nupkg"),
)
REACTOR_PKG = os.environ.get(
    "REACTOR_PKG",
    os.path.join(ROOT, "packages", "linux", "classicus.reactor.1.1.0.nupkg")
    if IS_LINUX
    else os.path.join(ROOT, "packages", "ClassicUs.Reactor.1.2.0.nupkg"),
)
MOD_DLL = os.environ.get(
    "MOD_DLL",
    os.path.join(ROOT, "bin", "Release", "linux", "TownOfUs.ManuAPI.dll")
    if IS_LINUX
    else os.path.join(ROOT, "bin", "Release", "TownOfUs.ManuAPI.dll"),
)
PATCHER_DLL = os.environ.get(
    "PATCHER_DLL",
    os.path.join(ROOT, "UpdaterPatcher", "bin", "Release", "TownOfUs.Updater.Patcher.dll"),
)

# Conditionals are hoisted out of the f-string because Python rejects literal
# newlines inside f-string expressions.
_loader_note = (
    "NOTE: Classic Us already ships its own BepInEx 6.0.0-be.755 fork, so this\n"
    "archive contains only the mod stack (plugins + patchers), NOT a second\n"
    "BepInEx copy - a duplicate loader would clash with the game's."
    if SKIP_BEPINEX
    else "Contents include the BepInEx 6.0.0-be.755 loader as well as the mod stack."
)
_loader_contents_line = (
    "  BepInEx/ + doorstop files       BepInEx 6.0.0-be.755 (IL2CPP)\n"
    if not SKIP_BEPINEX
    else ""
)
_loader_note2 = (
    "This archive contains the native "
    + ("Linux x64" if IS_LINUX else "Windows x64")
    + " BepInEx loader. Do not use it with the other operating system's game build."
    if not SKIP_BEPINEX
    else "The game's own BepInEx must already be present; this archive only adds plugins/ and patchers/."
)

README = f"""TownOfUs.ManuAPI v{VERSION} - {'Linux' if IS_LINUX else 'Windows'} install
{'=' * 76}

Extract this zip into your Classic Us GAME ROOT folder (the folder containing
the game executable).

{_loader_note}

Contents:
{_loader_contents_line}  BepInEx/plugins/TownOfUs.ManuAPI.dll          Town Of Us mod with configurable role settings
  BepInEx/plugins/ClassicUs.ManuAPI.dll         ManuAPI 1.7.1
  BepInEx/plugins/ClassicUs.Reactor.dll        Reactor 1.2.0
  BepInEx/patchers/TownOfUs.Updater.Patcher.dll Self-update applier (applies staged updates on launch)

{'Linux launch:' if IS_LINUX else 'Launch:'}
{'  chmod +x run_bepinex.sh  # normally preserved by extraction\n  ./run_bepinex.sh ./classicus.x86_64\n\n  Do not launch the Linux game binary directly: run_bepinex.sh sets\n  LD_PRELOAD for libdoorstop.so and starts BepInEx.' if IS_LINUX else '  Launch the Classic Us Windows executable normally.'}

First launch: BepInEx generates the game interop and config folder
(BepInEx/interop, BepInEx/config), so startup may take longer once.

Verify: BepInEx/LogOutput.log should contain:
  Town Of Us (ManuAPI port) loaded.
  Jester: enabled

Role toggles and command settings: edit BepInEx/config/TownOfUs.ManuAPI.cfg:
  [Crewmate Roles] Sheriff/Engineer/Medic/Seer/Vigilante and their values
  [Impostor Roles] Assassin and its values
  [Neutral Roles] Jester and its values
  [Menu] NativeMenuRows = true  (arrow in the native config menu -> tabbed role window)
  [Diagnostics] EnableGameplayHooks (optional Sheriff gameplay hooks)
  [Commands] Enabled, AlwaysCommandChat, and AllowSetRole

Role settings are grouped into stable BepInEx config sections instead of being
injected into the native Classic Us Game Options screen:
  [Crewmate Roles] Sheriff, Engineer, Medic, Seer, Vigilante
  [Impostor Roles] Assassin
  [Neutral Roles] Jester

The native Game Options screen is intentionally never patched because the public
ManuAPI settings-row path can freeze Classic Us 8.9. Role toggles, pool values,
and gameplay values are read from BepInEx/config/TownOfUs.ManuAPI.cfg; restart
the game after changing them.

Self-update:
  [Updates]
  Enabled = true
  ManifestUrl = https://github.com/OWNER/REPO/releases/latest/download/latest.json
  AllowDownload = true

  On launch the mod fetches ManifestUrl, and when the manifest reports a newer
  version it shows an in-game 'Update Available' prompt. Pressing Update
  downloads the DLL, verifies its SHA-256, and stages it; the bundled
  TownOfUs.Updater.Patcher preloader plugin applies it on the next launch.
  Update OWNER/REPO in ManifestUrl to your GitHub repo. Build a release with
  tools/publish_update.py.

Original Town Of Us Medic and Seer art is embedded in the plugin DLL and used
for their ability buttons. The cloned source has no standalone Vigilante icon,
so Vigilante keeps the native button art.

The Jester is a Neutral win-condition role. When the Jester is voted out,
the host ends the match and the result screen says "Jester Wins".
For Freeplay testing, the host can use /setrole Jester in the in-game chat.
Existing configs are migrated from the former [Roles], [Role Pool], [Gameplay],
[Sheriff], [Medic], [Seer], and [Assassin] sections on the next launch.

Linux crash diagnostic mode:
  [Diagnostics]
  EnableGameplayHooks = false

With gameplay hooks disabled, the Sheriff remains registered for the
Freeplay computer role selector, but TownOfUs does not patch the HUD or
meeting lifecycle. Set this to true only after selector-only mode launches
without a native crash.

Notes:
  - ClassicUs.ManuAPI.dll 1.7.1 includes the 8.9
    IntroCutscene+_BeginTeam_d__35 fix.
  - {_loader_note2}
"""


def _check(path, hint):
    if not os.path.exists(path):
        print(f"ERROR: missing {path}")
        print(f"  {hint}")
        sys.exit(1)


def _mark_linux_executables(stage):
    """Restore execute bits required by the Linux launcher/native libraries.

    The downloaded be.755 archive currently records these files as 0644, so
    relying on extraction alone would leave the launcher unusable.
    """
    for dirpath, _, files in os.walk(stage):
        for name in files:
            full = os.path.join(dirpath, name)
            if name == "run_bepinex.sh" or name.endswith(".so"):
                os.chmod(
                    full,
                    os.stat(full).st_mode
                    | stat.S_IXUSR
                    | stat.S_IXGRP
                    | stat.S_IXOTH,
                )


def _zip_dir(stage, out):
    if os.path.exists(out):
        os.remove(out)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for dirpath, _, files in os.walk(stage):
            for name in files:
                full = os.path.join(dirpath, name)
                rel = os.path.relpath(full, stage)
                mode = stat.S_IMODE(os.stat(full).st_mode)
                info = zipfile.ZipInfo.from_file(full, rel)
                info.external_attr = (stat.S_IFREG | mode) << 16
                with open(full, "rb") as source:
                    z.writestr(info, source.read(), compress_type=zipfile.ZIP_DEFLATED)


def _extract_runtime_dll(package, entry, destination):
    with zipfile.ZipFile(package) as z:
        z.extract(entry, STAGE)
    extracted = os.path.join(STAGE, *entry.split("/"))
    shutil.move(extracted, os.path.join(destination, os.path.basename(entry)))


def main():
    if not SKIP_TOWN_OF_US:
        _check(
            MOD_DLL,
            "Build first and set MOD_DLL to the platform-specific TownOfUs.ManuAPI.dll when using a custom output folder.",
        )
    if not SKIP_BEPINEX:
        _check(
            BEPINEX_ZIP,
            f"Download BepInEx 6.0.0-be.755 IL2CPP {'linux-x64' if IS_LINUX else 'win-x64'} "
            "from https://builds.bepinex.dev/projects/bepinex_be/755/ and set BEPINEX_ZIP.",
        )
    _check(
        MANUAPI_PKG,
        "Build/restore the platform-specific ManuAPI package first (Linux: tools/build_linux.sh).",
    )
    _check(
        REACTOR_PKG,
        "Restore/copy the platform-specific ClassicUs.Reactor 1.1.0 package first.",
    )

    if os.path.exists(STAGE):
        shutil.rmtree(STAGE)
    os.makedirs(PLUGINS)

    # 1. Bundled BepInEx distribution -> game root layout (optional: Classic Us
    # ships its own be.755 fork, and the official download is unreachable).
    if not SKIP_BEPINEX:
        with zipfile.ZipFile(BEPINEX_ZIP) as z:
            z.extractall(STAGE)

    # 2. The managed mod stack into BepInEx/plugins.
    if not SKIP_TOWN_OF_US:
        shutil.copy(
            MOD_DLL,
            PLUGINS,
        )
        # 2b. Preloader patcher that applies staged self-updates on launch.
        _check(
            PATCHER_DLL,
            "Build UpdaterPatcher first: cd UpdaterPatcher && dotnet build -c Release",
        )
        patchers_dir = os.path.join(STAGE, "BepInEx", "patchers")
        os.makedirs(patchers_dir, exist_ok=True)
        shutil.copy(PATCHER_DLL, patchers_dir)
    _extract_runtime_dll(
        MANUAPI_PKG, "lib/net6.0/ClassicUs.ManuAPI.dll", PLUGINS
    )
    _extract_runtime_dll(
        REACTOR_PKG, "lib/net6.0/ClassicUs.Reactor.dll", PLUGINS
    )
    shutil.rmtree(os.path.join(STAGE, "lib"), ignore_errors=True)

    if IS_LINUX:
        _mark_linux_executables(STAGE)

    # 3. README at the archive root.
    with open(os.path.join(STAGE, "README.txt"), "w", encoding="utf-8") as f:
        f.write(README)

    # 4. Full zip: game-root layout with bundled BepInEx.
    _zip_dir(STAGE, OUT_FULL)
    size_mb = os.path.getsize(OUT_FULL) / (1024 * 1024)
    print(f"Wrote {OUT_FULL} ({size_mb:.1f} MB) - extract into game root")

    # 5. Plugins-only zip: for installs that already have BepInEx.
    plugins_stage = os.path.join(STAGE, "plugins-only")
    os.makedirs(os.path.join(plugins_stage, "BepInEx", "plugins"))
    mod_names = ["ClassicUs.ManuAPI.dll", "ClassicUs.Reactor.dll"]
    if not SKIP_TOWN_OF_US:
        mod_names.insert(0, "TownOfUs.ManuAPI.dll")
    for name in mod_names:
        shutil.copy(
            os.path.join(PLUGINS, name),
            os.path.join(plugins_stage, "BepInEx", "plugins", name),
        )
    if not SKIP_TOWN_OF_US:
        os.makedirs(os.path.join(plugins_stage, "BepInEx", "patchers"), exist_ok=True)
        shutil.copy(PATCHER_DLL, os.path.join(plugins_stage, "BepInEx", "patchers", "TownOfUs.Updater.Patcher.dll"))
    _zip_dir(plugins_stage, OUT_PLUGINS)
    print(
        f"Wrote {OUT_PLUGINS} "
        f"({os.path.getsize(OUT_PLUGINS) / 1024:.0f} KB) - plugins only"
    )


if __name__ == "__main__":
    main()

# Town Of Roles

A port of the classic, deprecated **Town-Of-Us** Among Us role mod onto the modern
**Classic Us** game, built on **ClassicUs.Reactor** (networking) and
**ClassicUs.MarshAPI** (roles / abilities / kills / UI), both by TechDevOfficial, and i forked them and updated.

```
Classic Us (game, DlovanSl)
   └─ BepInEx (IL2CPP)
        └─ ClassicUs.Manactor (networking / handshake / RPCs)
             └─ ClassicUs.ManuAPI (roles, abilities, kills, settings UI)
                  └─ TownOfUs.ManuAPI (this mod)
```

## Build

Requires the .NET SDK (the project targets `net6.0`):

```bash
dotnet build -c Release
```

Output: `bin/Release/TownOfUs.ManuAPI.dll`.

> **Important:** the `ClassicUs.GameLibs` package on nuget.org is malformed (backslash
> paths inside the archive), so NuGet finds no assets in it and the build would fail with
> hundreds of “type not found” errors. This repo carries a repacked, working copy in
> `packages/` and consumes it through the `LocalPackages` feed in `nuget.config`. See
> `PORTING.md` → “Known upstream issue” for how to refresh it after a GameLibs version bump.

### Install

Ready-made packages:

- **Windows:** `TownOfUs.ManuAPI-v0.1.0-full.zip` (~33 MB) — self-contained
  win-x64 BepInEx. Extract into the Windows game root and launch normally.
- **Linux:** `TownOfUs.ManuAPI-v0.1.0-linux-full.zip` (~32 MB) — self-contained
  Linux-x64 BepInEx. Extract into the Linux game root, then launch through
  `run_bepinex.sh` (not the game binary directly):
  ```bash
  chmod +x run_bepinex.sh
  ./run_bepinex.sh ./classicus.x86_64
  ```
- **Plugins-only:** the matching `*-plugins.zip` files are for installs that
  already have the correct platform's BepInEx loader.

To regenerate the Windows package:
```bash
python3 package_install_zip.py
```
To build the native Linux interop + managed DLL and regenerate its packages:
```bash
bash tools/build_linux.sh
TOU_PLATFORM=linux python3 package_install_zip.py
```

During development you can also copy the DLL automatically after every build by setting
`ClassicUsGameDir` to the game folder:

```bash
dotnet build -c Release -p:ClassicUsGameDir="C:\Path\To\ClassicUs"
```

Either way the mod needs **ManuAPI 1.5.2** and **Manactor 1.1.0** in the same
`BepInEx/plugins` folder (both are included in the zips). The Linux package uses
DLLs rebuilt against the Linux 8.9 `GameAssembly.so`; the Windows package uses
DLLs rebuilt against the Windows 8.9 `GameAssembly.dll`. ManuAPI 1.5.2 is the
**local 8.9-compatible rebuild** — do *not* replace it with the stock nuget.org
1.5.1, which crashes on Classic Us 8.9 (see Compatibility).

### Config and in-game settings

`BepInEx/config/TownOfUs.ManuAPI.cfg` is created on first launch. Each role has an
on/off toggle — a disabled role is never registered, so it cannot be assigned.
Role toggles and command availability require a restart after changing them.
The role settings are stored in three BepInEx configuration sections that act as
stable role tabs without modifying Classic Us' native Game Options screen:

- `[Crewmate Roles]` — Sheriff, Engineer, Medic, Seer, and Vigilante
- `[Impostor Roles]` — Assassin
- `[Neutral Roles]` — Jester

The native Game Options screen is intentionally never patched. ManuAPI's public
settings builder only appends rows to the native scroller; on Classic Us 8.9 that
path can freeze when the menu opens. Role toggles, gameplay config, commands, and
Freeplay role selection are available through the config file and commands instead:

- role count and spawn chance for Sheriff, Engineer, Jester, Medic, Seer, and Vigilante
- Sheriff and Engineer cooldowns
- Medic shield uses, cooldown, and whether a blocked kill consumes the shield
- Seer investigation uses, cooldown, and Faction versus exact Role reveal
- Vigilante shot count and cooldown

These settings are read when the mod starts; restart the game after changing role
toggles or pool values. The `Class::Init signatures have been exhausted` line is an
Il2CppInterop warning emitted during delegate setup. It is not the settings fix,
and the mod no longer injects any custom rows into the native Game Options menu.


```ini
[Crewmate Roles]
Sheriff = true
SheriffCount = 1
SheriffChance = 100
SheriffKillCooldown = 10
SheriffKillOther = true
SheriffBodyReport = false
Engineer = true
EngineerCount = 1
EngineerChance = 100
EngineerFixCooldown = 30
Medic = true
MedicCount = 1
MedicChance = 100
MedicUses = 1
MedicCooldown = 0
MedicShieldBreaksOnKill = true
Seer = true
SeerCount = 1
SeerChance = 100
SeerUses = 1
SeerCooldown = 0
SeerRevealMode = Faction
Vigilante = true
VigilanteCount = 1
VigilanteChance = 100
VigilanteShots = 1
VigilanteCooldown = 0

[Impostor Roles]
Assassin = true
AssassinCount = 1
AssassinChance = 100
AssassinMultiKill = false
AssassinMeetingButtons = true

[Neutral Roles]
Jester = true
JesterCount = 1
JesterChance = 100

[Presentation]
Enabled = true
DeadSeeRoles = true
ImpostorSeeRoles = false

[Diagnostics]
EnableGameplayHooks = false

[Commands]
Enabled = true
AlwaysCommandChat = true
AllowSetRole = true
```

## In-game slash commands

Commands are entered in the Classic Us chat box and are consumed locally instead
of being sent as normal chat. Commands that affect the lobby or another player
require the current host:

```text
/forcestart
/nickname <new name>
/color gradient [on|off]
/color rainbow [on|off]
/gradient [on|off]
/rainbow [on|off]
/system <message>
/tpin [player name or player id]
/tpout [player name or player id]
/nogameend [on|off]
/setrole <role>
/setrole <player name or player id> <role>
/revive [player name or player id]

# Examples: /setrole Jester, /setrole Medic, /setrole Seer, /setrole Vigilante
/guess <player name or id> <role>  # Assassin only, during meetings
```

`/revive` is host-only and uses Classic Us 8.9's native `PlayerControl.Revive()`
operation. `/setrole Jester` assigns the toggleable Jester for testing; the current
host must have `Jester = true` in `[Neutral Roles]`.
When the Jester is exiled by a meeting vote, the host ends the match through
`ShipStatus.StartEndGame(GameOverReason.Custom, ...)`; the result screen is
changed to **Jester Wins** on every client through an authenticated Manactor
host event. With no target it revives the local host; with a player name or ID it
revives that player. `/tpin` and `/tpout` use the current map's native spawn locations as the
inside/outside dropship positions. They are intended for lobby/freeplay testing;
use them only when a map is loaded. `/system` is host-only and uses the game's
native system-alert RPC. `/rainbow` uses Classic Us 8.9's native
`PlayerColorSetter.EnableRainbowMode()` and is a local visual effect; `/gradient`
applies the hardcoded hallowmarsh blue/pink gradient to your own body via the
game's native `PlayerMaterial` tint path. Neither command injects a shader or
spams color RPCs.

The command layer is disabled with:

```ini
[Commands]
Enabled = false
```

## Current status — skeleton

| Area | Status |
|---|---|
| Project scaffolding (builds against real 8.9 interop + ManuAPI 1.5.2 rebuilt for 8.9) | ✅ |
| **Sheriff** (Crewmate) — worked example | ✅ native Kill button, correct/wrong-target kill logic, suicide on miss, self-report suppression, cross-client kill tracking via `[ManactorRpc]`, config toggle |
| **Engineer** (Crewmate) — native vent + Fix Sab slice | ✅ virtual role, Freeplay computer selection, native vent permission/networking, native Fix Sab button, config toggle; ⏳ classic vent cooldown/limited-time/repair extras |
| **Medic** (Crewmate) — one-use shield | ✅ host-authoritative shield RPC, murder cancellation, toggle, original Town Of Us Medic art embedded |
| **Seer** (Crewmate) — one-use faction investigation | ✅ host-authoritative investigation RPC, result delivery, toggle, original Town Of Us Seer art embedded |
| **Vigilante** (Crewmate) — one-shot impostor kill / self-kill on mistake | ✅ host-authoritative shot RPC and toggle; no standalone Vigilante icon exists in the cloned source |
| **Jester** (Neutral win condition) — voted-out win, explicit Jester Wins result | ✅ toggleable virtual role, Freeplay selector, host-authoritative exile win |
| Janitor, Mayor, Swapper, other neutral roles, modifiers, Lovers | ⏳ next port batches; original Janitor and Swapper art is already bundled for their implementations |

The **Sheriff** is the template role: copy `Roles/Sheriff/` and rename to add the next
role. `PORTING.md` has the old-TOU → ManuAPI mapping table and a recommended order.

> The three role sections are BepInEx configuration sections, not actual tabs inside
> the game's Game Options screen. Classic Us 8.9's native settings list is left
> untouched intentionally to avoid the freeze caused by custom row injection.

## Compatibility

Targets **Classic Us 8.9** on Windows and native Linux. Both were verified against
real 8.9 interop generated from each platform's actual game binary with the exact
pipeline BepInEx uses at runtime (Cpp2IL `dummydll` → Il2CppInterop generator).
The Linux input is the native ELF `GameAssembly.so`; the Windows input is
`GameAssembly.dll`.

**⚠️ 8.9 broke upstream ManuAPI — this repo ships a rebuilt ManuAPI.** The maintainers'
"nothing much changed" turned out to be wrong for the interop surface: stock ManuAPI
1.5.1 crashes on load with

```
System.TypeLoadException: Could not load type 'IntroCutscene+_BeginTeam_d__35'
from assembly 'Assembly-CSharp'
```

because 8.9 renumbered a compiler-generated coroutine state machine
(`<BeginTeam>d__35` → `d__36`), and the `MeetingHud.Start` stub changed. The zips
therefore contain **ManuAPI rebuilt from source against the real 8.9 interop**, packaged
as **1.5.2** locally (the DLL keeps assembly version 1.5.1, so configs/other mods are
unaffected). The version bump is deliberate: stock nuget.org 1.5.1 is broken on 8.9, and
if the local feed ever goes missing the restore fails *loudly* instead of silently
building a mod that crashes at runtime. The three drift fixes and the full regeneration
pipeline are documented in `PORTING.md`.

If a future game update breaks the build again, the error will point at the changed
stub; if it breaks silently at runtime, re-run the interop diff from `PORTING.md`.

## Project layout

```
TownOfUs.ManuAPI.csproj     # net6.0; refs GameLibs/Manactor/ManuAPI (+ local repack)
TownOfUsPlugin.cs           # BepInEx entry: registers mod, RPCs, roles, GameEvents hooks
Core/
  ClosestPlayerFinder.cs    # shared nearest-target-in-kill-range helper
Roles/Sheriff/              # ← the worked-example / template role
  SheriffRole.cs            #   virtual Crewmate role descriptor
  SheriffAbility.cs         #   CustomAbility shoot button (+ per-frame holder)
  SheriffSystem.cs          #   shoot logic, kill-record RPC, self-report suppression
  SheriffPatches.cs         #   optional Sheriff kill hook
Roles/Engineer/              # native vent + Fix Sab role
Roles/MedicRole.cs            # one-use shield role descriptor
Roles/MedicSystem.cs          # shield RPC + BeforeMurder cancellation
Roles/SeerRole.cs             # one-use investigation descriptor
Roles/SeerSystem.cs           # faction investigation RPC/result
Roles/VigilanteRole.cs        # one-shot vigilante descriptor
Roles/VigilanteSystem.cs      # host-authoritative shot logic
Roles/FirstBatchPatches.cs    # first-batch button routing/art refresh
Assets/OriginalTownOfUs/Roles/ # cloned original Town Of Us PNG resources
  EngineerRole.cs            #   virtual Crewmate role descriptor
  EngineerAbility.cs         #   native repair call and button refresh
  EngineerPatches.cs         #   targeted native button hooks
Commands/
  CommandSystem.cs           #   host-gated in-game slash commands
  CommandPatches.cs          #   chat interception + local visual tick
Core/
  CommandConfig.cs           #   command toggle
  VisualEffects.cs           #   local stepped color effects
  Options.cs                #   ported Town-Of-Us tuning values
packages/                   # local feeds: Windows packages + packages/linux native-Linux packages
tools/
  repack_gamelibs.py        # normalize the malformed nuget.org GameLibs nupkg (forward slashes)
  update_gamelibs.py        # swap a new interop Assembly-CSharp.dll into the GameLibs nupkg
  update_manuapi.py         # swap the rebuilt ManuAPI DLL into the nupkg + bump version
  build_manuapi.sh          # rebuild Windows ManuAPI from source + repack
  build_linux.sh             # generate/use Linux interop + rebuild native-Linux stack
  _nupkg_utils.py           # shared nupkg rewrite (extract/modify/rezip, strips stale signature)
```

## Original Town Of Us assets

The mod now copies and embeds the original PNG files directly from:

```text
Town-Of-Us/source/Resources/
```

All 30 root resource PNGs plus the root `glitchbundle` are preserved byte-for-byte in
`Assets/OriginalTownOfUs/Resources/`. The currently implemented role buttons use
`Engineer.png`, `Medic.png`, and `Seer.png`; `Janitor.png`, `Revive.png`,
`Douse.png`, and `Ignite.png` are also embedded and ready for their role batches.
Large composite/UI files such as `NormalKill.png`, `ShiftKill.png`, `Vote1.png`,
and `Vote2.png` are preserved but are not used as button icons. The `Hats/`
subdirectory is intentionally not bundled because it is unrelated to role
abilities. The root `glitchbundle` is included for future Glitch-role support but
is not loaded by the current role buttons. No replacement or AI-generated art is used.

## Credits

- **ClassicUs** game — DlovanSl
- **ManuAPI / Manactor** — TechDevOfficial (source: `github.com/TechDevOfficial/ClassicUs.ManuAPI`, `.../ClassicUs.Manactor`)
- **Town Of Us** (original) — slushiegoose et al. (archived: `github.com/slushiegoose/Town-Of-Us`)

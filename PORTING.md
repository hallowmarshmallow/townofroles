# Porting guide: Town-Of-Us (old) → ManuAPI

How each classic Town-Of-Us pattern maps onto the ManuAPI/Reactor model. The Sheriff in
`Roles/Sheriff/` is the reference implementation of every row below.

## Pattern mapping

| Old Town-Of-Us (Mono/BepInEx) | ManuAPI / Reactor equivalent |
|---|---|
| `Role` subclass + 4–6 Harmony patch files per role | One `CustomCrewmateRole` / `CustomImpostorRole` / `CustomRole` descriptor + one `CustomAbility` |
| Hand-cloned `KillButtonManager` + manual cooldown in `HudManager.Update` | `CustomAbility.Tick(hud)` from a `HudManager.FixedUpdate` patch; `AbilityButton` handles cooldown/visibility |
| `KillButtonManager.PerformKill` prefix | `CustomAbility.OnActivate()` → `KillManager.Kill(killer, target, KillRequest)` (host-authoritative, networked) |
| `PlayerControl.ReportClosest` prefix | `GameEvents.BeforeReport += handler` + set `args.Cancelled = true` |
| `PlayerControl.FixedUpdate` report-button hiding | `GameEvents.BeforeReport` cancellation (skeleton) or a HUD patch (nicety) |
| `Utils.SetTarget(ref role.ClosestPlayer, KillButton)` | `Core/ClosestPlayerFinder.GetClosestTarget(player, out target)` (shared) |
| `Utils.RpcMurderPlayer(...)` + `CustomRPC` byte handlers | `KillManager.Kill` + `[ReactorRpc("key")]` handler / `ReactorAPI.SendRpcMethod(key, ...)` |
| `Murder.KilledPlayers` (static list) | Per-role static tracker synced over the role's own RPC (see `SheriffSystem`) |
| `CustomGameOptions.*` | `Roles/<Role>/Options.cs` static class; expose through the role's BepInEx section |
| `RoleEnum.X` / `role.Is(RoleEnum.X)` | `RoleRegistry.IsAssigned(player, "<mod>.<Role>")` |
| `MeetingHud.Start` / `ExileController` cleanup | `MeetingHud.Start` + `HudManager.Start` postfixes calling `X.Reset()` |
| `CustomGameMode` win conditions (Jester/Arsonist/Glitch/Executioner) | Subclass `CustomGameMode` and override `CheckEndCriteria()` (ManuAPI handles game over + networking) |

## Rules of thumb

1. **Prefer virtual roles** (`RoleRegistry.RegisterVirtual`) — they ride on the vanilla
   Crewmate/Impostor backing role, so no IL2CPP type injection is needed. `Register()`
   (native IL2CPP roles) is only for rare cases that truly need a custom `RoleBehaviour`
   subclass.

### ⚠️ Game ships native role classes — name collisions

Classic Us implements several classic roles **natively** (`SheriffRole`, `EngineerRole`,
`MedicRole`, `JesterRole`, … live in the *global namespace* of `Assembly-CSharp`). C#
resolves simple names up the enclosing-namespace chain *including the global namespace*
**before** consulting `using` directives — so inside `TownOfUs.ManuAPI`, a bare
`new SheriffRole()` binds to the **game's** class, not your descriptor.

Rules that keep the port working:
- In files whose namespace chain does **not** contain your role folder, fully-qualify the
  descriptor: `new TownOfUs.ManuAPI.Roles.Sheriff.SheriffRole()` (see `TownOfUsPlugin.cs`).
- Do **not** use a `using X = ...` alias to dodge this — C# raises CS0576
  ("global namespace contains a definition conflicting with alias").
- Inside the role's own folder namespace (e.g. `...Roles.Sheriff`) bare names are fine
  because the namespace chain finds your class first.
- When in doubt, check the game's type list for your role name (`strings Assembly-CSharp.dll | grep -i <role>`) and adjust.
2. **Register** the descriptor and RPC handlers once in `TownOfUsPlugin.Load()`.

### Adding a role toggle (config)

Every role gets an on/off toggle in one of the three role sections in
`BepInEx/config/TownOfUs.ManuAPI.cfg` — `[Crewmate Roles]`, `[Impostor Roles]`, or
`[Neutral Roles]`:
1. Add the `ConfigEntry<bool>` in `Core/RoleConfig.cs` via
   `BindRoleToggle(config, "<Role section>", "<Role>", "<help text>")`.
2. In `TownOfUsPlugin.Load()`, guard the role's registration block with its `.Value`
   (copy `RegisterSheriff()`). A disabled role is never registered → never assigned.
3. Keep the `Unload()` unsubscriptions symmetric with the enabled-state guard.
3. **Host-authoritative state**: request via a `RequestX` RPC; the host validates, applies,
   and broadcasts the result — exactly like `CloakSystem` in the official ModExample
   (`github.com/TechDevOfficial/ClassicUs.ManuAPI.ModExample`).
4. **Clear per-round state** in the `MeetingHud.Start` / `HudManager.Start` postfixes.
5. Custom abilities are auto-cleared by ManuAPI on death / game restart / game end — don't
   fight that; hook `GameEvents.GameStarted`/`GameEnded` for extra cleanup.

## Recommended order (old TOU role list, 20 roles + modifiers + Lovers)

1. ✅ **Sheriff** (done — template role)
2. **Mayor** — vote-weight; needs `GameEvents.AtMeeting` + a `[ReactorRpc]` to sync the
   extra votes (old `MayorMod`). Great second example of state sync.
3. **Engineer** — vent ability; `CustomAbility` + `GameEvents.PlayerEnteredVent`/`ExitedVent`.
4. Neutral win-condition roles — **Jester** (ejected win), **Arsonist** (douse → ignite),
   **Executioner** (target exiled), **Glitch** (hack/kill/mimic): each becomes a
   `CustomRole` (neutral team) + `CustomGameMode.CheckEndCriteria()` override + RPCs.
5. **Impostor roles** — **Janitor** (clean bodies; needs `Physics2D.OverlapCircleAll` vs
   `DeadBody` colliders — non-player targeting), **Morphling** (disguise sync RPC),
   **Swooper**, **Miner**, **Underdog**, **Camouflage**, **Assassin**, **Undertaker**.
6. **Crewmate roles** — **Medic** (one-shot shield: `GameEvents.BeforeMurder` cancellation +
   sync RPC), **Seer**, **Snitch**, **Spy**, **Swapper**, **TimeLord**, **Altruist**
   (revive RPC), **Investigator**.
7. **Modifiers + Lovers** — small `CustomRole`-less behaviors; use HUD/event patches.

## Version pin & interop drift (Classic Us 8.9 incident)

Targets `ClassicUs.GameLibs 2026.7.11.1` (repacked, real 8.9 interop) + `ManuAPI 1.5.2`
(a local rebuild of 1.5.1 from source, fixed for 8.9) + `Reactor 1.1.0`. Compatible
with Classic Us **8.9** on Windows and native Linux. The platform packages are kept
separate under `packages/` and `packages/linux/`; do not mix the generated GameLibs
or ManuAPI DLLs between OS builds.

**What actually happened on 8.9:** stock ManuAPI 1.5.1 crashed on load with
`TypeLoadException: Could not load type 'IntroCutscene+_BeginTeam_d__35'`. The game dev
changed the body of `IntroCutscene.BeginTeam`, so IL2CPP renumbered its compiler-generated
coroutine state machine `d__35` → `d__36`; ManuAPI's Harmony patch still referenced the
old type. This is *interop drift*: it compiles against the old stubs, then dies at runtime.

The fixes applied (all in `tools/build_manuapi.sh`):
1. `Roles/RolePatches.cs`: `_BeginTeam_d__35` → `_BeginTeam_d__36`.
2. `Events/GameEventPatches.cs` + `GameModes/GameModePatches.cs`:
   `nameof(MeetingHud.Start)` → `"Start"` (patch by string name so visibility/name
   changes never break the build again — Harmony resolves methods via reflection).
3. Our own `Roles/Sheriff/SheriffPatches.cs` uses `[HarmonyPatch(typeof(MeetingHud), "Start")]`
   for the same reason.

**How the real 8.9 interop was generated** (the exact BepInEx runtime pipeline, run
on Linux against the Windows game files):

```bash
# 1. extract from the game zip: GameAssembly.dll + il2cpp_data/Metadata/global-metadata.dat
# 2. Cpp2IL (build from github.com/SamboyCoding/Cpp2IL):
dotnet Cpp2IL.dll --game-path "<GameRoot>" --output-as dummydll --output-to /tmp/interop
# 3. Il2CppInterop generator (build Il2CppInterop.CLI from github.com/BepInEx/Il2CppInterop;
#    its global.json pins SDK 8 — bump it to your installed SDK if the build refuses):
export DOTNET_ROLL_FORWARD=LatestMajor  # CLI targets net6.0
dotnet /path/to/Il2CppInterop.CLI.dll generate --input /tmp/interop --output /tmp/interop-real \
    --game-assembly "<GameRoot>/GameAssembly.dll"
# 4. swap Assembly-CSharp.dll into the local GameLibs package (expects the repacked,
#    forward-slash nupkg — not the raw nuget.org download):
python3 tools/update_gamelibs.py packages/classicus.gamelibs.2026.7.11.1.nupkg /tmp/interop-real/Assembly-CSharp.dll
# 5. rebuild ManuAPI (applies the drift fixes, builds, repacks into the feed as 1.5.2):
bash tools/build_manuapi.sh
# 6. clear the nuget cache so the rebuilt packages are picked up:
rm -rf ~/.nuget/packages/classicus.gamelibs ~/.nuget/packages/classicus.manuapi && dotnet restore --force

### Native Linux build

The managed assemblies are platform-neutral at the C# level, but IL2CPP interop
assemblies are generated from native game binaries. Build the Linux stack separately:

```bash
# After generating /tmp/interop89-linux-real/Assembly-CSharp.dll from Linux
# GameAssembly.so (Cpp2IL + Il2CppInterop generator above):
bash tools/build_linux.sh
TOU_PLATFORM=linux python3 package_install_zip.py
```

`tools/build_linux.sh` uses `packages/linux/` and a separate NuGet cache, so it does
not overwrite the Windows package. The resulting Linux package contains
`libdoorstop.so` and `run_bepinex.sh`; launch it with:

```bash
chmod +x run_bepinex.sh
./run_bepinex.sh ./classicus.x86_64
```

Do not launch the Linux game executable directly after installing BepInEx: the
launcher sets `LD_PRELOAD` for Doorstop. The packager stores `0755` on the launcher
and native `.so` files, but `chmod +x` is safe to repeat after extraction.
```

Notes: the generator renames `<...>` compiler-generated types to `_..._` form (e.g.
`<BeginTeam>d__36` → `_BeginTeam_d__36`) and tags them with `[ObfuscatedName(...)]` — the
raw Cpp2IL output does **not**, so always run step 3 or the type names won't match what
BepInEx generates at runtime. After any future game update, re-run the diff: compare
`_BeginTeam_d__NN` / `MeetingHud.Start` / `ExileController._Animate_d__NN` (and any
`nameof(...)` you add) between the old and new interop before trusting the build.

Why the rebuilt ManuAPI is versioned **1.5.2** locally: interop drift is *compile-silent*
(stock 1.5.1 builds fine and crashes at runtime), so keeping the same version would let a
stray nuget.org 1.5.1 silently shadow the fix. The local feed only carries 1.5.2 — if it
goes missing, restore fails loudly.

## Known upstream issue: the GameLibs nuget package is malformed

The `ClassicUs.GameLibs` package on nuget.org uses **backslash path separators** inside
the archive (`ref\net6.0\...`), so NuGet sees **zero usable compile assets** — every
game/BepInEx/Unity type fails to resolve. Workaround (already applied in this repo):
`packages/classicus.gamelibs.2026.7.11.1.nupkg` is a repacked copy with normalized
forward-slash paths, consumed via the `LocalPackages` feed in `nuget.config`.

To refresh it after a future GameLibs bump:

```bash
# 1. download the new version, then normalize paths with the helper:
python3 tools/repack_gamelibs.py \
  <downloaded>.nupkg packages/classicus.gamelibs.<version>.nupkg
# 2. bump the version in TownOfUs.ManuAPI.csproj
# 3. dotnet restore --force && dotnet build -c Release
```

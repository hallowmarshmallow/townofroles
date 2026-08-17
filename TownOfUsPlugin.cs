using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using TownOfUs.ManuAPI.Core;
using TownOfUs.ManuAPI.Commands;
using System;
using TownOfUs.ManuAPI.Roles.Engineer;
using TownOfUs.ManuAPI.Roles;
using TownOfUs.ManuAPI.Roles.Jester;
using TownOfUs.ManuAPI.Roles.Medic;
using TownOfUs.ManuAPI.Roles.Seer;
using TownOfUs.ManuAPI.Roles.Sheriff;
using TownOfUs.ManuAPI.Roles.Vigilante;
using TownOfUs.ManuAPI.Roles.Assassin;
using TownOfUs.ManuAPI.Roles.Janitor;
using TownOfUs.ManuAPI.Roles.Altruist;
using TownOfUs.ManuAPI.Roles.Mayor;
using TownOfUs.ManuAPI.Roles.Executioner;
using TownOfUs.ManuAPI.Roles.Arsonist;
using TownOfUs.ManuAPI.Roles.Swapper;
using TownOfUs.ManuAPI.Roles.Morphling;
using TownOfUs.ManuAPI.Roles.Spy;
using TownOfUs.ManuAPI.Roles.Modifiers;
using TownOfUs.ManuAPI.Roles.Camouflager;
using TownOfUs.ManuAPI.Roles.Swooper;
using TownOfUs.ManuAPI.Roles.Underdog;
using TownOfUs.ManuAPI.Roles.Undertaker;
using TownOfUs.ManuAPI.Roles.Investigator;
using TownOfUs.ManuAPI.Roles.TimeLord;
using TownOfUs.ManuAPI.Roles.Snitch;
using TownOfUs.ManuAPI.Roles.Phantom;
using TownOfUs.ManuAPI.Roles.Shifter;
using TownOfUs.ManuAPI.Roles.Glitch;
using TownOfUs.ManuAPI.Roles.Miner;

namespace TownOfUs.ManuAPI
{
    /// <summary>
    /// Town Of Us for Classic Us — a port of the classic Town-Of-Us role mod onto the
    /// ManuAPI + Manactor framework.
    ///
    /// Skeleton status: the Sheriff (crewmate) is the worked example and demonstrates the
    /// full pattern — a virtual role descriptor, a CustomAbility button, host-authoritative
    /// kills through KillManager, a companion Manactor RPC for cross-client state, and a
    /// GameEvents hook (self-report suppression).
    /// </summary>
    [BepInPlugin(Guid, "Town Of Us", Version)]
    [BepInDependency(ManactorPlugin.Guid)]
    [BepInDependency(ManuAPIPlugin.Guid)]
    public sealed class TownOfUsPlugin : BasePlugin
    {
        public const string Guid = "townofus.manuapi";
        public const string Version = "0.7.14";

        private static bool _harmonyHooksInstalled;
        private static bool _eventHooksInstalled;
        private static bool _engineerEventHooksInstalled;
        private static bool _jesterEventHooksInstalled;
        private static bool _jesterHarmonyInstalled;
        private static bool _batchEventHooksInstalled;
        private static bool _batchHarmonyInstalled;
        private static bool _medicEventHooksInstalled;
        private static bool _seerEventHooksInstalled;
        private static bool _vigilanteEventHooksInstalled;
        private static bool _assassinEventHooksInstalled;
        private static bool _assassinHarmonyInstalled;
        private static bool _presentationHarmonyInstalled;
        private static bool _commandHarmonyInstalled;
        private static bool _updateHarmonyInstalled;
        private static bool _versionBadgeHarmonyInstalled;
        private static bool _gameConfigHarmonyInstalled;
        private static bool _clickRouterHarmonyInstalled;
        private static bool _systemChatHarmonyInstalled;
        private static bool _creatorColorHarmonyInstalled;
        private static bool _exileTextHarmonyInstalled;
        private const string CreatorColorHarmonyId = Guid + ".creatorcolor";
        private const string SystemChatHarmonyId = Guid + ".systemchat";
        private const string CommandHarmonyId = Guid + ".commands";
        private const string ClickRouterHarmonyId = Guid + ".clickrouter";
        private const string VersionBadgeHarmonyId = Guid + ".versionbadge";
        private const string GameConfigHarmonyId = Guid + ".gameconfig";
        private const string JesterHarmonyId = Guid + ".jester";
        private const string AssassinHarmonyId = Guid + ".assassin";

        public override void Load()
        {
            // CRASH DIAGNOSTIC: log any unhandled managed exception with its
            // stack BEFORE it crosses into native code and the process aborts
            // with PAL_SEHException (which discards the managed stack). If the
            // boot crash is a managed exception from any patch or callback, this
            // line shows exactly where it was thrown.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    Log.LogError("[TOU-FATAL] Unhandled exception: " + e.ExceptionObject);
                }
                catch
                {
                    // Never throw inside the handler.
                }
            };

            // Tell Manactor this mod is present (mod list only; the handshake /
            // mod-set verification system was removed from Manactor).
            ManactorAPI.Register("TownOfUs.ManuAPI", Version);

            // Per-role enable toggles (BepInEx/config/TownOfUs.ManuAPI.cfg).
            RoleConfig.Init(Config);
            CommandConfig.Init(Config);
            SystemChat.Init(Config);
            CreatorColor.Init(Config);
            UpdateConfig.Init(Config);

            // CRASH DIAGNOSTIC (Diagnostics.DisableAllPatches = true): load the
            // mod inert - registered with Manactor, config read, but no Harmony
            // patches, no game events, no RPC registration, no role descriptors.
            // If the game still crashes in this mode, the fault is in load-time
            // code or a ManuAPI/Manactor interaction, not in any patch.
            if (RoleConfig.DisableAllPatches?.Value == true)
            {
                Log.LogWarning("DisableAllPatches = true - running INERT (no patches/events/RPC). Use to bisect startup crashes.");
                Log.LogInfo("Town Of Us (ManuAPI port) loaded (inert diagnostic mode).");
                return;
            }

            // Manactor 1.2 reserves only 39 custom RPC ids (212-250). This mod
            // has ~68 RPC keys, so registering them all directly overflows the
            // allocator: keys past "Miner..." get no id, GetId() returns 0, and
            // ManactorRpc.Send(0, args) writes a Hazel message tagged callId 0 -
            // a vanilla RPC the native game parses with its own handler, which
            // segfaults the process on game start / join. The mux routes every
            // role key through one reserved transport id instead. Must run before
            // the first RegisterRpcMethods below so every role type is captured.
            // Manactor 1.4 serializes byte[] RPC arguments as a length-prefixed
            // payload, which is the transport contract used by TownOfUsRpcMux.
            // Install before any role system registers its handlers; otherwise
            // surplus role keys consume the finite native RPC-id range first.
            // Install the RPC multiplexer FIRST, before any role type registers
            // its [ManactorRpc] handlers below. Manactor only has 39 native custom
            // RPC ids (212-250) while this mod has ~68 keys, so the mux captures
            // every townofus.* key and routes it through one reserved transport id.
            // This is the pattern of the builds that ran clean (single
            // townofus.RpcMux id, no overflow, no call-id-0 sends). If the mux
            // cannot install, RpcRegistration skips role registrations entirely so
            // the finite native RPC range is never overflowed.
            try
            {
                TownOfUsRpcMux.Install();
                Log.LogInfo("TownOfUs RPC mux: installed (single transport for all role keys)");
            }
            catch (Exception ex)
            {
                Log.LogError("TownOfUs RPC mux failed to install: " + ex);
            }

            // Creator identity handshake: every client can receive and verify the
            // creator's claim (secret-matched), and the claim must not leak into
            // the next lobby after a game ends.
            RpcRegistration.Register(typeof(CreatorColor));
            GameEvents.GameStarted += CreatorColor.OnGameStarted;
            GameEvents.GameEnded += CreatorColor.OnGameEnded;
            if (UpdateConfig.Enabled?.Value == true)
            {
                InstallUpdateHarmony();
                UpdateSystem.StartCheck();
                Log.LogInfo("Self-update: checking for updates");
            }
            // Never inject custom rows into Classic Us' native Game Options menu.
            // On 8.9 that ManuAPI path can freeze the menu; role settings live in
            // the three BepInEx config sections instead.
            Log.LogInfo("Town Of Us role settings: BepInEx config sections (native menu injection disabled)");
            if (CommandConfig.Enabled.Value)
            {
                RpcRegistration.Register(typeof(CommandSystem));
                InstallCommandHarmony();
            }

            if (RoleConfig.Sheriff.Value)
            {
                RegisterSheriff();
                Log.LogInfo("Sheriff: enabled");
            }
            else
            {
                Log.LogInfo("Sheriff: disabled via config");
            }

            if (RoleConfig.Engineer.Value)
            {
                RegisterEngineer();
                Log.LogInfo("Engineer: enabled");
            }
            else
            {
                Log.LogInfo("Engineer: disabled via config");
            }

            if (RoleConfig.Jester.Value)
            {
                RegisterJester();
                InstallJesterHarmony();
                Log.LogInfo("Jester: enabled");
            }
            else
            {
                Log.LogInfo("Jester: disabled via config");
            }

            if (RoleConfig.Medic.Value || RoleConfig.Seer.Value || RoleConfig.Vigilante.Value)
                RegisterFirstBatchRoles();

            // HUD-clone ability buttons are gated on the [Diagnostics]
            // EnableCustomAbilityButtons toggle (default true). This exact system
            // was the source of native freezes/PAL crashes on some Linux builds,
            // so it must honor the config exactly like the earlier builds that
            // logged "disabled via config (Freeplay diagnostic mode)".
            if (RoleConfig.CustomAbilityButtons?.Value == false)
            {
                Log.LogInfo("Custom role ability buttons: disabled via config (HUD stability)");
            }
            else if (RoleConfig.Sheriff.Value || RoleConfig.Vigilante.Value ||
                RoleConfig.Engineer.Value || RoleConfig.Medic.Value || RoleConfig.Seer.Value ||
                RoleConfig.Janitor.Value || RoleConfig.Altruist.Value ||
                RoleConfig.Arsonist.Value || RoleConfig.Morphling.Value ||
                RoleConfig.Camouflager.Value || RoleConfig.Swooper.Value || RoleConfig.Undertaker.Value ||
                RoleConfig.TimeLord.Value || RoleConfig.Shifter.Value || RoleConfig.Glitch.Value ||
                RoleConfig.Miner.Value)
            {
                InstallCustomRoleAbilityHarmony();
                GameEvents.GameStarted += CustomRoleAbilities.OnGameStarted;
                GameEvents.GameEnded += CustomRoleAbilities.OnGameEnded;
                Log.LogInfo("Custom role ability buttons: native BottomRight layout enabled");
            }

            if (RoleConfig.Assassin.Value)
            {
                RegisterAssassin();
                InstallAssassinHarmony();
                Log.LogInfo("Assassin: enabled");
            }
            else
            {
                Log.LogInfo("Assassin: disabled via config");
            }

            if (RoleConfig.Janitor.Value)
            {
                RegisterJanitor();
                Log.LogInfo("Janitor: enabled");
            }
            else
            {
                Log.LogInfo("Janitor: disabled via config");
            }

            if (RoleConfig.Altruist.Value)
            {
                RegisterAltruist();
                Log.LogInfo("Altruist: enabled");
            }
            else
            {
                Log.LogInfo("Altruist: disabled via config");
            }

            if (RoleConfig.Mayor.Value)
            {
                RegisterMayor();
                InstallMayorHarmony();
                Log.LogInfo("Mayor: enabled");
            }
            else
            {
                Log.LogInfo("Mayor: disabled via config");
            }

            if (RoleConfig.Executioner.Value)
            {
                RegisterExecutioner();
                InstallExecutionerHarmony();
                Log.LogInfo("Executioner: enabled");
            }
            else
            {
                Log.LogInfo("Executioner: disabled via config");
            }

            if (RoleConfig.Arsonist.Value)
            {
                RegisterArsonist();
                InstallArsonistHarmony();
                Log.LogInfo("Arsonist: enabled");
            }
            else
            {
                Log.LogInfo("Arsonist: disabled via config");
            }

            if (RoleConfig.Swapper.Value)
            {
                RegisterSwapper();
                InstallSwapperHarmony();
                Log.LogInfo("Swapper: enabled");
            }
            else
            {
                Log.LogInfo("Swapper: disabled via config");
            }

            if (RoleConfig.Morphling.Value)
            {
                RegisterMorphling();
                InstallMorphlingHarmony();
                Log.LogInfo("Morphling: enabled");
            }
            else
            {
                Log.LogInfo("Morphling: disabled via config");
            }

            if (RoleConfig.Spy.Value)
            {
                RegisterSpy();
                InstallSpyHarmony();
                Log.LogInfo("Spy: enabled");
            }
            else
            {
                Log.LogInfo("Spy: disabled via config");
            }

            if (RoleConfig.Camouflager.Value || RoleConfig.Swooper.Value ||
                RoleConfig.Underdog.Value || RoleConfig.Undertaker.Value)
            {
                RegisterBatch3Impostors();
                InstallBatch3Harmony();
                Log.LogInfo("Batch-3 Impostor roles: enabled");
            }

            if (RoleConfig.Investigator.Value || RoleConfig.TimeLord.Value ||
                RoleConfig.Snitch.Value || RoleConfig.Phantom.Value)
            {
                RegisterBatch4Roles();
                InstallBatch4Harmony();
                Log.LogInfo("Batch-4 roles: enabled");
            }

            if (RoleConfig.Shifter.Value || RoleConfig.Glitch.Value || RoleConfig.Miner.Value)
            {
                RegisterBatch5Roles();
                InstallBatch5Harmony();
                Log.LogInfo("Batch-5 roles: enabled");
            }

            if (RoleConfig.Mayor.Value)
            {
                InstallMayorAbstainHarmony();
                Log.LogInfo("Mayor Abstain button: enabled");
            }

            // Player modifiers (Torch / Diseased / Flash / Tiebreaker / Drunk /
            // Giant / Button Barry). Registered regardless of which toggles are on
            // (inert when all are disabled).
            RegisterModifiers();

            if (RoleConfig.PresentationEnabled?.Value == true)
            {
                InstallPresentationHarmony();
                Log.LogInfo("Classic role presentation: enabled");
            }

            // Delegate-free click routing for all custom UI buttons. Classic Us
            // terminates the process when a managed delegate is first marshalled
            // into Il2Cpp (OnClick.AddListener), so every button built by this
            // mod is dispatched through the native PassiveButton.ReceiveClickDown
            // pipeline instead. Installed unconditionally; inert without registrations.
            InstallClickRouterHarmony();

            // Custom exile reveal text ("X was the Jester/Executioner/Arsonist/
            // Phantom/The Glitch") re-applied every frame while the exile animates.
            // Replaces the removed compiler-generated coroutine patches. Installed
            // unconditionally; inert (one null check) outside an exile.
            InstallExileTextHarmony();

            // System messages now render through the game's native "SYSTEM ALERT"
            // popup (HudManager.ChatPopup.ShowWarning) via SystemChat.Show, not the
            // chat bubble. The legacy mascot bubble patch is a no-op and is kept only
            // so existing configs/installs don't error on the removed chat restyle.
            InstallSystemChatHarmony();

            // Creator-only name color: the mod creator's name cycles blue/pink
            // (name-matched, config-gated).
            InstallCreatorColorHarmony();

            // "TownOfUs vX.Y.Z" under the game's version readout (top-left), plus
            // the in-game credit ("Made by hallowmarsh") above the ping/fps labels.
            InstallVersionBadgeHarmony();
            Log.LogInfo("Version badge + credit: enabled");

            // Tasks-tab role info card ("Your Role: X — description"). Non-fatal:
            // a missing importantTextTask field on some build just disables it.
            InstallRoleInfoCardHarmony();

            // In-game role settings: sync registry + the tabbed config overlay.
            RoleSettingsSync.Init();
            // Registered regardless of the overlay toggle: clients must mirror the
            // host's role settings even when the tabs are disabled.
            RpcRegistration.Register(typeof(RoleSettingsSync));
            if (RoleConfig.GameConfigOverlay?.Value == true)
            {
                InstallGameConfigHarmony();
                GameEvents.GameStarted += RoleSettingsSync.OnGameStarted;
                GameEvents.PlayerJoined += RoleSettingsSync.OnPlayerJoined;
                Log.LogInfo("Game config tabs: enabled");
            }
            else
            {
                Log.LogInfo("Game config tabs: disabled via config");
            }

            // ModsMenuAPI (the in-game bottom-right "Mods" menu) is not present in
            // ManuAPI 1.5.1, so this mod's own mod/role toggles are not registered
            // here. Role enablement is driven solely by the BepInEx config toggles.

            // Keep role registration independent from the experimental HUD/meeting
            // hooks. The Freeplay computer is provided by ManuAPI and only needs the
            // virtual role registration above; skipping these patches avoids touching
            // IL2CPP lifecycle methods while diagnosing native Linux crashes.
            if (RoleConfig.GameplayHooks.Value)
            {
                // Event-level Sheriff hooks (report suppression and lifecycle
                // bookkeeping) remain behind this diagnostic switch.
                Log.LogInfo("Sheriff gameplay hooks: enabled");
            }
            else
            {
                Log.LogInfo("Sheriff gameplay hooks: disabled (Freeplay selector diagnostic mode)");
            }

            // Startup crash diagnostics: marker lines as the boot scene runs.
            // Always armed so the next crash log shows exactly how far boot got.
            InstallBootTraceHarmony();
            BootTrace.Mark("M1 Load complete - boot trace armed");

            Log.LogInfo("Town Of Us (ManuAPI port) loaded.");
        }

        private void InstallBootTraceHarmony()
        {
            // Non-fatal: each marker installs independently and is inert if a
            // target does not exist on some build.
            var harmony = new Harmony(Guid + ".boottrace");
            foreach (var patchType in new[]
            {
                typeof(BootTrace.M2_AmongUsClient_Awake),
                typeof(BootTrace.M3_MainMenuManager_Start),
                typeof(BootTrace.M4_VersionShower_Start),
                typeof(BootTrace.M5_GameStartManager_Start),
                typeof(BootTrace.M6_HudManager_Start),
                typeof(BootTrace.M7_MeetingHud_Start),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                }
                catch (Exception e)
                {
                    Log.LogWarning("Boot trace marker skipped (" + patchType.Name + "): " + e.Message);
                }
            }
        }

        /// <summary>
        /// Registers everything the Sheriff needs. Guarded by RoleConfig.Sheriff so a
        /// disabled role never enters the role pool. Future roles copy this method
        /// (add a toggle in RoleConfig first).
        /// </summary>
        private static void RegisterSheriff()
        {
            // Every client can receive the Sheriff's kill-record RPC.
            RpcRegistration.Register(typeof(SheriffSystem));

            // Virtual roles ride on the vanilla Crewmate/Impostor backing roles — no
            // IL2CPP type injection required.
            // NOTE: the game ships its own global-namespace SheriffRole, so a simple
            // name would resolve to the game's class; fully-qualify our descriptor.
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Sheriff.SheriffRole());

            // The Sheriff cannot report bodies they shot themselves, and its kill tables
            // are cleared whenever a round starts or ends (HudManager.Start only fires
            // once, so game events close the no-meeting lifecycle gap). Keep these hooks
            // behind the same diagnostic gate as the Harmony patches.
            if (RoleConfig.GameplayHooks.Value)
            {
                GameEvents.BeforeReport += SheriffSystem.OnBeforeReport;
                GameEvents.GameStarted += SheriffSystem.OnGameStarted;
                GameEvents.GameEnded += SheriffSystem.OnGameEnded;
                _eventHooksInstalled = true;
            }
        }

        private static void RegisterJester()
        {
            RpcRegistration.Register(typeof(JesterSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Jester.JesterRole());
            GameEvents.PlayerExiled += JesterSystem.OnPlayerExiled;
            GameEvents.GameStarted += JesterSystem.OnGameStarted;
            GameEvents.GameEnded += JesterSystem.OnGameEnded;
            _jesterEventHooksInstalled = true;
        }

        private void InstallJesterHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Jester keeps its role, just loses the win screen).
            var harmony = new Harmony(JesterHarmonyId);
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(EndGameManager_SetEverythingUp_JesterPatch),
                typeof(RoleManager_AssignRolesForTeam_JesterPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Jester patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            _jesterHarmonyInstalled = ok > 0;
            if (ok > 0) Log.LogInfo("Jester win-screen hook: enabled");
            if (ok == 0) harmony.UnpatchSelf();
        }

        private void InstallPresentationHarmony()
        {
            // Non-fatal install: a missing HudManager.Update or MeetingHud.Update
            // target on some build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".presentation");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(HudManager_Update_PresentationPatch),
                typeof(MeetingHud_Update_PresentationPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Role presentation patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            _presentationHarmonyInstalled = ok > 0;
            if (ok == 0) harmony.UnpatchSelf();
        }

        private static void RegisterAssassin()
        {
            RpcRegistration.Register(typeof(AssassinSystem));
            RpcRegistration.Register(typeof(AssassinSettingsSync));
            AssassinSettingsSync.InitFromConfig();
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Assassin.AssassinRole());
            GameEvents.GameStarted += AssassinSystem.OnGameStarted;
            GameEvents.GameEnded += AssassinSystem.OnGameEnded;
            GameEvents.GameStarted += AssassinSettingsSync.OnGameStarted;
            GameEvents.GameEnded += AssassinSettingsSync.OnGameEnded;
            GameEvents.PlayerJoined += AssassinSettingsSync.OnPlayerJoined;
            GameEvents.AtMeeting += AssassinSystem.OnMeetingStarted;
            GameEvents.AfterMeeting += AssassinSystem.OnMeetingEnded;
            _assassinEventHooksInstalled = true;
        }

        private void InstallAssassinHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Assassin keeps its role, just loses the meeting UI).
            var harmony = new Harmony(AssassinHarmonyId);
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(MeetingHud_Start_AssassinPatch),
                typeof(MeetingHud_Confirm_AssassinPatch),
                typeof(MeetingHud_VotingComplete_AssassinPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Assassin patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            _assassinHarmonyInstalled = ok > 0;
            if (ok == 0) harmony.UnpatchSelf();
        }

        private static void RegisterJanitor()
        {
            // Every client can receive the Janitor's clean RPC.
            RpcRegistration.Register(typeof(JanitorSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Janitor.JanitorRole());
            GameEvents.GameStarted += JanitorSystem.OnGameStarted;
            GameEvents.GameEnded += JanitorSystem.OnGameEnded;
        }

        private static void RegisterAltruist()
        {
            RpcRegistration.Register(typeof(AltruistSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Altruist.AltruistRole());
            GameEvents.GameStarted += AltruistSystem.OnGameStarted;
            GameEvents.GameEnded += AltruistSystem.OnGameEnded;
        }

        private static void RegisterMayor()
        {
            // Mayor is passive: the vote-bank tally patch is the only hook.
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Mayor.MayorRole());
        }

        private void InstallMayorHarmony()
        {
            // Non-fatal install: "CalculateVotes" is private and subject to
            // interop drift, so a missing target must never prevent the plugin
            // from loading (Mayor just becomes vote-less if it fails).
            var harmony = new Harmony(Guid + ".mayor");
            try
            {
                harmony.CreateClassProcessor(typeof(MeetingHud_CalculateVotes_MayorPatch)).Patch();
                Log.LogInfo("Mayor vote-bank hook: enabled");
            }
            catch (Exception e)
            {
                Log.LogWarning("Mayor vote-bank hook skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private static void RegisterExecutioner()
        {
            // Every client can receive the Executioner's assignment/win RPCs.
            RpcRegistration.Register(typeof(ExecutionerSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Executioner.ExecutionerRole());
            GameEvents.PlayerExiled += ExecutionerSystem.OnPlayerExiled;
            // Conversion rule: the Executioner becomes Jester/Crewmate when
            // their target dies without being voted out.
            GameEvents.BeforeMurder += ExecutionerSystem.OnBeforeMurder;
            GameEvents.GameStarted += ExecutionerSystem.OnGameStarted;
            GameEvents.GameEnded += ExecutionerSystem.OnGameEnded;
        }

        private void InstallExecutionerHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Executioner keeps its role, just loses the extras).
            var harmony = new Harmony(Guid + ".executioner");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(ExileController_Begin_ExecutionerPatch),
                typeof(PlayerControl_FixedUpdate_ExecutionerPatch),
                typeof(RoleManager_AssignRolesForTeam_ExecutionerPatch),
                typeof(EndGameManager_Update_ExecutionerPatch),
                typeof(EndGameManager_SetEverythingUp_ExecutionerPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Executioner patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            if (ok == 0) harmony.UnpatchSelf();
        }

        private static void RegisterArsonist()
        {
            // Every client can receive the Arsonist's douse/ignite/win RPCs.
            RpcRegistration.Register(typeof(ArsonistSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Arsonist.ArsonistRole());
            GameEvents.GameStarted += ArsonistSystem.OnGameStarted;
            GameEvents.GameEnded += ArsonistSystem.OnGameEnded;
        }

        private void InstallArsonistHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Arsonist keeps its role, just loses the extras).
            var harmony = new Harmony(Guid + ".arsonist");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(ExileController_Begin_ArsonistPatch),
                typeof(PlayerControl_FixedUpdate_ArsonistPatch),
                typeof(RoleManager_AssignRolesForTeam_ArsonistPatch),
                typeof(EndGameManager_Update_ArsonistPatch),
                typeof(EndGameManager_SetEverythingUp_ArsonistPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Arsonist patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            if (ok == 0) harmony.UnpatchSelf();
        }

        private static void RegisterSwapper()
        {
            RpcRegistration.Register(typeof(SwapperSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Swapper.SwapperRole());
            GameEvents.GameStarted += SwapperSystem.OnGameStarted;
            GameEvents.GameEnded += SwapperSystem.OnGameEnded;
            GameEvents.AtMeeting += SwapperSystem.OnMeetingStarted;
            GameEvents.AfterMeeting += SwapperSystem.OnMeetingEnded;
        }

        private void InstallSwapperHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Swapper keeps its meeting UI, just loses the tally
            // swap, if CalculateVotes ever drifts again).
            var harmony = new Harmony(Guid + ".swapper");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(MeetingHud_Start_SwapperPatch),
                typeof(MeetingHud_Confirm_SwapperPatch),
                typeof(MeetingHud_VotingComplete_SwapperPatch),
                typeof(SwapperSystem.MeetingHud_CalculateVotes_SwapperPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Swapper patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            if (ok == 0) harmony.UnpatchSelf();
        }

        private static void RegisterMorphling()
        {
            RpcRegistration.Register(typeof(MorphlingSystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Morphling.MorphlingRole());
            GameEvents.GameStarted += MorphlingSystem.OnGameStarted;
            GameEvents.GameEnded += MorphlingSystem.OnGameEnded;
            GameEvents.AtMeeting += MorphlingSystem.OnMeetingStarted;
        }

        private void InstallMorphlingHarmony()
        {
            // Non-fatal install: a missing PlayerControl.FixedUpdate target on
            // some build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".morphling");
            try
            {
                harmony.CreateClassProcessor(typeof(PlayerControl_FixedUpdate_MorphlingPatch)).Patch();
            }
            catch (Exception e)
            {
                Log.LogWarning("Morphling patch skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private static void RegisterSpy()
        {
            RpcRegistration.Register(typeof(SpySystem));
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Spy.SpyRole());
            GameEvents.GameStarted += SpySystem.OnGameStarted;
            GameEvents.GameEnded += SpySystem.OnGameEnded;
        }

        private static void RegisterBatch3Impostors()
        {
            if (RoleConfig.Camouflager.Value)
            {
                RpcRegistration.Register(typeof(CamouflagerSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Camouflager.CamouflagerRole());
                GameEvents.GameStarted += CamouflagerSystem.OnGameStarted;
                GameEvents.GameEnded += CamouflagerSystem.OnGameEnded;
                GameEvents.AtMeeting += CamouflagerSystem.OnMeetingStarted;
            }
            if (RoleConfig.Swooper.Value)
            {
                RpcRegistration.Register(typeof(SwooperSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Swooper.SwooperRole());
                GameEvents.GameStarted += SwooperSystem.OnGameStarted;
                GameEvents.GameEnded += SwooperSystem.OnGameEnded;
                GameEvents.AtMeeting += SwooperSystem.OnMeetingStarted;
            }
            if (RoleConfig.Underdog.Value)
            {
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Underdog.UnderdogRole());
                GameEvents.GameStarted += UnderdogSystem.OnGameStarted;
                GameEvents.GameEnded += UnderdogSystem.OnGameEnded;
            }
            if (RoleConfig.Undertaker.Value)
            {
                RpcRegistration.Register(typeof(UndertakerSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Undertaker.UndertakerRole());
                GameEvents.GameStarted += UndertakerSystem.OnGameStarted;
                GameEvents.GameEnded += UndertakerSystem.OnGameEnded;
                GameEvents.AtMeeting += UndertakerSystem.OnMeetingStarted;
            }
        }

        private void InstallBatch3Harmony()
        {
            // Non-fatal install: a missing HudManager.FixedUpdate target on
            // some build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".batch3");
            try
            {
                harmony.CreateClassProcessor(typeof(HudManager_FixedUpdate_Batch3Patch)).Patch();
            }
            catch (Exception e)
            {
                Log.LogWarning("Batch-3 patch skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private static void RegisterBatch4Roles()
        {
            if (RoleConfig.Investigator.Value)
            {
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Investigator.InvestigatorRole());
                GameEvents.GameStarted += InvestigatorSystem.OnGameStarted;
                GameEvents.GameEnded += InvestigatorSystem.OnGameEnded;
                GameEvents.AtMeeting += InvestigatorSystem.OnMeetingStarted;
            }
            if (RoleConfig.TimeLord.Value)
            {
                RpcRegistration.Register(typeof(TimeLordSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.TimeLord.TimeLordRole());
                GameEvents.GameStarted += TimeLordSystem.OnGameStarted;
                GameEvents.GameEnded += TimeLordSystem.OnGameEnded;
            }
            if (RoleConfig.Snitch.Value)
            {
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Snitch.SnitchRole());
                GameEvents.GameStarted += SnitchSystem.OnGameStarted;
                GameEvents.GameEnded += SnitchSystem.OnGameEnded;
                GameEvents.AtMeeting += SnitchSystem.OnMeetingStarted;
            }
            if (RoleConfig.Phantom.Value)
            {
                RpcRegistration.Register(typeof(PhantomSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Phantom.PhantomRole());
                GameEvents.GameStarted += PhantomSystem.OnGameStarted;
                GameEvents.GameEnded += PhantomSystem.OnGameEnded;
            }
        }

        private void InstallBatch4Harmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Batch-4 roles keep their role, just lose the extras).
            var harmony = new Harmony(Guid + ".batch4");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(HudManager_FixedUpdate_Batch4Patch),
                typeof(EndGameManager_Update_PhantomPatch),
                typeof(EndGameManager_SetEverythingUp_PhantomPatch),
                typeof(ExileController_Begin_PhantomPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Batch-4 patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            if (ok == 0) harmony.UnpatchSelf();
        }

        private void InstallSpyHarmony()
        {
            // Non-fatal install: a missing PlayerControl.FixedUpdate target on
            // some build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".spy");
            try
            {
                harmony.CreateClassProcessor(typeof(PlayerControl_FixedUpdate_SpyPatch)).Patch();
            }
            catch (Exception e)
            {
                Log.LogWarning("Spy patch skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private static void RegisterFirstBatchRoles()
        {
            if (RoleConfig.Medic.Value)
            {
                RpcRegistration.Register(typeof(MedicSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Medic.MedicRole());
            }
            if (RoleConfig.Seer.Value)
            {
                RpcRegistration.Register(typeof(SeerSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Seer.SeerRole());
            }
            if (RoleConfig.Vigilante.Value)
            {
                RpcRegistration.Register(typeof(VigilanteSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Vigilante.VigilanteRole());
            }

            if (RoleConfig.Medic.Value)
            {
                GameEvents.BeforeMurder += MedicSystem.OnBeforeMurder;
                GameEvents.GameStarted += MedicSystem.OnGameStarted;
                GameEvents.GameEnded += MedicSystem.OnGameEnded;
                _medicEventHooksInstalled = true;
            }
            if (RoleConfig.Seer.Value)
            {
                GameEvents.GameStarted += SeerSystem.OnGameStarted;
                GameEvents.GameEnded += SeerSystem.OnGameEnded;
                _seerEventHooksInstalled = true;
            }
            if (RoleConfig.Vigilante.Value)
            {
                GameEvents.GameStarted += VigilanteSystem.OnGameStarted;
                GameEvents.GameEnded += VigilanteSystem.OnGameEnded;
                _vigilanteEventHooksInstalled = true;
            }
            _batchEventHooksInstalled = true;
        }

        private void InstallCustomRoleAbilityHarmony()
        {
            // Non-fatal install: a missing HudManager.FixedUpdate target on
            // some build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".abilities");
            try
            {
                harmony.CreateClassProcessor(typeof(HudManager_FixedUpdate_CustomRoleAbilitiesPatch)).Patch();
                _batchHarmonyInstalled = true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Custom role ability buttons skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private static void RegisterEngineer()
        {
            // Engineer uses only ManuAPI's virtual-role registration. CanVent is
            // applied to the native CrewmateRole, so the game's existing vent
            // controls, animations, and networking remain authoritative.
            RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Engineer.EngineerRole());

            // Engineer lifecycle reset is independent from the optional Sheriff hooks.
            GameEvents.GameStarted += EngineerAbility.OnGameStarted;
            GameEvents.GameEnded += EngineerAbility.OnGameEnded;
            _engineerEventHooksInstalled = true;
        }

        private void InstallCreatorColorHarmony()
        {
            // Non-fatal install: a missing HudManager.Update target on some build
            // can never prevent the plugin from loading.
            var harmony = new Harmony(CreatorColorHarmonyId);
            try
            {
                harmony.CreateClassProcessor(typeof(CreatorColor.HudManager_Update_CreatorColorPatch)).Patch();
                _creatorColorHarmonyInstalled = true;
                Log.LogInfo("Creator color: enabled");
            }
            catch (Exception e)
            {
                Log.LogWarning("Creator color skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private void InstallSystemChatHarmony()
        {
            // Non-fatal install: a missing AddChatWarning target on some build can
            // never prevent the plugin from loading.
            var harmony = new Harmony(SystemChatHarmonyId);
            try
            {
                harmony.CreateClassProcessor(typeof(SystemChat.ChatController_AddChatWarning_StylePatch)).Patch();
                _systemChatHarmonyInstalled = true;
                Log.LogInfo("System alerts: native popup enabled");
            }
            catch (Exception e)
            {
                Log.LogWarning("System chat mascot skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private void InstallCommandHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (commands keep working, just lose a hook).
            var harmony = new Harmony(CommandHarmonyId);
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(PlayerControl_RpcSendChat_CommandPatch),
                typeof(PlayerControl_FixedUpdate_VisualEffectsPatch),
                typeof(ShipStatus_CheckEndCriteria_CommandPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Command patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            _commandHarmonyInstalled = ok > 0;
            if (ok > 0) Log.LogInfo("In-game slash commands: enabled");
            if (ok == 0) harmony.UnpatchSelf();
        }

        private static void RegisterBatch5Roles()
        {
            if (RoleConfig.Shifter.Value)
            {
                RpcRegistration.Register(typeof(ShifterSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Shifter.ShifterRole());
                GameEvents.GameStarted += ShifterSystem.OnGameStarted;
                GameEvents.GameEnded += ShifterSystem.OnGameEnded;
            }
            if (RoleConfig.Glitch.Value)
            {
                RpcRegistration.Register(typeof(GlitchSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Glitch.GlitchRole());
                GameEvents.GameStarted += GlitchSystem.OnGameStarted;
                GameEvents.GameEnded += GlitchSystem.OnGameEnded;
                GameEvents.BeforeReport += GlitchSystem.OnBeforeReport;
            }
            if (RoleConfig.Miner.Value)
            {
                RpcRegistration.Register(typeof(MinerSystem));
                RoleRegistry.RegisterVirtual(new TownOfUs.ManuAPI.Roles.Miner.MinerRole());
                GameEvents.GameStarted += MinerSystem.OnGameStarted;
                GameEvents.GameEnded += MinerSystem.OnGameEnded;
            }
        }

        private void InstallBatch5Harmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (Batch-5 roles keep their role, just lose the extras).
            var harmony = new Harmony(Guid + ".batch5");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(HudManager_FixedUpdate_Batch5Patch),
                typeof(EndGameManager_Update_GlitchPatch),
                typeof(EndGameManager_SetEverythingUp_GlitchPatch),
                typeof(ExileController_Begin_GlitchPatch),
                typeof(RoleManager_AssignRolesForTeam_GlitchPatch),
                typeof(RoleManager_AssignRolesForTeam_ShifterPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Batch-5 patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            if (ok == 0) harmony.UnpatchSelf();
        }

        private void InstallMayorAbstainHarmony()
        {
            // Non-fatal install: "MeetingHud.Start" is subject to interop drift.
            var harmony = new Harmony(Guid + ".mayorabstain");
            try
            {
                harmony.CreateClassProcessor(typeof(MeetingHud_Start_MayorAbstainPatch)).Patch();
                harmony.CreateClassProcessor(typeof(MeetingHud_Confirm_MayorAbstainPatch)).Patch();
                harmony.CreateClassProcessor(typeof(MeetingHud_VotingComplete_MayorAbstainPatch)).Patch();
                Log.LogInfo("Mayor Abstain hook: installed");
            }
            catch (System.Exception e)
            {
                Log.LogWarning("Mayor Abstain hook skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private void InstallRoleInfoCardHarmony()
        {
            var harmony = new Harmony(Guid + ".roleinfocard");
            try
            {
                harmony.CreateClassProcessor(typeof(HudManager_Update_RoleInfoCardPatch)).Patch();
            }
            catch (Exception e)
            {
                Log.LogWarning("Role info card skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private void InstallGameConfigHarmony()
        {
            // Non-fatal install: each patch is applied independently so a target
            // that does not exist on the current game build (e.g. SettingMenu.Start
            // on 8.9) can never prevent the mod from loading.
            var harmony = new Harmony(GameConfigHarmonyId);
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(SettingMenu_OnEnable_ConfigOverlayPatch),
                typeof(GameSettingMenu_SetupFromData_ConfigOverlayPatch),
                typeof(GameOptionsMenu_OnEnable_ConfigOverlayPatch),
                typeof(HudManager_Update_ConfigOverlayPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Game config patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }

            _gameConfigHarmonyInstalled = ok > 0;
            if (ok > 0) Log.LogInfo("Game config tabs: " + ok + " patch(es) installed");
            else harmony.UnpatchSelf();
        }

        private void InstallVersionBadgeHarmony()
        {
            // Non-fatal install: each patch applies independently so a target
            // that does not exist on some build can never prevent the plugin
            // from loading (the badge and credit degrade independently).
            var harmony = new Harmony(VersionBadgeHarmonyId);
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(VersionShower_Start_VersionBadgePatch),
                typeof(PingTracker_Update_CreditPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Version badge patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            _versionBadgeHarmonyInstalled = ok > 0;
            if (ok == 0) harmony.UnpatchSelf();
        }

        private void RegisterModifiers()
        {
            RpcRegistration.Register(typeof(ModifierSystem));
            GameEvents.GameStarted += ModifierSystem.OnGameStarted;
            GameEvents.GameEnded += ModifierSystem.OnGameEnded;
            GameEvents.BeforeMurder += ModifierSystem.OnBeforeMurder;
            InstallModifierHarmony();
            Log.LogInfo("Player modifiers: enabled");
        }

        private void InstallModifierHarmony()
        {
            // Non-fatal install: each patch applies independently so a target that
            // does not exist on some build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".modifiers");
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(PlayerControl_FixedUpdate_ModifierPatch),
                typeof(PlayerPhysics_FixedUpdate_DrunkPatch),
                typeof(MeetingHud_CalculateVotes_TiebreakerPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("Modifier patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }
            if (ok == 0) harmony.UnpatchSelf();
        }

        private void InstallClickRouterHarmony()
        {
            // Delegate-free UI harmony: the ReceiveClickDown click router plus
            // the native settings rows / dedicated ToU Roles tab (all inert
            // without registrations). Each patch installs independently so a
            // missing target on some game build can never disable the others.
            var harmony = new Harmony(ClickRouterHarmonyId);
            int ok = 0;
            foreach (var patchType in new[]
            {
                typeof(PassiveButton_ReceiveClickDown_ClickRouterPatch),
                typeof(SettingMenu_OnEnable_MenuArrowPatch),
                typeof(CustomPlayerMenu_Start_MenuArrowPatch),
                typeof(GameOptionsMenu_Update_SettingsScrollPatch),
            })
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    Log.LogWarning("ClickRouter patch skipped (" + patchType.Name + "): " + e.Message);
                }
            }

            _clickRouterHarmonyInstalled = ok > 0;
            if (ok == 0) harmony.UnpatchSelf();
            if (RoleConfig.NativeMenuRows?.Value == true)
                Log.LogInfo(ok > 1 ? "Native menu arrow: enabled" : "Native menu arrow: patch not installed (" + ok + " ClickRouter patch)");
        }

        private void InstallExileTextHarmony()
        {
            // Non-fatal install: a missing HudManager.Update target on some build
            // can never prevent the plugin from loading (roles keep their win
            // screens, the exile reveal text just falls back to vanilla).
            var harmony = new Harmony(Guid + ".exiletext");
            try
            {
                harmony.CreateClassProcessor(typeof(HudManager_Update_ExileTextFixPatch)).Patch();
                _exileTextHarmonyInstalled = true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Exile text fix skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        private void InstallUpdateHarmony()
        {
            // Non-fatal install: a missing HudManager.Update target on some
            // build can never prevent the plugin from loading.
            var harmony = new Harmony(Guid + ".updates");
            try
            {
                harmony.CreateClassProcessor(typeof(HudManager_Update_UpdateModalPatch)).Patch();
                _updateHarmonyInstalled = true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Update modal skipped: " + e.Message);
                harmony.UnpatchSelf();
            }
        }

        public override bool Unload()
        {
            if (_eventHooksInstalled)
            {
                GameEvents.BeforeReport -= SheriffSystem.OnBeforeReport;
                GameEvents.GameStarted -= SheriffSystem.OnGameStarted;
                GameEvents.GameEnded -= SheriffSystem.OnGameEnded;
                _eventHooksInstalled = false;
            }

            if (_engineerEventHooksInstalled)
            {
                GameEvents.GameStarted -= EngineerAbility.OnGameStarted;
                GameEvents.GameEnded -= EngineerAbility.OnGameEnded;
                _engineerEventHooksInstalled = false;
            }

            if (_jesterEventHooksInstalled)
            {
                GameEvents.PlayerExiled -= JesterSystem.OnPlayerExiled;
                GameEvents.GameStarted -= JesterSystem.OnGameStarted;
                GameEvents.GameEnded -= JesterSystem.OnGameEnded;
                _jesterEventHooksInstalled = false;
            }

            if (_assassinEventHooksInstalled)
            {
                GameEvents.GameStarted -= AssassinSystem.OnGameStarted;
                GameEvents.GameEnded -= AssassinSystem.OnGameEnded;
                GameEvents.GameStarted -= AssassinSettingsSync.OnGameStarted;
                GameEvents.GameEnded -= AssassinSettingsSync.OnGameEnded;
                GameEvents.PlayerJoined -= AssassinSettingsSync.OnPlayerJoined;
                GameEvents.AtMeeting -= AssassinSystem.OnMeetingStarted;
                GameEvents.AfterMeeting -= AssassinSystem.OnMeetingEnded;
                _assassinEventHooksInstalled = false;
            }

            if (_assassinHarmonyInstalled)
            {
                new Harmony(AssassinHarmonyId).UnpatchSelf();
                AssassinSystem.Reset();
                _assassinHarmonyInstalled = false;
            }

            if (_presentationHarmonyInstalled)
            {
                new Harmony(Guid + ".presentation").UnpatchSelf();
                PresentationPatches.Reset();
                _presentationHarmonyInstalled = false;
            }

            if (_jesterHarmonyInstalled)
            {
                new Harmony(JesterHarmonyId).UnpatchSelf();
                JesterSystem.Reset();
                _jesterHarmonyInstalled = false;
            }

            if (_batchEventHooksInstalled)
            {
                if (_medicEventHooksInstalled)
                {
                    GameEvents.BeforeMurder -= MedicSystem.OnBeforeMurder;
                    GameEvents.GameStarted -= MedicSystem.OnGameStarted;
                    GameEvents.GameEnded -= MedicSystem.OnGameEnded;
                    _medicEventHooksInstalled = false;
                }
                if (_seerEventHooksInstalled)
                {
                    GameEvents.GameStarted -= SeerSystem.OnGameStarted;
                    GameEvents.GameEnded -= SeerSystem.OnGameEnded;
                    _seerEventHooksInstalled = false;
                }
                if (_vigilanteEventHooksInstalled)
                {
                    GameEvents.GameStarted -= VigilanteSystem.OnGameStarted;
                    GameEvents.GameEnded -= VigilanteSystem.OnGameEnded;
                    _vigilanteEventHooksInstalled = false;
                }
                _batchEventHooksInstalled = false;
            }

            if (_batchHarmonyInstalled)
            {
                GameEvents.GameStarted -= CustomRoleAbilities.OnGameStarted;
                GameEvents.GameEnded -= CustomRoleAbilities.OnGameEnded;
                CustomRoleAbilities.ResetAll();
                new Harmony(Guid + ".abilities").UnpatchSelf();
                MedicSystem.Reset();
                SeerSystem.Reset();
                VigilanteSystem.Reset();
                _batchHarmonyInstalled = false;
            }

            if (_commandHarmonyInstalled)
            {
                new Harmony(CommandHarmonyId).UnpatchSelf();
                _commandHarmonyInstalled = false;
                VisualEffects.Reset();
                CommandState.Reset();
            }

            if (_updateHarmonyInstalled)
            {
                new Harmony(Guid + ".updates").UnpatchSelf();
                _updateHarmonyInstalled = false;
            }

            if (_versionBadgeHarmonyInstalled)
            {
                new Harmony(VersionBadgeHarmonyId).UnpatchSelf();
                _versionBadgeHarmonyInstalled = false;
            }

            if (_gameConfigHarmonyInstalled)
            {
                GameEvents.GameStarted -= RoleSettingsSync.OnGameStarted;
                GameEvents.PlayerJoined -= RoleSettingsSync.OnPlayerJoined;
                GameConfigOverlay.Hide();
                new Harmony(GameConfigHarmonyId).UnpatchSelf();
                _gameConfigHarmonyInstalled = false;
            }

            RoleInfoCard.Reset();
            new Harmony(Guid + ".roleinfocard").UnpatchSelf();

            if (_clickRouterHarmonyInstalled)
            {
                ClickRouter.Reset();
                new Harmony(ClickRouterHarmonyId).UnpatchSelf();
                _clickRouterHarmonyInstalled = false;
            }

            if (_systemChatHarmonyInstalled)
            {
                new Harmony(SystemChatHarmonyId).UnpatchSelf();
                SystemChat.Reset();
                _systemChatHarmonyInstalled = false;
            }

            if (_creatorColorHarmonyInstalled)
            {
                new Harmony(CreatorColorHarmonyId).UnpatchSelf();
                CreatorColor.Reset();
                _creatorColorHarmonyInstalled = false;
            }

            if (_exileTextHarmonyInstalled)
            {
                new Harmony(Guid + ".exiletext").UnpatchSelf();
                _exileTextHarmonyInstalled = false;
            }

            new Harmony(Guid + ".boottrace").UnpatchSelf();

            // New-role lifecycle cleanup (delegate removal of unsubscribed
            // handlers and UnpatchSelf on an unpatched id are both no-ops).
            GameEvents.GameStarted -= JanitorSystem.OnGameStarted;
            GameEvents.GameEnded -= JanitorSystem.OnGameEnded;
            GameEvents.GameStarted -= AltruistSystem.OnGameStarted;
            GameEvents.GameEnded -= AltruistSystem.OnGameEnded;
            GameEvents.PlayerExiled -= ExecutionerSystem.OnPlayerExiled;
            GameEvents.BeforeMurder -= ExecutionerSystem.OnBeforeMurder;
            GameEvents.GameStarted -= ExecutionerSystem.OnGameStarted;
            GameEvents.GameEnded -= ExecutionerSystem.OnGameEnded;
            GameEvents.GameStarted -= ArsonistSystem.OnGameStarted;
            GameEvents.GameEnded -= ArsonistSystem.OnGameEnded;
            GameEvents.GameStarted -= SwapperSystem.OnGameStarted;
            GameEvents.GameEnded -= SwapperSystem.OnGameEnded;
            GameEvents.AtMeeting -= SwapperSystem.OnMeetingStarted;
            GameEvents.AfterMeeting -= SwapperSystem.OnMeetingEnded;
            GameEvents.GameStarted -= MorphlingSystem.OnGameStarted;
            GameEvents.GameEnded -= MorphlingSystem.OnGameEnded;
            GameEvents.AtMeeting -= MorphlingSystem.OnMeetingStarted;
            GameEvents.GameStarted -= SpySystem.OnGameStarted;
            GameEvents.GameEnded -= SpySystem.OnGameEnded;
            GameEvents.GameStarted -= CamouflagerSystem.OnGameStarted;
            GameEvents.GameEnded -= CamouflagerSystem.OnGameEnded;
            GameEvents.AtMeeting -= CamouflagerSystem.OnMeetingStarted;
            GameEvents.GameStarted -= SwooperSystem.OnGameStarted;
            GameEvents.GameEnded -= SwooperSystem.OnGameEnded;
            GameEvents.AtMeeting -= SwooperSystem.OnMeetingStarted;
            GameEvents.GameStarted -= UnderdogSystem.OnGameStarted;
            GameEvents.GameEnded -= UnderdogSystem.OnGameEnded;
            GameEvents.GameStarted -= UndertakerSystem.OnGameStarted;
            GameEvents.GameEnded -= UndertakerSystem.OnGameEnded;
            GameEvents.AtMeeting -= UndertakerSystem.OnMeetingStarted;
            GameEvents.GameStarted -= InvestigatorSystem.OnGameStarted;
            GameEvents.GameEnded -= InvestigatorSystem.OnGameEnded;
            GameEvents.AtMeeting -= InvestigatorSystem.OnMeetingStarted;
            GameEvents.GameStarted -= TimeLordSystem.OnGameStarted;
            GameEvents.GameEnded -= TimeLordSystem.OnGameEnded;
            GameEvents.GameStarted -= SnitchSystem.OnGameStarted;
            GameEvents.GameEnded -= SnitchSystem.OnGameEnded;
            GameEvents.AtMeeting -= SnitchSystem.OnMeetingStarted;
            GameEvents.GameStarted -= PhantomSystem.OnGameStarted;
            GameEvents.GameEnded -= PhantomSystem.OnGameEnded;
            GameEvents.GameStarted -= ShifterSystem.OnGameStarted;
            GameEvents.GameEnded -= ShifterSystem.OnGameEnded;
            GameEvents.GameStarted -= GlitchSystem.OnGameStarted;
            GameEvents.GameEnded -= GlitchSystem.OnGameEnded;
            GameEvents.BeforeReport -= GlitchSystem.OnBeforeReport;
            GameEvents.GameStarted -= MinerSystem.OnGameStarted;
            GameEvents.GameEnded -= MinerSystem.OnGameEnded;
            GameEvents.GameStarted -= ModifierSystem.OnGameStarted;
            GameEvents.GameEnded -= ModifierSystem.OnGameEnded;
            GameEvents.BeforeMurder -= ModifierSystem.OnBeforeMurder;
            GameEvents.GameEnded -= CreatorColor.OnGameEnded;
            ModifierSystem.Reset();
            new Harmony(Guid + ".modifiers").UnpatchSelf();
            new Harmony(Guid + ".batch3").UnpatchSelf();
            new Harmony(Guid + ".batch4").UnpatchSelf();
            new Harmony(Guid + ".batch5").UnpatchSelf();
            new Harmony(Guid + ".mayorabstain").UnpatchSelf();
            CamouflagerSystem.Reset();
            SwooperSystem.Reset();
            UnderdogSystem.Reset();
            UndertakerSystem.Reset();
            InvestigatorSystem.Reset();
            TimeLordSystem.Reset();
            SnitchSystem.Reset();
            PhantomSystem.Reset();
            ShifterSystem.Reset();
            GlitchSystem.Reset();
            MinerSystem.Reset();
            JanitorSystem.Reset();
            AltruistSystem.Reset();
            ExecutionerSystem.Reset();
            ArsonistSystem.Reset();
            SwapperSystem.Reset();
            MorphlingSystem.Reset();
            SpySystem.Reset();
            new Harmony(Guid + ".mayor").UnpatchSelf();
            new Harmony(Guid + ".executioner").UnpatchSelf();
            new Harmony(Guid + ".arsonist").UnpatchSelf();
            new Harmony(Guid + ".swapper").UnpatchSelf();
            new Harmony(Guid + ".morphling").UnpatchSelf();
            new Harmony(Guid + ".spy").UnpatchSelf();

            if (_harmonyHooksInstalled)
            {
                new Harmony(Guid).UnpatchSelf();
                _harmonyHooksInstalled = false;
            }
            return true;
        }
    }
}

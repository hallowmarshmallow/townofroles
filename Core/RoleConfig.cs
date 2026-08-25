using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// BepInEx configuration for role registration, role-pool generation, and
    /// gameplay tuning. Role enable toggles, counts, chances, and gameplay values
    /// are grouped into stable BepInEx sections rather than the native game menu.
    /// </summary>
    internal static class RoleConfig
    {
        public static ConfigEntry<bool> Sheriff { get; private set; }
        public static ConfigEntry<bool> Engineer { get; private set; }
        public static ConfigEntry<bool> Jester { get; private set; }
        public static ConfigEntry<bool> Medic { get; private set; }
        public static ConfigEntry<bool> Seer { get; private set; }
        public static ConfigEntry<bool> Vigilante { get; private set; }
        public static ConfigEntry<bool> Assassin { get; private set; }
        public static ConfigEntry<bool> Janitor { get; private set; }
        public static ConfigEntry<bool> Altruist { get; private set; }
        public static ConfigEntry<bool> Mayor { get; private set; }
        public static ConfigEntry<bool> Executioner { get; private set; }
        public static ConfigEntry<bool> Arsonist { get; private set; }
        public static ConfigEntry<bool> Swapper { get; private set; }
        public static ConfigEntry<bool> Morphling { get; private set; }
        public static ConfigEntry<bool> Spy { get; private set; }
        public static ConfigEntry<bool> Camouflager { get; private set; }
        public static ConfigEntry<bool> Swooper { get; private set; }
        public static ConfigEntry<bool> Underdog { get; private set; }
        public static ConfigEntry<bool> Undertaker { get; private set; }
        public static ConfigEntry<bool> Investigator { get; private set; }
        public static ConfigEntry<bool> TimeLord { get; private set; }
        public static ConfigEntry<bool> Snitch { get; private set; }
        public static ConfigEntry<bool> Phantom { get; private set; }
        public static ConfigEntry<bool> Shifter { get; private set; }
        public static ConfigEntry<bool> Glitch { get; private set; }
        public static ConfigEntry<bool> Miner { get; private set; }

        public static ConfigEntry<int> SheriffCount { get; private set; }
        public static ConfigEntry<float> SheriffChance { get; private set; }
        public static ConfigEntry<int> EngineerCount { get; private set; }
        public static ConfigEntry<float> EngineerChance { get; private set; }
        public static ConfigEntry<int> JesterCount { get; private set; }
        public static ConfigEntry<float> JesterChance { get; private set; }
        public static ConfigEntry<int> MedicCount { get; private set; }
        public static ConfigEntry<float> MedicChance { get; private set; }
        public static ConfigEntry<int> SeerCount { get; private set; }
        public static ConfigEntry<float> SeerChance { get; private set; }
        public static ConfigEntry<int> VigilanteCount { get; private set; }
        public static ConfigEntry<float> VigilanteChance { get; private set; }
        public static ConfigEntry<int> AssassinCount { get; private set; }
        public static ConfigEntry<float> AssassinChance { get; private set; }
        public static ConfigEntry<int> JanitorCount { get; private set; }
        public static ConfigEntry<float> JanitorChance { get; private set; }
        public static ConfigEntry<int> AltruistCount { get; private set; }
        public static ConfigEntry<float> AltruistChance { get; private set; }
        public static ConfigEntry<int> MayorCount { get; private set; }
        public static ConfigEntry<float> MayorChance { get; private set; }
        public static ConfigEntry<int> ExecutionerCount { get; private set; }
        public static ConfigEntry<float> ExecutionerChance { get; private set; }
        public static ConfigEntry<int> ArsonistCount { get; private set; }
        public static ConfigEntry<float> ArsonistChance { get; private set; }
        public static ConfigEntry<int> SwapperCount { get; private set; }
        public static ConfigEntry<float> SwapperChance { get; private set; }
        public static ConfigEntry<int> MorphlingCount { get; private set; }
        public static ConfigEntry<float> MorphlingChance { get; private set; }
        public static ConfigEntry<int> SpyCount { get; private set; }
        public static ConfigEntry<float> SpyChance { get; private set; }
        public static ConfigEntry<int> CamouflagerCount { get; private set; }
        public static ConfigEntry<float> CamouflagerChance { get; private set; }
        public static ConfigEntry<int> SwooperCount { get; private set; }
        public static ConfigEntry<float> SwooperChance { get; private set; }
        public static ConfigEntry<int> UnderdogCount { get; private set; }
        public static ConfigEntry<float> UnderdogChance { get; private set; }
        public static ConfigEntry<int> UndertakerCount { get; private set; }
        public static ConfigEntry<float> UndertakerChance { get; private set; }
        public static ConfigEntry<int> InvestigatorCount { get; private set; }
        public static ConfigEntry<float> InvestigatorChance { get; private set; }
        public static ConfigEntry<int> TimeLordCount { get; private set; }
        public static ConfigEntry<float> TimeLordChance { get; private set; }
        public static ConfigEntry<int> SnitchCount { get; private set; }
        public static ConfigEntry<float> SnitchChance { get; private set; }
        public static ConfigEntry<int> PhantomCount { get; private set; }
        public static ConfigEntry<float> PhantomChance { get; private set; }
        public static ConfigEntry<int> ShifterCount { get; private set; }
        public static ConfigEntry<float> ShifterChance { get; private set; }
        public static ConfigEntry<int> GlitchCount { get; private set; }
        public static ConfigEntry<float> GlitchChance { get; private set; }
        public static ConfigEntry<int> MinerCount { get; private set; }
        public static ConfigEntry<float> MinerChance { get; private set; }

        public static ConfigEntry<float> SheriffKillCooldown { get; private set; }
        public static ConfigEntry<bool> SheriffKillOther { get; private set; }
        public static ConfigEntry<bool> SheriffBodyReport { get; private set; }
        public static ConfigEntry<float> EngineerFixCooldown { get; private set; }
        public static ConfigEntry<int> MedicUses { get; private set; }
        public static ConfigEntry<float> MedicCooldown { get; private set; }
        public static ConfigEntry<bool> MedicShieldBreaksOnKill { get; private set; }
        public static ConfigEntry<int> MedicReportNameDuration { get; private set; }
        public static ConfigEntry<int> MedicReportColorDuration { get; private set; }
        public static ConfigEntry<int> SeerUses { get; private set; }
        public static ConfigEntry<float> SeerCooldown { get; private set; }
        public static ConfigEntry<string> SeerRevealMode { get; private set; }
        public static ConfigEntry<int> VigilanteShots { get; private set; }
        public static ConfigEntry<float> VigilanteCooldown { get; private set; }
        public static ConfigEntry<bool> AssassinMultiKill { get; private set; }
        public static ConfigEntry<bool> AssassinMeetingUi { get; private set; }
        public static ConfigEntry<float> JanitorCleanCooldown { get; private set; }
        public static ConfigEntry<int> AltruistUses { get; private set; }
        public static ConfigEntry<float> AltruistCooldown { get; private set; }
        public static ConfigEntry<int> MayorVoteBank { get; private set; }
        public static ConfigEntry<bool> SheriffKillsNeutrals { get; private set; }
        public static ConfigEntry<float> ArsonistDouseCooldown { get; private set; }
        public static ConfigEntry<bool> ExecutionerConvertOnTargetDeath { get; private set; }
        public static ConfigEntry<string> ExecutionerConvertRole { get; private set; }
        public static ConfigEntry<float> MorphlingMorphCooldown { get; private set; }
        public static ConfigEntry<float> MorphlingMorphDuration { get; private set; }
        public static ConfigEntry<float> CamouflageCooldown { get; private set; }
        public static ConfigEntry<float> CamouflageDuration { get; private set; }
        public static ConfigEntry<float> SwoopCooldown { get; private set; }
        public static ConfigEntry<float> SwoopDuration { get; private set; }
        public static ConfigEntry<float> UnderdogCooldownMultiplier { get; private set; }
        public static ConfigEntry<float> UndertakerDragCooldown { get; private set; }
        public static ConfigEntry<float> FootprintInterval { get; private set; }
        public static ConfigEntry<float> FootprintDuration { get; private set; }
        public static ConfigEntry<bool> FootprintAnonymous { get; private set; }
        public static ConfigEntry<float> RewindCooldown { get; private set; }
        public static ConfigEntry<float> RewindSeconds { get; private set; }
        public static ConfigEntry<bool> RewindRevive { get; private set; }
        public static ConfigEntry<float> ShiftCooldown { get; private set; }
        public static ConfigEntry<float> GlitchMimicCooldown { get; private set; }
        public static ConfigEntry<float> GlitchMimicDuration { get; private set; }
        public static ConfigEntry<float> GlitchHackCooldown { get; private set; }
        public static ConfigEntry<float> GlitchHackDuration { get; private set; }
        public static ConfigEntry<float> GlitchKillCooldown { get; private set; }
        public static ConfigEntry<float> MineCooldown { get; private set; }
        public static ConfigEntry<bool> PresentationEnabled { get; private set; }
        public static ConfigEntry<bool> DeadSeeRoles { get; private set; }
        public static ConfigEntry<bool> ImpostorSeeRoles { get; private set; }

        public static ConfigEntry<bool> ModifierTorch { get; private set; }
        public static ConfigEntry<float> ModifierTorchProbability { get; private set; }
        public static ConfigEntry<bool> ModifierDiseased { get; private set; }
        public static ConfigEntry<float> ModifierDiseasedProbability { get; private set; }
        public static ConfigEntry<bool> ModifierFlash { get; private set; }
        public static ConfigEntry<float> ModifierFlashProbability { get; private set; }
        public static ConfigEntry<bool> ModifierTiebreaker { get; private set; }
        public static ConfigEntry<float> ModifierTiebreakerProbability { get; private set; }
        public static ConfigEntry<bool> ModifierDrunk { get; private set; }
        public static ConfigEntry<float> ModifierDrunkProbability { get; private set; }
        public static ConfigEntry<bool> ModifierGiant { get; private set; }
        public static ConfigEntry<float> ModifierGiantProbability { get; private set; }
        public static ConfigEntry<bool> ModifierButtonBarry { get; private set; }
        public static ConfigEntry<float> ModifierButtonBarryProbability { get; private set; }

        public static ConfigEntry<bool> GameplayHooks { get; private set; }
        public static ConfigEntry<bool> CustomAbilityButtons { get; private set; }
        public static ConfigEntry<bool> GameConfigOverlay { get; private set; }
        public static ConfigEntry<bool> NativeMenuRows { get; private set; }
        public static ConfigEntry<bool> ModsMenu { get; private set; }
        public static ConfigEntry<bool> DisableAllPatches { get; private set; }
        public static ConfigEntry<bool> LobbyCodeEnabled { get; private set; }
        public static ConfigEntry<string> LobbyCode { get; private set; }

        public static void Init(ConfigFile config)
        {
            // Read the file before binding new definitions. BepInEx does not expose
            // unbound/orphaned legacy entries through ConfigFile.Entries, so parsing
            // the small INI surface is what preserves old users' settings reliably.
            var fileValues = ReadConfigFile(config);

            Sheriff = BindRoleToggle(config, "Crewmate Roles", "Sheriff", "Add the Sheriff to the role pool.");
            Engineer = BindRoleToggle(config, "Crewmate Roles", "Engineer", "Add the Engineer to the role pool.");
            Medic = BindRoleToggle(config, "Crewmate Roles", "Medic", "Add the Medic to the role pool.");
            Seer = BindRoleToggle(config, "Crewmate Roles", "Seer", "Add the Seer to the role pool.");
            Vigilante = BindRoleToggle(config, "Crewmate Roles", "Vigilante", "Add the Vigilante to the role pool.");
            Assassin = BindRoleToggle(config, "Impostor Roles", "Assassin", "Add the Assassin to the role pool.");
            Janitor = BindRoleToggle(config, "Impostor Roles", "Janitor", "Add the Janitor to the role pool.");
            Altruist = BindRoleToggle(config, "Crewmate Roles", "Altruist", "Add the Altruist to the role pool.");
            Mayor = BindRoleToggle(config, "Crewmate Roles", "Mayor", "Add the Mayor to the role pool.");
            Jester = BindRoleToggle(config, "Neutral Roles", "Jester", "Add the Jester to the role pool.");
            Executioner = BindRoleToggle(config, "Neutral Roles", "Executioner", "Add the Executioner to the role pool.");
            Arsonist = BindRoleToggle(config, "Neutral Roles", "Arsonist", "Add the Arsonist to the role pool.");
            Swapper = BindRoleToggle(config, "Crewmate Roles", "Swapper", "Add the Swapper to the role pool.");
            Morphling = BindRoleToggle(config, "Impostor Roles", "Morphling", "Add the Morphling to the role pool.");
            Spy = BindRoleToggle(config, "Crewmate Roles", "Spy", "Add the Spy to the role pool.");
            Camouflager = BindRoleToggle(config, "Impostor Roles", "Camouflager", "Add the Camouflager to the role pool.");
            Swooper = BindRoleToggle(config, "Impostor Roles", "Swooper", "Add the Swooper to the role pool.");
            Underdog = BindRoleToggle(config, "Impostor Roles", "Underdog", "Add the Underdog to the role pool.");
            Undertaker = BindRoleToggle(config, "Impostor Roles", "Undertaker", "Add the Undertaker to the role pool.");
            Investigator = BindRoleToggle(config, "Crewmate Roles", "Investigator", "Add the Investigator to the role pool.");
            TimeLord = BindRoleToggle(config, "Crewmate Roles", "TimeLord", "Add the Time Lord to the role pool.");
            Snitch = BindRoleToggle(config, "Crewmate Roles", "Snitch", "Add the Snitch to the role pool.");
            Phantom = BindRoleToggle(config, "Neutral Roles", "Phantom", "Add the Phantom to the role pool.");
            Shifter = BindRoleToggle(config, "Neutral Roles", "Shifter", "Add the Shifter to the role pool.");
            Glitch = BindRoleToggle(config, "Neutral Roles", "Glitch", "Add The Glitch to the role pool.");
            Miner = BindRoleToggle(config, "Impostor Roles", "Miner", "Add the Miner to the role pool.");

            SheriffCount = BindCount(config, "Crewmate Roles", "SheriffCount", 1, "Maximum Sheriffs assigned per game.");
            SheriffChance = BindChance(config, "Crewmate Roles", "SheriffChance", 100f, "Chance for each Sheriff slot to be filled.");
            EngineerCount = BindCount(config, "Crewmate Roles", "EngineerCount", 1, "Maximum Engineers assigned per game.");
            EngineerChance = BindChance(config, "Crewmate Roles", "EngineerChance", 100f, "Chance for each Engineer slot to be filled.");
            MedicCount = BindCount(config, "Crewmate Roles", "MedicCount", 1, "Maximum Medics assigned per game.");
            MedicChance = BindChance(config, "Crewmate Roles", "MedicChance", 100f, "Chance for each Medic slot to be filled.");
            SeerCount = BindCount(config, "Crewmate Roles", "SeerCount", 1, "Maximum Seers assigned per game.");
            SeerChance = BindChance(config, "Crewmate Roles", "SeerChance", 100f, "Chance for each Seer slot to be filled.");
            VigilanteCount = BindCount(config, "Crewmate Roles", "VigilanteCount", 1, "Maximum Vigilantes assigned per game.");
            VigilanteChance = BindChance(config, "Crewmate Roles", "VigilanteChance", 100f, "Chance for each Vigilante slot to be filled.");
            AssassinCount = BindCount(config, "Impostor Roles", "AssassinCount", 1, "Maximum Assassins assigned per game.");
            AssassinChance = BindChance(config, "Impostor Roles", "AssassinChance", 100f, "Chance for each Assassin slot to be filled.");
            JanitorCount = BindCount(config, "Impostor Roles", "JanitorCount", 1, "Maximum Janitors assigned per game.");
            JanitorChance = BindChance(config, "Impostor Roles", "JanitorChance", 100f, "Chance for each Janitor slot to be filled.");
            AltruistCount = BindCount(config, "Crewmate Roles", "AltruistCount", 1, "Maximum Altruists assigned per game.");
            AltruistChance = BindChance(config, "Crewmate Roles", "AltruistChance", 100f, "Chance for each Altruist slot to be filled.");
            MayorCount = BindCount(config, "Crewmate Roles", "MayorCount", 1, "Maximum Mayors assigned per game.");
            MayorChance = BindChance(config, "Crewmate Roles", "MayorChance", 100f, "Chance for each Mayor slot to be filled.");
            JesterCount = BindCount(config, "Neutral Roles", "JesterCount", 1, "Maximum Jesters assigned per game.");
            JesterChance = BindChance(config, "Neutral Roles", "JesterChance", 100f, "Chance for each Jester slot to be filled.");
            ExecutionerCount = BindCount(config, "Neutral Roles", "ExecutionerCount", 1, "Maximum Executioners assigned per game.");
            ExecutionerChance = BindChance(config, "Neutral Roles", "ExecutionerChance", 100f, "Chance for each Executioner slot to be filled.");
            ArsonistCount = BindCount(config, "Neutral Roles", "ArsonistCount", 1, "Maximum Arsonists assigned per game.");
            ArsonistChance = BindChance(config, "Neutral Roles", "ArsonistChance", 100f, "Chance for each Arsonist slot to be filled.");
            SwapperCount = BindCount(config, "Crewmate Roles", "SwapperCount", 1, "Maximum Swappers assigned per game.");
            SwapperChance = BindChance(config, "Crewmate Roles", "SwapperChance", 100f, "Chance for each Swapper slot to be filled.");
            MorphlingCount = BindCount(config, "Impostor Roles", "MorphlingCount", 1, "Maximum Morphlings assigned per game.");
            MorphlingChance = BindChance(config, "Impostor Roles", "MorphlingChance", 100f, "Chance for each Morphling slot to be filled.");
            SpyCount = BindCount(config, "Crewmate Roles", "SpyCount", 1, "Maximum Spies assigned per game.");
            SpyChance = BindChance(config, "Crewmate Roles", "SpyChance", 100f, "Chance for each Spy slot to be filled.");
            CamouflagerCount = BindCount(config, "Impostor Roles", "CamouflagerCount", 1, "Maximum Camouflagers assigned per game.");
            CamouflagerChance = BindChance(config, "Impostor Roles", "CamouflagerChance", 100f, "Chance for each Camouflager slot to be filled.");
            SwooperCount = BindCount(config, "Impostor Roles", "SwooperCount", 1, "Maximum Swoopers assigned per game.");
            SwooperChance = BindChance(config, "Impostor Roles", "SwooperChance", 100f, "Chance for each Swooper slot to be filled.");
            UnderdogCount = BindCount(config, "Impostor Roles", "UnderdogCount", 1, "Maximum Underdogs assigned per game.");
            UnderdogChance = BindChance(config, "Impostor Roles", "UnderdogChance", 100f, "Chance for each Underdog slot to be filled.");
            UndertakerCount = BindCount(config, "Impostor Roles", "UndertakerCount", 1, "Maximum Undertakers assigned per game.");
            UndertakerChance = BindChance(config, "Impostor Roles", "UndertakerChance", 100f, "Chance for each Undertaker slot to be filled.");
            InvestigatorCount = BindCount(config, "Crewmate Roles", "InvestigatorCount", 1, "Maximum Investigators assigned per game.");
            InvestigatorChance = BindChance(config, "Crewmate Roles", "InvestigatorChance", 100f, "Chance for each Investigator slot to be filled.");
            TimeLordCount = BindCount(config, "Crewmate Roles", "TimeLordCount", 1, "Maximum Time Lords assigned per game.");
            TimeLordChance = BindChance(config, "Crewmate Roles", "TimeLordChance", 100f, "Chance for each Time Lord slot to be filled.");
            SnitchCount = BindCount(config, "Crewmate Roles", "SnitchCount", 1, "Maximum Snitches assigned per game.");
            SnitchChance = BindChance(config, "Crewmate Roles", "SnitchChance", 100f, "Chance for each Snitch slot to be filled.");
            PhantomCount = BindCount(config, "Neutral Roles", "PhantomCount", 1, "Maximum Phantoms assigned per game.");
            PhantomChance = BindChance(config, "Neutral Roles", "PhantomChance", 100f, "Chance for each Phantom slot to be filled.");
            ShifterCount = BindCount(config, "Neutral Roles", "ShifterCount", 1, "Maximum Shifters assigned per game.");
            ShifterChance = BindChance(config, "Neutral Roles", "ShifterChance", 100f, "Chance for each Shifter slot to be filled.");
            GlitchCount = BindCount(config, "Neutral Roles", "GlitchCount", 1, "Maximum Glitches assigned per game.");
            GlitchChance = BindChance(config, "Neutral Roles", "GlitchChance", 100f, "Chance for each Glitch slot to be filled.");
            MinerCount = BindCount(config, "Impostor Roles", "MinerCount", 1, "Maximum Miners assigned per game.");
            MinerChance = BindChance(config, "Impostor Roles", "MinerChance", 100f, "Chance for each Miner slot to be filled.");

            SheriffKillCooldown = BindSeconds(config, "Crewmate Roles", "SheriffKillCooldown", 10f, "Seconds between Sheriff shots.");
            SheriffKillOther = config.Bind("Crewmate Roles", "SheriffKillOther", true, "When Sheriff shoots a non-enemy, the target also dies.");
            SheriffBodyReport = config.Bind("Crewmate Roles", "SheriffBodyReport", false, "Allow the Sheriff to report bodies they shot themselves.");
            EngineerFixCooldown = BindSeconds(config, "Crewmate Roles", "EngineerFixCooldown", 30f, "Seconds between Engineer Fix Sab uses.");
            MedicUses = BindCount(config, "Crewmate Roles", "MedicUses", 1, "Number of shields the Medic can place per game.");
            MedicCooldown = BindSeconds(config, "Crewmate Roles", "MedicCooldown", 0f, "Seconds between Medic shields; zero allows immediate next use.");
            MedicShieldBreaksOnKill = config.Bind("Crewmate Roles", "MedicShieldBreaksOnKill", true, "Consume the shield when it blocks a murder.");
            MedicReportNameDuration = config.Bind("Crewmate Roles", "MedicReportNameDuration", 15, "Body Report: seconds after a kill within which the Medic learns the killer's name.");
            MedicReportColorDuration = config.Bind("Crewmate Roles", "MedicReportColorDuration", 40, "Body Report: seconds after a kill within which the Medic learns the killer's color shade.");
            SeerUses = BindCount(config, "Crewmate Roles", "SeerUses", 1, "Number of investigations the Seer can perform per game.");
            SeerCooldown = BindSeconds(config, "Crewmate Roles", "SeerCooldown", 0f, "Seconds between Seer investigations; zero allows immediate next use.");
            SeerRevealMode = config.Bind("Crewmate Roles", "SeerRevealMode", "Faction", "Faction or Role. Faction is safer for virtual custom roles.");
            VigilanteShots = BindCount(config, "Crewmate Roles", "VigilanteShots", 1, "Number of shots the Vigilante can take per game.");
            VigilanteCooldown = BindSeconds(config, "Crewmate Roles", "VigilanteCooldown", 0f, "Seconds between Vigilante shots; zero allows immediate next use.");
            AssassinMultiKill = config.Bind("Impostor Roles", "AssassinMultiKill", false, "Allow more than one successful Assassin guess in a meeting.");
            AssassinMeetingUi = config.Bind("Impostor Roles", "AssassinMeetingButtons", true, "Show Cycle/Guess buttons beside eligible players during meetings.");
            JanitorCleanCooldown = BindSeconds(config, "Impostor Roles", "JanitorCleanCooldown", 10f, "Seconds between Janitor cleans.");
            AltruistUses = BindCount(config, "Crewmate Roles", "AltruistUses", 1, "Number of revives the Altruist can perform (they die on use, so one per round).");
            AltruistCooldown = BindSeconds(config, "Crewmate Roles", "AltruistCooldown", 0f, "Seconds between Altruist revives; zero allows immediate next use.");
            MayorVoteBank = BindCount(config, "Crewmate Roles", "MayorVoteBank", 2, "How many votes the Mayor casts in a meeting.");
            SheriffKillsNeutrals = config.Bind("Crewmate Roles", "SheriffKillsNeutrals", true, "Allow the Sheriff to shoot neutral roles (Jester, Executioner).");
            ArsonistDouseCooldown = BindSeconds(config, "Neutral Roles", "ArsonistDouseCooldown", 10f, "Seconds between Arsonist douses.");
            ExecutionerConvertOnTargetDeath = config.Bind("Neutral Roles", "ExecutionerConvertOnTargetDeath", true, "When the Executioner's target dies without being voted out, convert the Executioner to another role.");
            ExecutionerConvertRole = config.Bind("Neutral Roles", "ExecutionerConvertRole", "Jester", "Role the Executioner becomes when their target dies: Jester or Crewmate.");
            MorphlingMorphCooldown = BindSeconds(config, "Impostor Roles", "MorphlingMorphCooldown", 15f, "Seconds between Morphling morphs.");
            MorphlingMorphDuration = BindSeconds(config, "Impostor Roles", "MorphlingMorphDuration", 10f, "How long a Morphling morph lasts.");
            CamouflageCooldown = BindSeconds(config, "Impostor Roles", "CamouflageCooldown", 30f, "Seconds between Camouflager camouflages.");
            CamouflageDuration = BindSeconds(config, "Impostor Roles", "CamouflageDuration", 10f, "How long a Camouflager camouflage lasts.");
            SwoopCooldown = BindSeconds(config, "Impostor Roles", "SwoopCooldown", 25f, "Seconds between Swooper swoops.");
            SwoopDuration = BindSeconds(config, "Impostor Roles", "SwoopDuration", 5f, "How long a Swooper swoop lasts.");
            UnderdogCooldownMultiplier = config.Bind("Impostor Roles", "UnderdogCooldownMultiplier", 0.5f, "Kill-cooldown multiplier for the Underdog while outnumbered (0.5 = half).");
            UndertakerDragCooldown = BindSeconds(config, "Impostor Roles", "UndertakerDragCooldown", 10f, "Seconds between Undertaker drags.");
            FootprintInterval = BindSeconds(config, "Crewmate Roles", "FootprintInterval", 3f, "Seconds between Investigator footprint drops.");
            FootprintDuration = BindSeconds(config, "Crewmate Roles", "FootprintDuration", 10f, "How long Investigator footprints stay visible.");
            FootprintAnonymous = config.Bind("Crewmate Roles", "FootprintAnonymous", true, "Investigator footprints are grey instead of player-colored (original Town-Of-Us AnonymousFootprints default).");
            RewindCooldown = BindSeconds(config, "Crewmate Roles", "RewindCooldown", 30f, "Seconds between Time Lord rewinds.");
            RewindSeconds = BindSeconds(config, "Crewmate Roles", "RewindSeconds", 5f, "How far back a Time Lord rewind goes.");
            RewindRevive = config.Bind("Crewmate Roles", "RewindRevive", true, "Players killed inside the rewind window are revived by the rewind (Town-Of-Us behavior).");
            ShiftCooldown = BindSeconds(config, "Neutral Roles", "ShiftCooldown", 30f, "Seconds between Shifter shifts.");
            GlitchMimicCooldown = BindSeconds(config, "Neutral Roles", "GlitchMimicCooldown", 30f, "Seconds between Glitch mimics.");
            GlitchMimicDuration = BindSeconds(config, "Neutral Roles", "GlitchMimicDuration", 10f, "How long a Glitch mimic lasts.");
            GlitchHackCooldown = BindSeconds(config, "Neutral Roles", "GlitchHackCooldown", 30f, "Seconds between Glitch hacks.");
            GlitchHackDuration = BindSeconds(config, "Neutral Roles", "GlitchHackDuration", 10f, "How long a Glitch hack lasts.");
            GlitchKillCooldown = BindSeconds(config, "Neutral Roles", "GlitchKillCooldown", 30f, "Seconds between Glitch kills.");
            MineCooldown = BindSeconds(config, "Impostor Roles", "MineCooldown", 30f, "Seconds between Miner mines.");
            PresentationEnabled = config.Bind("Presentation", "Enabled", true, "Show custom role names under visible player names during play and meetings.");
            DeadSeeRoles = config.Bind("Presentation", "DeadSeeRoles", true, "Allow dead players to see custom role names in-world and during meetings.");
            ImpostorSeeRoles = config.Bind("Presentation", "ImpostorSeeRoles", false, "Allow Impostors to see other players' custom role names.");

            ModifierTorch = BindRoleToggle(config, "Modifiers", "Torch", "Torch modifier: vision unaffected by lights sabotage (Crewmate).");
            ModifierTorchProbability = BindChance(config, "Modifiers", "TorchProbability", 0f, "Chance each eligible player gets the Torch modifier.");
            ModifierDiseased = BindRoleToggle(config, "Modifiers", "Diseased", "Diseased modifier: killing them triples the killer's kill cooldown (Crewmate).");
            ModifierDiseasedProbability = BindChance(config, "Modifiers", "DiseasedProbability", 0f, "Chance each eligible player gets the Diseased modifier.");
            ModifierFlash = BindRoleToggle(config, "Modifiers", "Flash", "Flash modifier: moves at 2x speed.");
            ModifierFlashProbability = BindChance(config, "Modifiers", "FlashProbability", 0f, "Chance each player gets the Flash modifier.");
            ModifierTiebreaker = BindRoleToggle(config, "Modifiers", "Tiebreaker", "Tiebreaker modifier: their vote decides tied meetings.");
            ModifierTiebreakerProbability = BindChance(config, "Modifiers", "TiebreakerProbability", 0f, "Chance each player gets the Tiebreaker modifier.");
            ModifierDrunk = BindRoleToggle(config, "Modifiers", "Drunk", "Drunk modifier: movement controls are inverted.");
            ModifierDrunkProbability = BindChance(config, "Modifiers", "DrunkProbability", 0f, "Chance each player gets the Drunk modifier.");
            ModifierGiant = BindRoleToggle(config, "Modifiers", "Giant", "Giant modifier: bigger body, slower walk.");
            ModifierGiantProbability = BindChance(config, "Modifiers", "GiantProbability", 0f, "Chance each player gets the Giant modifier.");
            ModifierButtonBarry = BindRoleToggle(config, "Modifiers", "ButtonBarry", "Button Barry modifier: can call an emergency meeting from anywhere (/meeting).");
            ModifierButtonBarryProbability = BindChance(config, "Modifiers", "ButtonBarryProbability", 0f, "Chance each player gets the Button Barry modifier.");

            GameplayHooks = config.Bind(
                "Diagnostics", "EnableGameplayHooks", false,
                "Enable optional Sheriff kill/report hooks. Engineer Fix Sab hooks are independent.");
            CustomAbilityButtons = config.Bind(
                "Diagnostics", "EnableCustomAbilityButtons", true,
                "Show dedicated ability buttons for Sheriff/Vigilante/Engineer/Medic/Seer. These buttons use the game's PassiveButton click path (ClickRouter), not managed Unity delegates, so they are safe on the current build.");
            GameConfigOverlay = config.Bind(
                "Menu", "GameConfigOverlay", true,
                "Show the Town Of Us role-settings tabs (Crewmate/Impostor/Neutral Roles) when the lobby game-config menu opens. Disable if the config menu is unstable.");
            NativeMenuRows = config.Bind(
                "Menu", "NativeMenuRows", false,
                "Add a clickable arrow in the corner of the game's own customize window. Clicking it swaps the window's content to a native Town Of Us roles page (Crewmate / Impostor / Neutral sections with count + / - rows built from the game's own row prefabs); clicking again restores the game's rows. Experimental: needs a restart, and while it is on the overlay does not auto-open.");
            ModsMenu = config.Bind(
                "Menu", "ModsMenu", false,
                "Show the 'Mods' management button in the bottom-right corner of the HUD (list + On/Off toggles for every loaded mod/role). Defaults OFF for HUD stability.");
            LobbyCodeEnabled = config.Bind(
                "LobbyCode", "Enabled", false,
                "Replace the randomly generated lobby code with the custom code below. The custom code is shown to everyone in the lobby. Note: in online mode the random code is the network routing key, so friends still join with the real code (the custom code is a display alias).");
            LobbyCode = config.Bind(
                "LobbyCode", "Code", "",
                "The custom lobby code to show (1-6 letters/digits, e.g. YOUSEF or MARSHY). Leave empty to disable.");
            DisableAllPatches = config.Bind(
                "Diagnostics", "DisableAllPatches", false,
                "CRASH DIAGNOSTIC: load the mod completely inert - register with Manactor and read config, but install NO Harmony patches, subscribe to NO game events, and register NO RPC handlers. Use to bisect startup crashes: true = game should launch (roles are inert); false = normal behavior.");

            MigrateLegacyEntries(config, fileValues);
        }

        public static int Count(ConfigEntry<int> entry, int fallback = 1) =>
            entry == null ? fallback : Clamp(entry.Value, 0, 15);

        public static float Chance(ConfigEntry<float> entry, float fallback = 100f) =>
            entry == null ? fallback : Clamp(entry.Value, 0f, 100f);

        public static float Seconds(ConfigEntry<float> entry, float fallback = 0f) =>
            entry == null ? fallback : Clamp(entry.Value, 0f, 600f);

        public static bool RevealRole =>
            SeerRevealMode != null && string.Equals(SeerRevealMode.Value?.Trim(), "Role", StringComparison.OrdinalIgnoreCase);

        private static ConfigEntry<int> BindCount(ConfigFile config, string section, string key, int value, string description) =>
            config.Bind(section, key, value, description);

        private static ConfigEntry<float> BindChance(ConfigFile config, string section, string key, float value, string description) =>
            config.Bind(section, key, value, description);

        private static ConfigEntry<bool> BindRoleToggle(ConfigFile config, string section, string key, string description) =>
            config.Bind(section, key, true, description);

        private static ConfigEntry<float> BindSeconds(ConfigFile config, string section, string key, float value, string description) =>
            config.Bind(section, key, value, description);

        private static void MigrateLegacyEntries(ConfigFile config, Dictionary<ConfigDefinition, string> fileValues)
        {
            // Old section -> new section/key mappings. Only migrate when the new key
            // did not already exist, so a user's new-section value always wins.
            Migrate(config, fileValues, "Crewmate Roles", "Sheriff", "Roles", "Sheriff");
            Migrate(config, fileValues, "Crewmate Roles", "Engineer", "Roles", "Engineer");
            Migrate(config, fileValues, "Crewmate Roles", "Medic", "Roles", "Medic");
            Migrate(config, fileValues, "Crewmate Roles", "Seer", "Roles", "Seer");
            Migrate(config, fileValues, "Crewmate Roles", "Vigilante", "Roles", "Vigilante");
            Migrate(config, fileValues, "Impostor Roles", "Assassin", "Roles", "Assassin");
            Migrate(config, fileValues, "Neutral Roles", "Jester", "Roles", "Jester");

            Migrate(config, fileValues, "Crewmate Roles", "SheriffCount", "Role Pool", "SheriffCount");
            Migrate(config, fileValues, "Crewmate Roles", "SheriffChance", "Role Pool", "SheriffChance");
            Migrate(config, fileValues, "Crewmate Roles", "EngineerCount", "Role Pool", "EngineerCount");
            Migrate(config, fileValues, "Crewmate Roles", "EngineerChance", "Role Pool", "EngineerChance");
            Migrate(config, fileValues, "Crewmate Roles", "MedicCount", "Role Pool", "MedicCount");
            Migrate(config, fileValues, "Crewmate Roles", "MedicChance", "Role Pool", "MedicChance");
            Migrate(config, fileValues, "Crewmate Roles", "SeerCount", "Role Pool", "SeerCount");
            Migrate(config, fileValues, "Crewmate Roles", "SeerChance", "Role Pool", "SeerChance");
            Migrate(config, fileValues, "Crewmate Roles", "VigilanteCount", "Role Pool", "VigilanteCount");
            Migrate(config, fileValues, "Crewmate Roles", "VigilanteChance", "Role Pool", "VigilanteChance");
            Migrate(config, fileValues, "Impostor Roles", "AssassinCount", "Role Pool", "AssassinCount");
            Migrate(config, fileValues, "Impostor Roles", "AssassinChance", "Role Pool", "AssassinChance");
            Migrate(config, fileValues, "Neutral Roles", "JesterCount", "Role Pool", "JesterCount");
            Migrate(config, fileValues, "Neutral Roles", "JesterChance", "Role Pool", "JesterChance");

            Migrate(config, fileValues, "Crewmate Roles", "SheriffKillCooldown", "Gameplay", "SheriffKillCooldown");
            Migrate(config, fileValues, "Crewmate Roles", "EngineerFixCooldown", "Gameplay", "EngineerFixCooldown");
            Migrate(config, fileValues, "Crewmate Roles", "MedicUses", "Gameplay", "MedicUses");
            Migrate(config, fileValues, "Crewmate Roles", "MedicCooldown", "Gameplay", "MedicCooldown");
            Migrate(config, fileValues, "Crewmate Roles", "MedicShieldBreaksOnKill", "Medic", "ShieldBreaksOnKill");
            Migrate(config, fileValues, "Crewmate Roles", "SeerUses", "Gameplay", "SeerUses");
            Migrate(config, fileValues, "Crewmate Roles", "SeerCooldown", "Gameplay", "SeerCooldown");
            Migrate(config, fileValues, "Crewmate Roles", "SeerRevealMode", "Seer", "RevealMode");
            Migrate(config, fileValues, "Crewmate Roles", "VigilanteShots", "Gameplay", "VigilanteShots");
            Migrate(config, fileValues, "Crewmate Roles", "VigilanteCooldown", "Gameplay", "VigilanteCooldown");
            Migrate(config, fileValues, "Impostor Roles", "AssassinMultiKill", "Assassin", "MultiKill");
            Migrate(config, fileValues, "Impostor Roles", "AssassinMeetingButtons", "Assassin", "MeetingButtons");
            Migrate(config, fileValues, "Crewmate Roles", "SheriffKillOther", "Sheriff", "KillOther");
            Migrate(config, fileValues, "Crewmate Roles", "SheriffBodyReport", "Sheriff", "BodyReport");

            config.Save();
        }

        private static void Migrate(
            ConfigFile config,
            Dictionary<ConfigDefinition, string> fileValues,
            string newSection,
            string newKey,
            string oldSection,
            string oldKey)
        {
            var newDefinition = new ConfigDefinition(newSection, newKey);
            var oldDefinition = new ConfigDefinition(oldSection, oldKey);
            if (fileValues.ContainsKey(newDefinition)) return;
            if (!fileValues.TryGetValue(oldDefinition, out var oldValue)) return;
            if (!TryGetEntry(config, newDefinition, out var newEntry)) return;

            try
            {
                object converted = newEntry.SettingType == typeof(string)
                    ? oldValue
                    : Convert.ChangeType(oldValue, newEntry.SettingType, CultureInfo.InvariantCulture);
                newEntry.BoxedValue = converted;
            }
            catch
            {
                // A malformed legacy value should never prevent the mod from loading;
                // the new entry simply keeps its documented default.
            }
        }

        private static Dictionary<ConfigDefinition, string> ReadConfigFile(ConfigFile config)
        {
            var values = new Dictionary<ConfigDefinition, string>();
            try
            {
                var path = config.GetType()
                    .GetProperty("ConfigFilePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(config) as string;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return values;

                var section = string.Empty;
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var separator = line.IndexOf('=');
                    if (separator <= 0 || section.Length == 0) continue;
                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    if (key.Length > 0) values[new ConfigDefinition(section, key)] = value;
                }
            }
            catch
            {
                // Config migration is best effort and must never block plugin startup.
            }
            return values;
        }

        private static bool TryGetEntry(ConfigFile config, ConfigDefinition definition, out ConfigEntryBase entry)
        {
            entry = null;
            var entries = GetEntries(config);
            if (entries == null) return false;

            try
            {
                var indexer = entries.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                entry = indexer?.GetValue(entries, new object[] { definition }) as ConfigEntryBase;
                return entry != null;
            }
            catch
            {
                return false;
            }
        }

        private static object GetEntries(ConfigFile config)
        {
            try
            {
                return config.GetType()
                    .GetProperty("Entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(config);
            }
            catch
            {
                return null;
            }
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}

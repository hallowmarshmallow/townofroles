using System;
using System.Collections.Generic;
using ClassicUs.ManuAPI;
using HarmonyLib;
using InnerNet;
using TownOfUs.ManuAPI.Commands;
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

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Wipes every role system's persistent state when the local player leaves a
    /// session. Role systems reset themselves on GameEvents.GameEnded, but quitting
    /// mid-round (freeplay exit, leaving a lobby, kick/disconnect) never raises that
    /// event — so footprints, morph targets, douse lists, kill history, vent
    /// networks and cooldowns leaked into the next session.
    ///
    /// The two patches below cover every path out of a running session:
    ///  - AmongUsClient.ExitGame            — explicit quit buttons (lobby/freeplay).
    ///  - InnerNetClient.HandleDisconnect   — kicks, errors, host closing.
    /// Both resets are idempotent; double-firing is harmless.
    /// </summary>
    internal static class SessionReset
    {
        private static readonly List<Action> Resets = new List<Action>
        {
            // Ability buttons/HUD + per-role cooldowns.
            () => Roles.CustomRoleAbilities.ResetAll(),

            // Crewmate roles.
            SheriffAbilityHolder.Reset,
            SheriffSystem.Reset,
            JesterSystem.Reset,
            MedicSystem.Reset,
            SeerSystem.Reset,
            VigilanteSystem.Reset,
            EngineerAbility.Reset,
            AltruistSystem.Reset,
            ExecutionerSystem.Reset,
            InvestigatorSystem.Reset,
            SnitchSystem.Reset,
            SpySystem.Reset,

            // Impostor roles.
            JanitorSystem.Reset,
            MorphlingSystem.Reset,
            SwooperSystem.Reset,
            UnderdogSystem.Reset,
            UndertakerSystem.Reset,
            MinerSystem.Reset,
            CamouflagerSystem.Reset,
            ShifterSystem.Reset,

            // Neutrals.
            GlitchSystem.Reset,
            ArsonistSystem.Reset,
            SwapperSystem.Reset,
            PhantomSystem.Reset,
            TimeLordSystem.Reset,

            // Cross-cutting systems.
            ModifierSystem.Reset,
            AssassinSystem.Reset,
            KillLog.Reset,
            CommandState.Reset,
            VisualEffects.Reset,
        };

        /// <summary>Run every registered reset, isolating failures so one broken
        /// system can never prevent the others from clearing.</summary>
        internal static void ResetAll()
        {
            foreach (var reset in Resets)
            {
                try { reset(); }
                catch (Exception) { /* best effort by design */ }
            }
        }

        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
        internal static class AmongUsClient_ExitGame_SessionPatch
        {
            private static void Postfix(DisconnectReasons reason)
            {
                _ = reason;
                ResetAll();
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), "HandleDisconnect",
            new[] { typeof(DisconnectReasons), typeof(string) })]
        internal static class InnerNetClient_HandleDisconnect_SessionPatch
        {
            private static void Postfix(DisconnectReasons reason, string stringReason)
            {
                _ = reason; _ = stringReason;
                ResetAll();
            }
        }
    }
}

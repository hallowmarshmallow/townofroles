using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using InnerNet;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Single source of truth for Town Of Us role settings that are editable from
    /// the in-game config overlay.
    ///
    /// Every exposed setting is a named "channel" bound to one RoleConfig entry.
    /// The host edits channels (which write RoleConfig + save). On any change, game
    /// start, or player join the host broadcasts the full value set over a Reactor
    /// RPC as a packed "name=value;..." string; clients mirror the received values
    /// back into their own RoleConfig entries, so descriptors, abilities, and the
    /// UI all read identical values on every client.
    /// </summary>
    internal static class RoleSettingsSync
    {
        private const string RpcKey = "townofus.RoleSettings";

        private enum Kind { Bool, Int, Float, String }

        private sealed class Channel
        {
            public string Name;
            public Kind Kind;
            public Func<object> Get;
            public Action<object> Set;
            public Func<string, object> Parse;
        }

        private static readonly List<Channel> Channels = new();

        public static bool Initialized { get; private set; }

        /// <summary>True when the local player can edit (no client = freeplay).</summary>
        public static bool CanEdit => AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost;

        public static void Init()
        {
            if (Initialized) return;
            Channels.Clear();

            // ── Crewmate roles ────────────────────────────────────────────────
            AddBool("Sheriff.Enabled", RoleConfig.Sheriff);
            AddInt("Sheriff.Count", RoleConfig.SheriffCount, 0, 15);
            AddFloat("Sheriff.Chance", RoleConfig.SheriffChance, 0f, 100f);
            AddFloat("Sheriff.KillCooldown", RoleConfig.SheriffKillCooldown, 0f, 600f);
            AddBool("Sheriff.KillOther", RoleConfig.SheriffKillOther);
            AddBool("Sheriff.BodyReport", RoleConfig.SheriffBodyReport);

            AddBool("Engineer.Enabled", RoleConfig.Engineer);
            AddInt("Engineer.Count", RoleConfig.EngineerCount, 0, 15);
            AddFloat("Engineer.Chance", RoleConfig.EngineerChance, 0f, 100f);
            AddFloat("Engineer.FixCooldown", RoleConfig.EngineerFixCooldown, 0f, 600f);

            AddBool("Medic.Enabled", RoleConfig.Medic);
            AddInt("Medic.Count", RoleConfig.MedicCount, 0, 15);
            AddFloat("Medic.Chance", RoleConfig.MedicChance, 0f, 100f);
            AddInt("Medic.Uses", RoleConfig.MedicUses, 0, 15);
            AddFloat("Medic.Cooldown", RoleConfig.MedicCooldown, 0f, 600f);
            AddBool("Medic.ShieldBreaksOnKill", RoleConfig.MedicShieldBreaksOnKill);

            AddBool("Seer.Enabled", RoleConfig.Seer);
            AddInt("Seer.Count", RoleConfig.SeerCount, 0, 15);
            AddFloat("Seer.Chance", RoleConfig.SeerChance, 0f, 100f);
            AddInt("Seer.Uses", RoleConfig.SeerUses, 0, 15);
            AddFloat("Seer.Cooldown", RoleConfig.SeerCooldown, 0f, 600f);
            AddString("Seer.RevealMode", RoleConfig.SeerRevealMode);

            AddBool("Vigilante.Enabled", RoleConfig.Vigilante);
            AddInt("Vigilante.Count", RoleConfig.VigilanteCount, 0, 15);
            AddFloat("Vigilante.Chance", RoleConfig.VigilanteChance, 0f, 100f);
            AddInt("Vigilante.Shots", RoleConfig.VigilanteShots, 0, 15);
            AddFloat("Vigilante.Cooldown", RoleConfig.VigilanteCooldown, 0f, 600f);

            AddBool("Altruist.Enabled", RoleConfig.Altruist);
            AddInt("Altruist.Count", RoleConfig.AltruistCount, 0, 15);
            AddFloat("Altruist.Chance", RoleConfig.AltruistChance, 0f, 100f);
            AddInt("Altruist.Uses", RoleConfig.AltruistUses, 0, 15);
            AddFloat("Altruist.Cooldown", RoleConfig.AltruistCooldown, 0f, 600f);

            AddBool("Mayor.Enabled", RoleConfig.Mayor);
            AddInt("Mayor.Count", RoleConfig.MayorCount, 0, 15);
            AddFloat("Mayor.Chance", RoleConfig.MayorChance, 0f, 100f);
            AddInt("Mayor.VoteBank", RoleConfig.MayorVoteBank, 1, 15);

            AddBool("Swapper.Enabled", RoleConfig.Swapper);
            AddInt("Swapper.Count", RoleConfig.SwapperCount, 0, 15);
            AddFloat("Swapper.Chance", RoleConfig.SwapperChance, 0f, 100f);

            AddBool("Spy.Enabled", RoleConfig.Spy);
            AddInt("Spy.Count", RoleConfig.SpyCount, 0, 15);
            AddFloat("Spy.Chance", RoleConfig.SpyChance, 0f, 100f);

            // ── Impostor roles ────────────────────────────────────────────────
            AddBool("Assassin.Enabled", RoleConfig.Assassin);
            AddInt("Assassin.Count", RoleConfig.AssassinCount, 0, 15);
            AddFloat("Assassin.Chance", RoleConfig.AssassinChance, 0f, 100f);
            AddBool("Assassin.MultiKill", RoleConfig.AssassinMultiKill);
            AddBool("Assassin.MeetingUi", RoleConfig.AssassinMeetingUi);

            AddBool("Janitor.Enabled", RoleConfig.Janitor);
            AddInt("Janitor.Count", RoleConfig.JanitorCount, 0, 15);
            AddFloat("Janitor.Chance", RoleConfig.JanitorChance, 0f, 100f);
            AddFloat("Janitor.CleanCooldown", RoleConfig.JanitorCleanCooldown, 0f, 600f);

            AddBool("Morphling.Enabled", RoleConfig.Morphling);
            AddInt("Morphling.Count", RoleConfig.MorphlingCount, 0, 15);
            AddFloat("Morphling.Chance", RoleConfig.MorphlingChance, 0f, 100f);
            AddFloat("Morphling.MorphCooldown", RoleConfig.MorphlingMorphCooldown, 0f, 600f);
            AddFloat("Morphling.MorphDuration", RoleConfig.MorphlingMorphDuration, 1f, 60f);

            AddBool("Camouflager.Enabled", RoleConfig.Camouflager);
            AddInt("Camouflager.Count", RoleConfig.CamouflagerCount, 0, 15);
            AddFloat("Camouflager.Chance", RoleConfig.CamouflagerChance, 0f, 100f);
            AddFloat("Camouflager.CamouflageCooldown", RoleConfig.CamouflageCooldown, 0f, 600f);
            AddFloat("Camouflager.CamouflageDuration", RoleConfig.CamouflageDuration, 1f, 60f);

            AddBool("Swooper.Enabled", RoleConfig.Swooper);
            AddInt("Swooper.Count", RoleConfig.SwooperCount, 0, 15);
            AddFloat("Swooper.Chance", RoleConfig.SwooperChance, 0f, 100f);
            AddFloat("Swooper.SwoopCooldown", RoleConfig.SwoopCooldown, 0f, 600f);
            AddFloat("Swooper.SwoopDuration", RoleConfig.SwoopDuration, 1f, 60f);

            AddBool("Underdog.Enabled", RoleConfig.Underdog);
            AddInt("Underdog.Count", RoleConfig.UnderdogCount, 0, 15);
            AddFloat("Underdog.Chance", RoleConfig.UnderdogChance, 0f, 100f);
            AddFloat("Underdog.CooldownMultiplier", RoleConfig.UnderdogCooldownMultiplier, 0.1f, 1f);

            AddBool("Undertaker.Enabled", RoleConfig.Undertaker);
            AddInt("Undertaker.Count", RoleConfig.UndertakerCount, 0, 15);
            AddFloat("Undertaker.Chance", RoleConfig.UndertakerChance, 0f, 100f);
            AddFloat("Undertaker.DragCooldown", RoleConfig.UndertakerDragCooldown, 0f, 600f);

            // ── Batch-4 roles (crewmate investigators + neutral phantom) ─────
            AddBool("Investigator.Enabled", RoleConfig.Investigator);
            AddInt("Investigator.Count", RoleConfig.InvestigatorCount, 0, 15);
            AddFloat("Investigator.Chance", RoleConfig.InvestigatorChance, 0f, 100f);
            AddFloat("Investigator.FootprintInterval", RoleConfig.FootprintInterval, 0.5f, 60f);
            AddFloat("Investigator.FootprintDuration", RoleConfig.FootprintDuration, 1f, 60f);

            AddBool("TimeLord.Enabled", RoleConfig.TimeLord);
            AddInt("TimeLord.Count", RoleConfig.TimeLordCount, 0, 15);
            AddFloat("TimeLord.Chance", RoleConfig.TimeLordChance, 0f, 100f);
            AddFloat("TimeLord.RewindCooldown", RoleConfig.RewindCooldown, 0f, 600f);
            AddFloat("TimeLord.RewindSeconds", RoleConfig.RewindSeconds, 1f, 30f);

            AddBool("Snitch.Enabled", RoleConfig.Snitch);
            AddInt("Snitch.Count", RoleConfig.SnitchCount, 0, 15);
            AddFloat("Snitch.Chance", RoleConfig.SnitchChance, 0f, 100f);

            AddBool("Phantom.Enabled", RoleConfig.Phantom);
            AddInt("Phantom.Count", RoleConfig.PhantomCount, 0, 15);
            AddFloat("Phantom.Chance", RoleConfig.PhantomChance, 0f, 100f);

            AddBool("Miner.Enabled", RoleConfig.Miner);
            AddInt("Miner.Count", RoleConfig.MinerCount, 0, 15);
            AddFloat("Miner.Chance", RoleConfig.MinerChance, 0f, 100f);
            AddFloat("Miner.MineCooldown", RoleConfig.MineCooldown, 0f, 600f);

            // ── Batch-5 roles (final OG set: Shifter + The Glitch) ────────────
            AddBool("Shifter.Enabled", RoleConfig.Shifter);
            AddInt("Shifter.Count", RoleConfig.ShifterCount, 0, 15);
            AddFloat("Shifter.Chance", RoleConfig.ShifterChance, 0f, 100f);
            AddFloat("Shifter.ShiftCooldown", RoleConfig.ShiftCooldown, 0f, 600f);

            AddBool("Glitch.Enabled", RoleConfig.Glitch);
            AddInt("Glitch.Count", RoleConfig.GlitchCount, 0, 15);
            AddFloat("Glitch.Chance", RoleConfig.GlitchChance, 0f, 100f);
            AddFloat("Glitch.MimicCooldown", RoleConfig.GlitchMimicCooldown, 0f, 600f);
            AddFloat("Glitch.MimicDuration", RoleConfig.GlitchMimicDuration, 1f, 60f);
            AddFloat("Glitch.HackCooldown", RoleConfig.GlitchHackCooldown, 0f, 600f);
            AddFloat("Glitch.HackDuration", RoleConfig.GlitchHackDuration, 1f, 60f);
            AddFloat("Glitch.KillCooldown", RoleConfig.GlitchKillCooldown, 0f, 600f);

            // ── Neutral roles ─────────────────────────────────────────────────
            AddBool("Jester.Enabled", RoleConfig.Jester);
            AddInt("Jester.Count", RoleConfig.JesterCount, 0, 15);
            AddFloat("Jester.Chance", RoleConfig.JesterChance, 0f, 100f);

            AddBool("Executioner.Enabled", RoleConfig.Executioner);
            AddInt("Executioner.Count", RoleConfig.ExecutionerCount, 0, 15);
            AddFloat("Executioner.Chance", RoleConfig.ExecutionerChance, 0f, 100f);
            AddBool("Executioner.ConvertOnTargetDeath", RoleConfig.ExecutionerConvertOnTargetDeath);
            AddString("Executioner.ConvertRole", RoleConfig.ExecutionerConvertRole);

            AddBool("Arsonist.Enabled", RoleConfig.Arsonist);
            AddInt("Arsonist.Count", RoleConfig.ArsonistCount, 0, 15);
            AddFloat("Arsonist.Chance", RoleConfig.ArsonistChance, 0f, 100f);
            AddFloat("Arsonist.DouseCooldown", RoleConfig.ArsonistDouseCooldown, 0f, 600f);

            // ── Modifiers ────────────────────────────────────────────────────
            AddBool("Modifiers.Torch.Enabled", RoleConfig.ModifierTorch);
            AddFloat("Modifiers.Torch.Probability", RoleConfig.ModifierTorchProbability, 0f, 100f);
            AddBool("Modifiers.Diseased.Enabled", RoleConfig.ModifierDiseased);
            AddFloat("Modifiers.Diseased.Probability", RoleConfig.ModifierDiseasedProbability, 0f, 100f);
            AddBool("Modifiers.Flash.Enabled", RoleConfig.ModifierFlash);
            AddFloat("Modifiers.Flash.Probability", RoleConfig.ModifierFlashProbability, 0f, 100f);
            AddBool("Modifiers.Tiebreaker.Enabled", RoleConfig.ModifierTiebreaker);
            AddFloat("Modifiers.Tiebreaker.Probability", RoleConfig.ModifierTiebreakerProbability, 0f, 100f);
            AddBool("Modifiers.Drunk.Enabled", RoleConfig.ModifierDrunk);
            AddFloat("Modifiers.Drunk.Probability", RoleConfig.ModifierDrunkProbability, 0f, 100f);
            AddBool("Modifiers.Giant.Enabled", RoleConfig.ModifierGiant);
            AddFloat("Modifiers.Giant.Probability", RoleConfig.ModifierGiantProbability, 0f, 100f);
            AddBool("Modifiers.ButtonBarry.Enabled", RoleConfig.ModifierButtonBarry);
            AddFloat("Modifiers.ButtonBarry.Probability", RoleConfig.ModifierButtonBarryProbability, 0f, 100f);

            Initialized = true;
        }

        // ── Typed getters (UI reads through these) ────────────────────────────
        public static bool GetBool(string name, bool def = false) => Find(name)?.Get() is bool b ? b : def;
        public static int GetInt(string name, int def = 0) => Find(name)?.Get() is int i ? i : def;
        public static float GetFloat(string name, float def = 0f) => Find(name)?.Get() is float f ? f : def;
        public static string GetString(string name, string def = "") => Find(name)?.Get() is string s ? s : def;

        // ── Typed setters (UI writes through these) ───────────────────────────
        public static void SetBool(string name, bool value) => Set(name, value);
        public static void SetInt(string name, int value) => Set(name, value);
        public static void SetFloat(string name, float value) => Set(name, value);
        public static void SetString(string name, string value) => Set(name, value);

        private static void Set(string name, object value)
        {
            if (!CanEdit) return;
            var ch = Find(name);
            if (ch == null) return;
            try { ch.Set(value); }
            catch (Exception e) { Log("set " + name + ": " + e.Message); }
        }

        /// <summary>Rebroadcast the full value set to all clients (host only).</summary>
        public static void HostBroadcast()
        {
            if (!CanEdit) return;
            // Only meaningful in a real networked lobby. In Freeplay the Reactor
            // handshake never finalizes, so SendRpcMethod spams the log with
            // "'townofus.RoleSettings' was never reserved" on every join.
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || client.GameState == InnerNetClient.GameStates.NotJoined) return;
            var payload = BuildPayload();
            if (payload == null) return;
            try { TownOfUsRpcMux.Send(RpcKey, payload); }
            catch (Exception e) { Log("broadcast: " + e.Message); }
        }

        [ReactorRpc(RpcKey)]
        private static void Receive(byte senderId, string payload)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            if (string.IsNullOrEmpty(payload)) return;

            foreach (var pair in payload.Split(';'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                var ch = Find(pair.Substring(0, eq));
                if (ch == null) continue;
                try { ch.Set(ch.Parse(pair.Substring(eq + 1))); }
                catch { /* malformed value: keep this client's current value */ }
            }
        }

        public static void OnGameStarted(GameStartedEventArgs _) => HostBroadcast();
        public static void OnPlayerJoined(PlayerConnectionEventArgs _) => HostBroadcast();

        // ── Channel plumbing ──────────────────────────────────────────────────
        private static Channel Find(string name)
        {
            for (int i = 0; i < Channels.Count; i++)
                if (Channels[i].Name == name) return Channels[i];
            return null;
        }

        private static string BuildPayload()
        {
            if (Channels.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Channels.Count; i++)
            {
                var c = Channels[i];
                string value;
                try { value = Format(c); }
                catch { continue; }
                if (sb.Length > 0) sb.Append(';');
                sb.Append(c.Name).Append('=').Append(value);
            }
            return sb.ToString();
        }

        private static string Format(Channel c)
        {
            switch (c.Kind)
            {
                case Kind.Bool: return ((bool)c.Get()).ToString();
                case Kind.Int: return ((int)c.Get()).ToString(CultureInfo.InvariantCulture);
                case Kind.Float: return ((float)c.Get()).ToString("0.##", CultureInfo.InvariantCulture);
                default: return (string)(c.Get() ?? "");
            }
        }

        private static void AddBool(string name, ConfigEntry<bool> entry) =>
            Add(name, entry, v => v, raw => raw == "True");

        private static void AddInt(string name, ConfigEntry<int> entry, int min, int max) =>
            Add(name, entry,
                v => v < min ? min : v > max ? max : v,
                raw => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : entry.Value);

        private static void AddFloat(string name, ConfigEntry<float> entry, float min, float max) =>
            Add(name, entry,
                v => v < min ? min : v > max ? max : v,
                raw => float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : entry.Value);

        private static void AddString(string name, ConfigEntry<string> entry) =>
            Add(name, entry, v => v, raw => raw);

        private static void Add<T>(string name, ConfigEntry<T> entry, Func<T, T> clamp, Func<string, T> parse)
        {
            Channels.Add(new Channel
            {
                Name = name,
                Kind = typeof(T) == typeof(bool) ? Kind.Bool
                     : typeof(T) == typeof(int) ? Kind.Int
                     : typeof(T) == typeof(float) ? Kind.Float
                     : Kind.String,
                Get = () => entry.Value,
                Set = value =>
                {
                    entry.Value = clamp((T)value);
                    try { entry.ConfigFile.Save(); } catch { }
                    if (CanEdit) HostBroadcast();
                },
                Parse = raw => (object)clamp(parse(raw)),
            });
        }

        private static void Log(string message) =>
            BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("RoleSettingsSync " + message);
    }
}

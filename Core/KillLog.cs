using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Cross-client log of every murder this round: (victim, killer, time).
    /// Ported from Town-Of-Us' MedicMod.Murder.KilledPlayers — the Medic's
    /// Body Report needs the killer and kill age of any body they report, on
    /// whichever client the Medic is playing.
    ///
    /// The vanilla game runs PlayerControl.MurderPlayer on every client via
    /// RpcMurderPlayer, so each client records locally from GameEvents.AfterMurder;
    /// the host also broadcasts a companion RPC as belt-and-braces for clients
    /// where the local event path misses (dedup is idempotent per event).
    /// </summary>
    internal static class KillLog
    {
        public struct KillEntry
        {
            public byte Victim;
            public byte Killer;
            public DateTime Time;
        }

        private const string RpcKey = "townofus.KillLogRecord";
        private static readonly List<KillEntry> Entries = new();

        public static IReadOnlyList<KillEntry> All => Entries;

        /// <summary>Most recent record for a victim, if any.</summary>
        public static bool TryGetLatest(byte victimId, out KillEntry entry)
        {
            entry = default;
            var found = false;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Victim != victimId) continue;
                if (!found || Entries[i].Time > entry.Time) { entry = Entries[i]; found = true; }
            }
            return found;
        }

        /// <summary>GameEvents.AfterMurder hook — subscribed in TownOfUsPlugin.Load().</summary>
        public static void OnAfterMurder(MurderEventArgs args)
        {
            if (args?.Target == null || args.Killer == null) return;
            Add(args.Target.PlayerId, args.Killer.PlayerId);
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
                TownOfUsRpcMux.Send(RpcKey, args.Target.PlayerId, args.Killer.PlayerId);
        }

        private static void Add(byte victim, byte killer) =>
            Entries.Add(new KillEntry { Victim = victim, Killer = killer, Time = DateTime.UtcNow });

        [ManactorRpc(RpcKey)]
        private static void OnRecord(byte senderId, byte victim, byte killer)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            // Skip if the local AfterMurder already recorded this exact kill.
            if (TryGetLatest(victim, out var existing) &&
                existing.Killer == killer &&
                (DateTime.UtcNow - existing.Time).TotalSeconds < 2.0) return;
            Add(victim, killer);
        }

        public static void Reset() => Entries.Clear();
        public static void OnGameStarted(GameStartedEventArgs _) => Reset();
        public static void OnGameEnded(GameEndedEventArgs _) => Reset();
    }
}

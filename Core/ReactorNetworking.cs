using System.Collections.Generic;
using System.Text;
using ClassicUs.Reactor;
using TownOfUs.ManuAPI.Commands;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Reactor lobby-mod-compatibility layer for Town Of Us. Wires up Reactor's
    /// handshake events so every client knows:
    ///   - which players are modded/unmodded
    ///   - when a player's mod versions mismatch the host's
    ///   - when the entire lobby runs a compatible mod set
    ///
    /// Uses SystemChat (the native popup) for all user-facing notifications so
    /// players see warnings directly — no dependency on chat-text parsing.
    /// </summary>
    internal static class ReactorNetworking
    {
        private static bool _wired;

        /// <summary>
        /// Call once in TownOfUsPlugin.Load(), after ReactorAPI.Register().
        /// Subscribes to Reactor's lobby-mod events and the match-start
        /// compatibility gate.
        /// </summary>
        internal static void Install()
        {
            if (_wired) return;
            _wired = true;

            ReactorAPI.OnPlayerModded += OnPlayerModded;
            ReactorAPI.OnPlayerUnmodded += OnPlayerUnmodded;
            ReactorAPI.OnModVersionMismatch += OnModVersionMismatch;
            ReactorAPI.OnLobbyFullyModded += OnLobbyFullyModded;
            ReactorAPI.OnJoiningUnmoddedLobby += OnJoiningUnmoddedLobby;

            // Per-player compatibility is validated at match start.
            GameEvents.GameStarted += OnGameStarted;

            TownOfUsPlugin.Log.LogInfo("Reactor lobby compat: wired (mod tracking + version checks active)");
        }

        /// <summary>
        /// Unsubscribe from all Reactor events during plugin unload.
        /// </summary>
        internal static void Uninstall()
        {
            if (!_wired) return;
            _wired = false;

            ReactorAPI.OnPlayerModded -= OnPlayerModded;
            ReactorAPI.OnPlayerUnmodded -= OnPlayerUnmodded;
            ReactorAPI.OnModVersionMismatch -= OnModVersionMismatch;
            ReactorAPI.OnLobbyFullyModded -= OnLobbyFullyModded;
            ReactorAPI.OnJoiningUnmoddedLobby -= OnJoiningUnmoddedLobby;
            GameEvents.GameStarted -= OnGameStarted;
        }

        private static void OnPlayerModded(byte playerId, List<(string mod, string version)> mods)
        {
            // Log what the player advertised. The handshake info is already
            // validated by Reactor (KickTracker), so this is informational.
            var sb = new StringBuilder();
            sb.Append("Player ").Append(playerId).Append(" mods: ");
            foreach (var (mod, version) in mods)
                sb.Append(mod).Append('@').Append(version).Append(' ');
            TownOfUsPlugin.Log.LogInfo(sb.ToString());
        }

        private static void OnPlayerUnmodded(byte playerId)
        {
            TownOfUsPlugin.Log.LogInfo("Player " + playerId + " left the modded session (or is unmodded).");
            // Re-check overall compatibility after any player change.
            CheckCompatibility();
        }

        private static void OnModVersionMismatch(byte playerId, string mod, string localVersion, string remoteVersion)
        {
            var msg = string.Format(
                "Version mismatch: Player {0} has {1} v{2} (local: v{3})",
                playerId, mod, remoteVersion, localVersion);
            TownOfUsPlugin.Log.LogWarning(msg);

            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
            {
                var displayName = PlayerName(playerId);
                SystemChat.Show(string.Format("[VERSION] {0} runs {1} v{2} — you have v{3}. Roles may not sync.", displayName, mod, remoteVersion, localVersion));
            }
        }

        private static void OnLobbyFullyModded()
        {
            var client = AmongUsClient.Instance;
            if (client == null) return;

            TownOfUsPlugin.Log.LogInfo("All players in lobby are modded with Reactor — full compat.");
            if (client.AmHost)
            {
                // Tell the host once that everyone is modded.
                SystemChat.Show("All players have Reactor — modded roles will work.");
            }
        }

        private static void OnJoiningUnmoddedLobby()
        {
            // Reactor detected that the host never sent a handshake within the
            // grace period. This mod only fires once; Reactor also auto-leaves.
            TownOfUsPlugin.Log.LogWarning("Host has no Reactor handshake — lobby is unmodded.");
        }

        private static void OnGameStarted()
        {
            // Ran from GameEvents.GameStarted (local, not networked). Perform a
            // final cross-client compatibility check once the match actually
            // begins — Reactor has had the full handshake window to gather
            // player mod lists.
            var client = AmongUsClient.Instance;
            if (client == null) return;

            if (!ReactorAPI.HasLocalMods()) return; // we are unmodded

            var unmodded = ReactorAPI.GetUnmoddedPlayers();
            if (unmodded.Count > 0 && client.AmHost)
            {
                var names = new StringBuilder();
                foreach (var id in unmodded)
                {
                    var name = PlayerName(id);
                    if (names.Length > 0) names.Append(", ");
                    names.Append(name);
                }
                var msg = string.Format(
                    "{0} {1} not running Reactor — their roles are vanilla.",
                    names,
                    unmodded.Count == 1 ? "is" : "are");
                SystemChat.Show("[UNMODDED] " + msg);
            }

            if (!ReactorAPI.IsCompatibleToPlay() && client.AmHost)
            {
                SystemChat.Show("[VERSION] Players have mismatched mod versions. Roles may not work correctly.");
            }
        }

        private static void CheckCompatibility()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (!ReactorAPI.HasLocalMods()) return;

            var unmodded = ReactorAPI.GetUnmoddedPlayers();
            if (unmodded.Count == 0 && ReactorAPI.IsCompatibleToPlay())
                TownOfUsPlugin.Log.LogInfo("Lobby is now fully compatible.");
        }

        private static string PlayerName(byte playerId)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null) continue;
                if (p.Data.PlayerId == playerId)
                    return p.Data.PlayerName ?? ("#" + playerId);
            }
            return "#" + playerId;
        }
    }
}
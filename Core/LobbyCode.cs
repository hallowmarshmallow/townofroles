using System;
using BepInEx.Logging;
using ClassicUs.Reactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using InnerNet;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Custom lobby code: replaces the randomly generated lobby code with a
    /// user-chosen word (e.g. "YOUSEF" or "MARSHY") everywhere the game displays
    /// it, and mirrors that word to every connected client.
    ///
    /// Important limitation (by game design): in online mode the 6-letter code is
    /// a deterministic encoding of the lobby's network <see cref="InnerNetClient.GameId"/>
    /// (server number + game number assigned by the matchmaking server). A fixed
    /// word can therefore only be a *display alias* — a friend still has to join
    /// with the real code, because their client has no way to know the host's
    /// random GameId before connecting. The alias is applied to the whole lobby so
    /// everyone sees the same custom code once they are in.
    /// </summary>
    internal static class LobbyCode
    {
        private const string RpcKey = "townofus.LobbyCode";

        private static string _code = string.Empty;   // normalized custom code (empty = disabled)
        private static int _learnedGameId;             // host's GameId learned via broadcast (fallback)
        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("TownOfUs LobbyCode");

        /// <summary>The active custom code, or an empty string when disabled.</summary>
        internal static string ActiveCode => _code;

        /// <summary>Re-reads the alias from config (called once at plugin load).</summary>
        internal static void Refresh()
        {
            _code = RoleConfig.LobbyCodeEnabled?.Value == true
                ? Normalize(RoleConfig.LobbyCode?.Value)
                : string.Empty;
        }

        /// <summary>Sets (or clears, with an empty string) the alias, persists it, and broadcasts.</summary>
        internal static void Set(string code)
        {
            _code = Normalize(code);
            try
            {
                if (RoleConfig.LobbyCodeEnabled != null) RoleConfig.LobbyCodeEnabled.Value = _code.Length > 0;
                if (RoleConfig.LobbyCode != null) RoleConfig.LobbyCode.Value = _code;
                RoleConfig.LobbyCode?.ConfigFile?.Save();
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not persist lobby code: " + e.Message);
            }
            HostBroadcast();
        }

        /// <summary>Sanitizes user input: A-Z / 0-9, at most 6 characters.</summary>
        internal static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var code = raw.Trim().ToUpperInvariant();
            if (code.Length > 6) return string.Empty;
            foreach (var c in code)
                if (!char.IsLetterOrDigit(c)) return string.Empty;
            return code;
        }

        /// <summary>The lobby GameId the alias applies to. Connected clients know it
        /// directly (their own <see cref="InnerNetClient.GameId"/>); the broadcast value is
        /// a fallback for the brief window before a client's GameId is populated.</summary>
        private static int CurrentRealGameId()
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.GameId != 0) return client.GameId;
            return _learnedGameId;
        }

        // ── Display override ──────────────────────────────────────────────────

        /// <summary>Rewrites <see cref="GameCode.IntToGameName"/> output when the
        /// encoded game id is the lobby's own game id.</summary>
        internal static void ApplyDisplay(int gameId, ref string result)
        {
            if (string.IsNullOrEmpty(_code)) return;
            if (gameId == 0 || gameId != CurrentRealGameId()) return;
            result = _code;
        }

        // ── Host → client sync ────────────────────────────────────────────────

        internal static void OnGameStarted(GameStartedEventArgs _) => HostBroadcast();
        internal static void OnPlayerJoined(PlayerConnectionEventArgs _) => HostBroadcast();

        private static void HostBroadcast()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (client.GameState == InnerNetClient.GameStates.NotJoined) return;

            try
            {
                TownOfUsRpcMux.Send(RpcKey, _code ?? string.Empty);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not broadcast lobby code: " + e.Message);
            }
        }

        [ReactorRpc(RpcKey)]
        private static void Receive(byte senderId, string code)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost) return;
            if (senderId != client.HostId) return;

            _code = Normalize(code);
            _learnedGameId = client.GameId;
        }
    }

    /// <summary>Applies the custom lobby code whenever the game renders its own code.</summary>
    [HarmonyPatch(typeof(InnerNet.GameCode), nameof(InnerNet.GameCode.IntToGameName))]
    internal static class GameCode_IntToGameName_LobbyCodePatch
    {
        private static void Postfix(int gameId, ref string __result)
        {
            try { LobbyCode.ApplyDisplay(gameId, ref __result); }
            catch { /* cosmetic only — never fail the game over a code alias */ }
        }
    }
}

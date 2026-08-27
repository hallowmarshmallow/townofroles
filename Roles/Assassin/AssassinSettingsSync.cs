using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Roles.Assassin
{
    internal static class AssassinSettingsSync
    {
        private const string RpcKey = "townofus.AssassinSettings";

        public static bool ActiveEnabled { get; private set; } = true;
        public static int ActiveCount { get; private set; } = 1;
        public static float ActiveChance { get; private set; } = 100f;
        public static bool ActiveMultiKill { get; private set; }
        public static bool ActiveMeetingUi { get; private set; } = true;

        public static void InitFromConfig()
        {
            ActiveEnabled = RoleConfig.Assassin?.Value != false;
            ActiveCount = RoleConfig.Count(RoleConfig.AssassinCount);
            ActiveChance = RoleConfig.Chance(RoleConfig.AssassinChance);
            ActiveMultiKill = RoleConfig.AssassinMultiKill?.Value == true;
            ActiveMeetingUi = RoleConfig.AssassinMeetingUi?.Value != false;
        }

        public static void SaveAndBroadcast()
        {
            InitFromConfig();
            RoleConfig.AssassinCount?.ConfigFile.Save();
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                HostBroadcast();
        }

        public static void HostBroadcast()
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            InitFromConfig();
            TownOfUsRpcMux.Send(RpcKey, ActiveEnabled, (byte)ActiveCount, ActiveChance, ActiveMultiKill, ActiveMeetingUi);
        }

        [ManactorRpc(RpcKey)]
        private static void Receive(byte senderId, bool enabled, byte count, float chance, bool multiKill, bool meetingUi)
        {
            var client = AmongUsClient.Instance;
            if (client == null || client.AmHost || senderId != client.HostId) return;
            ActiveEnabled = enabled;
            ActiveCount = count > 15 ? 15 : count;
            ActiveChance = chance < 0f ? 0f : chance > 100f ? 100f : chance;
            ActiveMultiKill = multiKill;
            ActiveMeetingUi = meetingUi;
        }

        public static void OnGameStarted(GameStartedEventArgs _)
        {
            InitFromConfig();
            HostBroadcast();
        }

        public static void OnPlayerJoined(PlayerConnectionEventArgs _)
        {
            HostBroadcast();
        }

        public static void OnGameEnded(GameEndedEventArgs _)
        {
            InitFromConfig();
        }
    }
}

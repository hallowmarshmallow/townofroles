namespace TownOfUs.ManuAPI.Core
{
    internal static class CommandState
    {
        private const int MaxReviveRetries = 600;
        public static bool NoGameEnd { get; private set; }
        public static byte? PendingRevivePlayerId { get; private set; }
        private static int _pendingReviveRetries;

        public static void SetNoGameEnd(bool enabled) => NoGameEnd = enabled;
        public static void QueueRevive(byte playerId)
        {
            if (PendingRevivePlayerId != playerId)
                _pendingReviveRetries = 0;
            if (++_pendingReviveRetries > MaxReviveRetries)
            {
                PendingRevivePlayerId = null;
                _pendingReviveRetries = 0;
                return;
            }
            PendingRevivePlayerId = playerId;
        }
        public static byte? TakePendingRevive()
        {
            var value = PendingRevivePlayerId;
            PendingRevivePlayerId = null;
            return value;
        }

        public static void Reset()
        {
            NoGameEnd = false;
            PendingRevivePlayerId = null;
            _pendingReviveRetries = 0;
        }
    }
}

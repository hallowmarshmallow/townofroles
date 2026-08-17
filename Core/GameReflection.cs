using System.Linq;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Adapters for game members whose shape changed in the 2026.8.9 interop.
    ///
    /// These members are exposed by the interop as PUBLIC PROPERTIES, not fields:
    ///   MeetingHud.playerStates  (Il2CppReferenceArray&lt;PlayerVoteArea&gt;)
    ///   MeetingHud.state         (MeetingHud.VoteStates)
    ///   ExileController.completeString (string)
    ///   ExileController.exiled   (GameData.PlayerInfo)
    ///   ShipStatus.AllVents      (Il2CppReferenceArray&lt;Vent&gt;)
    ///
    /// The earlier implementation looked them up with AccessTools.Field, which
    /// silently returned null at runtime and disabled the meeting role-name
    /// display, the exile reveal text, and the Miner vent fix. Accessing the
    /// public properties directly is type-safe: if the interop drifts again the
    /// build fails loudly instead of the feature silently disappearing.
    /// </summary>
    internal static class GameReflection
    {
        /// <summary>MeetingHud.playerStates (public Il2CppReferenceArray).</summary>
        public static PlayerVoteArea[] GetPlayerStates(MeetingHud meeting)
        {
            if (meeting == null) return null;
            try
            {
                var states = meeting.playerStates;
                return states == null ? null : states.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>MeetingHud.state (public MeetingHud.VoteStates).</summary>
        public static MeetingHud.VoteStates GetMeetingState(MeetingHud meeting)
        {
            if (meeting == null) return MeetingHud.VoteStates.NotVoted;
            try
            {
                return meeting.state;
            }
            catch
            {
                return MeetingHud.VoteStates.NotVoted;
            }
        }

        /// <summary>ShipStatus.AllVents (public setter).</summary>
        public static void SetAllVents(ShipStatus ship, Vent[] vents)
        {
            if (ship == null || vents == null) return;
            try
            {
                ship.AllVents = vents;
            }
            catch
            {
            }
        }

        /// <summary>ExileController.completeString (public string).</summary>
        public static void SetCompleteString(ExileController controller, string text)
        {
            if (controller == null || text == null) return;
            try
            {
                controller.completeString = text;
            }
            catch
            {
            }
        }

        /// <summary>ExileController.exiled (public GameData.PlayerInfo).</summary>
        public static GameData.PlayerInfo GetExileExiled(ExileController controller)
        {
            if (controller == null) return null;
            try
            {
                return controller.exiled;
            }
            catch
            {
                return null;
            }
        }
    }
}

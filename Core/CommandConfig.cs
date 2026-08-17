using BepInEx.Configuration;

namespace TownOfUs.ManuAPI.Core
{
    internal static class CommandConfig
    {
        public static ConfigEntry<bool> Enabled { get; private set; }
        public static ConfigEntry<bool> AlwaysCommandChat { get; private set; }
        public static ConfigEntry<bool> AllowSetRole { get; private set; }
        public static ConfigEntry<string> CustomCommands { get; private set; }
        public static ConfigEntry<bool> CustomCommandHostOnly { get; private set; }

        public static void Init(ConfigFile config)
        {
            Enabled = config.Bind(
                "Commands", "Enabled", true,
                "Enable Town Of Us in-game slash commands such as /nickname, /forcestart, /system, /tpin, and /tpout.");
            AlwaysCommandChat = config.Bind(
                "Commands", "AlwaysCommandChat", true,
                "Keep slash-command processing available after normal chat/game-end UI state changes. Does not force the chat panel to stay visually open.");
            AllowSetRole = config.Bind(
                "Commands", "AllowSetRole", true,
                "Allow the host to use /setrole and the Freeplay role selector for enabled custom roles.");
            CustomCommands = config.Bind(
                "Commands", "CustomCommands", "",
                "Custom slash commands, semicolon-separated, each in the form name=>message. " +
                "Example: hello=>Welcome to the lobby!;gg=>Good game, everyone!. " +
                "Placeholders: {player} (the sender's name) and {args} (anything typed after the command). " +
                "Anyone (or only the host, see CustomCommandHostOnly) can trigger them and the whole lobby sees the message.");
            CustomCommandHostOnly = config.Bind(
                "Commands", "CustomCommandHostOnly", false,
                "Restrict custom slash commands to the lobby host.");
        }
    }
}

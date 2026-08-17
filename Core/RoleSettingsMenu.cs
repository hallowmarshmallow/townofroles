namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Role settings are intentionally kept out of Classic Us' native GameSettingMenu.
    /// ManuAPI's public SettingsMenuAPI only appends NumberOption rows to the native
    /// scroller; on Classic Us 8.9 that path can freeze when the menu opens. The
    /// authoritative settings are grouped in RoleConfig's BepInEx sections instead:
    /// [Crewmate Roles], [Impostor Roles], and [Neutral Roles].
    /// </summary>
    internal static class RoleSettingsMenu
    {
        public static void Register()
        {
            // Deliberately no SettingsMenuAPI.Register call. This method remains as a
            // compatibility no-op so plugin startup and future callers stay simple.
        }
    }
}

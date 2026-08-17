using System;
using ClassicUs.ManuAPI;
using UnityEngine;
using TownOfUs.ManuAPI.Roles.Assassin;
using TownOfUs.ManuAPI.Roles.Executioner;
using TownOfUs.ManuAPI.Roles.Engineer;
using TownOfUs.ManuAPI.Roles.Jester;
using TownOfUs.ManuAPI.Roles.Medic;
using TownOfUs.ManuAPI.Roles.Seer;
using TownOfUs.ManuAPI.Roles.Sheriff;
using TownOfUs.ManuAPI.Roles.Vigilante;

namespace TownOfUs.ManuAPI.Core
{
    internal static class RolePresentation
    {
        public static bool TryGet(PlayerControl player, out string name, out Color color)
        {
            name = null;
            color = Color.white;
            if (player == null || player.Data == null) return false;

            foreach (var def in RoleCatalog.All)
            {
                if (!RoleRegistry.IsAssigned(player, def.Id)) continue;
                // A converted Executioner (target died) is a plain Crewmate or a
                // Jester — never still an Executioner.
                if (def.Id == "townofus.Executioner" && ExecutionerSystem.IsConverted(player))
                    return false;
                name = def.Name;
                color = def.Color;
                return true;
            }
            return false;
        }

        public static bool CanSee(PlayerControl viewer, PlayerControl target)
        {
            if (viewer == null || target == null || target.Data == null) return false;
            if (viewer == target) return true;
            if (viewer.Data.IsDead) return RoleConfig.DeadSeeRoles?.Value == true;
            return RoleConfig.ImpostorSeeRoles?.Value == true && viewer.Data.myRole != null && target.Data.myRole != null &&
                   viewer.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor &&
                   target.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor;
        }

        public static string WithRole(string playerName, string roleName) =>
            string.IsNullOrEmpty(roleName)
                ? playerName
                : playerName + "\n" + roleName;
    }
}

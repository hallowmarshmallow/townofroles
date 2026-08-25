using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ClassicUs.Manactor;
using HarmonyLib;

namespace ClassicUs.ManuAPI
{
    [BepInPlugin(Guid, "Classic Us ManuAPI", Version)]
    [BepInDependency(ManactorPlugin.Guid)]
    public class ManuAPIPlugin : BasePlugin
    {
        public const string Guid = "classicus.manuapi";
        public const string Version = "1.5.1";

        public static ManualLogSource Log;

        /// <summary>
        /// All patch classes, grouped by subsystem, in the order they are applied.
        /// Each one is pre-flighted and applied independently so a single target that
        /// moved, became non-public, or disappeared in a game update is skipped with a
        /// warning instead of crashing the whole plugin (PatchAll would detour every
        /// target blindly and can segfault on Linux IL2CPP for an incompatible layout).
        /// </summary>
        private static readonly Type[] PatchTypes = new[]
        {
            // --- Core game events ---
            typeof(PlayerControl_MurderPlayer_GameEvents_Patch),
            typeof(PlayerControl_CmdReportDeadBody_GameEvents_Patch),
            typeof(MeetingHud_Start_GameEvents_Patch),
            typeof(MeetingHud_Close_GameEvents_Patch),
            typeof(PlayerPhysics_RpcEnterVent_GameEvents_Patch),
            typeof(PlayerPhysics_ExitVent_GameEvents_Patch),
            typeof(PlayerControl_CompleteTask_GameEvents_Patch),
            typeof(PlayerControl_Exiled_GameEvents_Patch),
            typeof(HudManager_Start_GameEvents_Patch),
            typeof(AmongUsClient_OnGameEnd_GameEvents_Patch),
            typeof(AmongUsClient_OnPlayerJoined_GameEvents_Patch),
            typeof(AmongUsClient_OnPlayerLeft_GameEvents_Patch),

            // --- Roles ---
            typeof(RoleManager_AssignRole_VirtualPatch),
            typeof(RoleBehaviour_DisplayName_Patch),
            typeof(ImpostorRole_DisplayName_Patch),
            typeof(RoleBehaviour_Description_Patch),
            typeof(ImpostorRole_Description_Patch),
            typeof(RoleBehaviour_DescriptionShort_Patch),
            typeof(RoleBehaviour_TeamColor_Patch),
            typeof(RoleBehaviour_IntroSound_Patch),
            typeof(RoleBehaviour_KillAbilityName_Patch),
            typeof(RoleBehaviour_KillAbilityImageName_Patch),
            typeof(IntroCutscene_GetTeamColor_Patch),
            typeof(IntroCutscene_BeginTeam_MoveNext_Patch),
            typeof(IntroCutscene_RetryTaskTextWhenRoleArrives_MoveNext_Patch),
            typeof(RoleManager_Start_Patch),
            typeof(PlayerControl_FixedUpdate_RoleRegistry_Patch),
            typeof(IntroCutscene_CoBegin_MoveNext_Patch),
            typeof(HudManager_FixedUpdate_RoleRegistry_Patch),
            typeof(RoleBehaviour_OnAssign_Patch),
            typeof(PlayerControl_SetRole_Patch),
            typeof(TaskAdderGame_Begin_RoleRegistry_Patch),
            typeof(TaskAdderGame_OpenRoleFolder_RoleRegistry_Patch),
            typeof(RoleManager_AssignRolesForTeam_Patch),
            typeof(ExileController_Begin_Patch),
            typeof(ExileController_Animate_MoveNext_Patch),

            // --- Game modes ---
            typeof(HudManager_Start_GameMode_Patch),
            typeof(HudManager_FixedUpdate_GameMode_Patch),
            typeof(MeetingHud_Start_GameMode_Patch),
            typeof(MeetingHud_Close_GameMode_Patch),
            typeof(ShipStatus_CheckEndCriteria_GameMode_Patch),
            typeof(AmongUsClient_OnGameEnd_GameMode_Patch),
            typeof(AmongUsClient_ExitGame_GameMode_Patch),

            // --- Ability lifecycle ---
            typeof(HudManager_Start_AbilityReset_Patch),
            typeof(AmongUsClient_OnGameEnd_AbilityReset_Patch),
            typeof(EndGameManager_NextGame_AbilityReset_Patch),
            typeof(EndGameManager_Exit_AbilityReset_Patch),
            typeof(AmongUsClient_ExitGame_AbilityReset_Patch),

            // --- Settings menu ---
            typeof(SettingMenu_OnEnable_Patch),

            // --- Mod badges ---
            typeof(VersionShower_Start_Patch),
            typeof(PingTracker_Update_Patch),
        };

        public override void Load()
        {
            Log = base.Log;

            var harmony = new Harmony(Guid);
            int applied = 0;
            int skipped = 0;

            foreach (var patchType in PatchTypes)
            {
                if (SafePatch(harmony, patchType))
                    applied++;
                else
                    skipped++;
            }

            if (skipped > 0)
                Log.LogWarning($"ManuAPI: {skipped} patch(es) skipped (see warnings above). " +
                               $"{applied}/{PatchTypes.Length} patches active.");
            else
                Log.LogInfo($"ManuAPI: all {applied} patches applied.");

            Il2CppInteropCompat.Apply(harmony);
            RoleRegistry.RegisterNetworkHandlers();
            GameModeRegistry.RegisterNetworkHandlers();
            Log.LogInfo("ClassicUs.ManuAPI loaded.");
        }

        /// <summary>
        /// Attempts to install a single Harmony patch class. Returns true on success.
        ///
        /// Before calling Harmony (which generates native detours that can SIGSEGV on
        /// Linux IL2CPP if the target method layout is incompatible), we do a managed
        /// pre-flight check: resolve the [HarmonyPatch] target via reflection. If that
        /// resolution returns null or throws, we skip the patch entirely instead of
        /// letting Harmony crash the process.
        /// </summary>
        private bool SafePatch(Harmony harmony, Type patchType)
        {
            string name = patchType.Name;
            try
            {
                if (!ValidatePatchTarget(patchType))
                {
                    Log.LogWarning($"Skipping patch [{name}]: target method not found in current game version.");
                    return false;
                }

                harmony.CreateClassProcessor(patchType).Patch();
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning($"Skipping patch [{name}]: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resolves the target type and method from [HarmonyPatch] attributes.
        /// Returns false if the target type, method, or property cannot be found.
        /// </summary>
        private static bool ValidatePatchTarget(Type patchType)
        {
            var attrs = HarmonyMethodExtensions.GetFromType(patchType);
            if (attrs == null || attrs.Count == 0) return true; // no target info — let Harmony decide

            var merged = HarmonyMethod.Merge(attrs);
            if (merged.declaringType == null) return true; // manual patch, skip pre-flight

            // Force the IL2CPP type to initialise — if it doesn't exist this throws.
            try
            {
                var _ = merged.declaringType.FullName;
            }
            catch
            {
                return false;
            }

            // If no method name is specified there is nothing further to resolve.
            if (string.IsNullOrEmpty(merged.methodName)) return true;

            try
            {
                // Property getter/setter targets must resolve via the property, not a method.
                if (merged.methodType == MethodType.Getter || merged.methodType == MethodType.Setter)
                {
                    var prop = AccessTools.Property(merged.declaringType, merged.methodName);
                    if (prop == null) return false;
                    return merged.methodType == MethodType.Getter
                        ? prop.GetGetMethod(true) != null
                        : prop.GetSetMethod(true) != null;
                }

                var method = merged.argumentTypes == null || merged.argumentTypes.Length == 0
                    ? AccessTools.Method(merged.declaringType, merged.methodName)
                    : AccessTools.Method(merged.declaringType, merged.methodName, merged.argumentTypes);
                return method != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

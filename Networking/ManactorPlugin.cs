using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ClassicUs.Manactor
{
    [BepInPlugin(Guid, "Manactor", Version)]
    public class ManactorPlugin : BasePlugin
    {
        public const string Guid = "classicus.manactor";
        public const string Version = "1.1.0";

        public static ManualLogSource Log;

        /// <summary>
        /// When true, the host kicks players who never send a Manactor handshake
        /// (unmodded/vanilla clients) or whose handshake is incompatible, after the
        /// grace period. Leave false (default) for host-only mods so vanilla clients
        /// can join a modded lobby.
        /// </summary>
        public static ConfigEntry<bool> EnforceCompatibility { get; private set; }

        /// <summary>
        /// All patch types from Patches.cs, in the order they should be applied.
        /// No coroutine (IEnumerator) entry points are patched: those segfault during
        /// Harmony detour installation on Linux IL2CPP. Game-start detection uses the
        /// non-coroutine HudManager.Start callback instead of IntroCutscene.CoBegin.
        /// </summary>
        private static readonly Type[] PatchTypes = new[]
        {
            // --- Core networking (must succeed) ---
            typeof(PlayerControl_HandleRpc_Patch),
            typeof(AmongUsClient_OnPlayerJoined_Patch),
            typeof(AmongUsClient_OnPlayerLeft_Patch),
            typeof(AmongUsClient_OnGameJoined_Patch),

            // --- Lobby lifecycle ---
            typeof(GameStartManager_Start_Patch),
            typeof(GameStartManager_Update_Patch),

            // --- HUD tick (il2cpp registrar) ---
            typeof(HudManager_FixedUpdate_Il2CppTypeRegistrar_Patch),

            // --- Game events ---
            typeof(MeetingHud_Start_Patch),
            typeof(PlayerControl_MurderPlayer_Patch),
            typeof(PlayerControl_Die_Patch),
            typeof(RoleBehaviour_OnAssign_Patch),

            // --- Game started (non-coroutine, safe to detour) ---
            typeof(HudManager_Start_GameStarted_Patch),
        };

        public override void Load()
        {
            Log = base.Log;

            EnforceCompatibility = Config.Bind(
                "Handshake", "EnforceCompatibility", false,
                "Kick players who are missing Manactor or have a mismatched mod set after the handshake grace period. Leave false for host-only mods so vanilla/unmodded clients can join.");

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
                Log.LogWarning($"Manactor: {skipped} patch(es) skipped (see warnings above). " +
                               $"{applied}/{PatchTypes.Length} patches active.");
            else
                Log.LogInfo($"Manactor: all {applied} patches applied.");

            ManactorAPI.RegisterRpcMethods(typeof(CustomKillManager));

            Log.LogInfo("Manactor loaded.");
        }

        /// <summary>
        /// Attempts to install a single Harmony patch class. Returns true on success.
        ///
        /// Before calling Harmony (which generates native detours that can SIGSEGV on
        /// Linux IL2CPP if the target method layout is incompatible), we do a managed
        /// pre-flight check: resolve the [HarmonyPatch] target method via reflection.
        /// If that resolution returns null or throws, we skip the patch entirely instead
        /// of letting Harmony crash the process.
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
        /// Returns false if the target type or method cannot be found.
        /// </summary>
        private static bool ValidatePatchTarget(Type patchType)
        {
            var attrs = HarmonyMethodExtensions.GetFromType(patchType);
            if (attrs == null || attrs.Count == 0) return true; // no target info — let Harmony decide

            var merged = HarmonyMethod.Merge(attrs);
            if (merged.declaringType == null) return true; // manual patch, skip pre-flight

            // Check that the declaring type is loadable
            try
            {
                // Force the IL2CPP type to initialise — if it doesn't exist this throws
                var _ = merged.declaringType.FullName;
            }
            catch
            {
                return false;
            }

            // If a method name is specified, check that it resolves
            if (!string.IsNullOrEmpty(merged.methodName))
            {
                try
                {
                    var method = AccessTools.Method(
                        merged.declaringType,
                        merged.methodName,
                        merged.argumentTypes);
                    if (method == null) return false;
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
    }
}

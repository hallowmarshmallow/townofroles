using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Shows "TownOfUs vX.Y.Z" directly below the game's version text in the
    /// top-left corner (the "2026.8.9" readout on the main menu).
    ///
    /// Uses the same proven pattern as ManuAPI's ModBadgeAPI: a child TextMeshPro
    /// is created under the VersionShower's text and positioned from its world
    /// bounds, so it follows the vanilla layout without touching IL2CPP
    /// lifecycle methods.
    /// </summary>
    internal static class VersionBadge
    {
        private const string BadgeName = "TownOfRolesVersionBadge";

        public static void Ensure(VersionShower shower)
        {
            var versionText = shower == null ? null : shower.text;
            if (versionText == null) return;
            if (versionText.transform.Find(BadgeName) != null) return;

            versionText.ForceMeshUpdate(false, false);
            var rend = versionText.GetComponent<MeshRenderer>();
            Bounds worldBounds = rend != null ? rend.bounds : new Bounds(versionText.transform.position, Vector3.zero);
            float gap = (worldBounds.size.y > 0f ? worldBounds.size.y : 0.3f) * 0.25f;
            float rightShift = (worldBounds.size.y > 0f ? worldBounds.size.y : 0.3f) * 0.23f;

            float baseY = worldBounds.min.y;
            for (int i = 0; i < versionText.transform.childCount; i++)
            {
                var child = versionText.transform.GetChild(i);
                if (child == null) continue;
                if (!child.name.EndsWith("ModVersion") && !child.name.StartsWith("ManuAPIBadge")) continue;
                var childRend = child.GetComponent<MeshRenderer>();
                if (childRend != null) baseY = Mathf.Min(baseY, childRend.bounds.min.y);
            }

            var go = new GameObject(BadgeName);
            go.transform.SetParent(versionText.transform, true);
            go.transform.localScale = Vector3.one;
            go.transform.localRotation = Quaternion.identity;
            go.transform.position = new Vector3(
                versionText.transform.position.x + rightShift,
                baseY - gap,
                versionText.transform.position.z);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = versionText.font;
            tmp.fontSharedMaterial = versionText.fontSharedMaterial;
            tmp.text = "Town of Roles V" + TownOfUsPlugin.Version + " Beta";
            tmp.fontSize = versionText.fontSize;
            tmp.color = new Color(0.45f, 0.85f, 1f);
            tmp.alignment = versionText.alignment;
            tmp.enableWordWrapping = false;
        }
    }

    [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
    internal static class VersionShower_Start_VersionBadgePatch
    {
        private static void Postfix(VersionShower __instance)
        {
            try
            {
                VersionBadge.Ensure(__instance);
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("Town of Roles").LogError("Version badge: " + e.Message);
            }
        }
    }

    /// <summary>
    /// In-game credit line. Each frame the PingTracker rewrites the ping/fps text,
    /// so this postfix prepends the mod stack ("Town of Roles V" + "by
    /// hallowmarsh") on top of it — directly above the ping and fps labels in the
    /// lobby/HUD. The StartsWith guard keeps it idempotent regardless of whether
    /// the vanilla text is rewritten every frame or not.
    /// </summary>
    [HarmonyPatch(typeof(PingTracker), "Update")]
    internal static class PingTracker_Update_CreditPatch
    {
        private static void Postfix(PingTracker __instance)
        {
            try
            {
                var renderer = __instance == null ? null : __instance.text;
                if (renderer == null) return;
                var tmp = renderer.TextData;
                if (tmp == null) return;

                string current = tmp.text;
                if (string.IsNullOrEmpty(current) || current.StartsWith("Town of Roles V")) return;

                tmp.text = "Town of Roles V" + TownOfUsPlugin.Version + " Beta\nby <color=#FF69B4>hallowmarsh</color>\n" + current;
                renderer.RefreshMesh();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("Town of Roles").LogError("PingTracker credit: " + e.Message);
            }
        }
    }
}

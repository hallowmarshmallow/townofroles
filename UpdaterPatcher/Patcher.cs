using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Preloader.Core.Patching;

namespace TownOfUs.Updater.Patcher
{
    /// <summary>
    /// Preloader patcher that applies staged Town Of Us updates BEFORE the
    /// chainloader loads any plugin assembly.
    ///
    /// Why this exists: the game locks BepInEx/plugins/TownOfUs.ManuAPI.dll
    /// while the plugin is loaded, so the mod itself can never replace its own
    /// DLL at runtime. Instead the mod stages the downloaded DLL in
    /// BepInEx/plugins/.townofus-update/ and writes pending.json. This patcher
    /// runs first on the next launch, verifies the staged file's SHA-256, and
    /// swaps it over the live plugin DLL while nothing has loaded it yet.
    ///
    /// The patcher deliberately uses only BCL + BepInEx preloader APIs — no
    /// Unity or game types — because it executes before the game's IL2CPP
    /// interop assemblies are ready.
    /// </summary>
    [PatcherPluginInfo("townofus.updater.patcher", "Town Of Us Updater", "1.0.0")]
    public class TownOfUsUpdaterPatcher : BasePatcher
    {
        private const string StagingDirName = ".townofus-update";
        private const string PendingFileName = "pending.json";

        public override void Initialize()
        {
            try
            {
                ApplyPendingUpdate();
            }
            catch (Exception e)
            {
                Log.LogError("TownOfUs updater patcher failed: " + e);
            }
        }

        private void ApplyPendingUpdate()
        {
            string stagingDir = Path.Combine(Paths.PluginPath, StagingDirName);
            string pendingPath = Path.Combine(stagingDir, PendingFileName);
            if (!File.Exists(pendingPath))
                return; // nothing staged

            var pending = ReadPending(pendingPath);
            if (pending == null)
            {
                Log.LogWarning("TownOfUs: pending.json was unreadable; ignoring.");
                return;
            }

            string targetName = pending.Target ?? "TownOfUs.ManuAPI.dll";
            string stagedName = pending.Staged ?? targetName;
            if (string.IsNullOrEmpty(targetName) || !IsSafeFileName(targetName) ||
                string.IsNullOrEmpty(stagedName) || !IsSafeFileName(stagedName))
            {
                Log.LogError("TownOfUs: invalid file name in pending.json; refusing to apply.");
                return;
            }

            string targetPath = Path.Combine(Paths.PluginPath, targetName);
            string stagedPath = Path.Combine(stagingDir, stagedName);
            if (!File.Exists(stagedPath))
            {
                Log.LogError("TownOfUs: staged file missing (" + stagedPath + "); discarding pending update.");
                TryDelete(pendingPath);
                return;
            }

            if (!string.IsNullOrEmpty(pending.Sha256))
            {
                string actual = Sha256Hex(stagedPath);
                if (!string.Equals(actual, pending.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogError("TownOfUs: staged DLL SHA-256 mismatch (" + actual + " != " + pending.Sha256 + "); discarding pending update.");
                    TryDelete(pendingPath);
                    return;
                }
            }

            // Copy (not move) so the staged file remains until we know the copy succeeded.
            File.Copy(stagedPath, targetPath, true);
            Log.LogInfo("TownOfUs: applied update to " + targetName + " (version " + (pending.Version ?? "?") + ")");

            // Clean up staging.
            TryDelete(pendingPath);
            TryDelete(stagedPath);
            try
            {
                if (Directory.Exists(stagingDir) && Directory.GetFileSystemEntries(stagingDir).Length == 0)
                    Directory.Delete(stagingDir);
            }
            catch { }
        }

        private static PendingInfo ReadPending(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                return new PendingInfo
                {
                    Version = GetJsonString(json, "version"),
                    Target = GetJsonString(json, "target"),
                    Staged = GetJsonString(json, "staged"),
                    Sha256 = GetJsonString(json, "sha256"),
                };
            }
            catch
            {
                return null;
            }
        }

        private static string GetJsonString(string json, string key)
        {
            // Tiny extractor for the fixed pending.json shape ("key":"value").
            // No JSON dependency is pulled into the preloader on purpose.
            string needle = "\"" + key + "\"";
            int start = json.IndexOf(needle, StringComparison.Ordinal);
            if (start < 0) return null;
            int colon = json.IndexOf(':', start + needle.Length);
            if (colon < 0) return null;
            int quote = json.IndexOf('"', colon);
            if (quote < 0) return null;
            int end = json.IndexOf('"', quote + 1);
            if (end < 0) return null;
            return json.Substring(quote + 1, end - quote - 1);
        }

        private static bool IsSafeFileName(string name) =>
            !name.Contains("..") && !name.Contains("/") && !name.Contains("\\") && name.Length <= 64;

        private static string Sha256Hex(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", null));
            return sb.ToString();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private sealed class PendingInfo
        {
            public string Version;
            public string Target;
            public string Staged;
            public string Sha256;
        }
    }
}

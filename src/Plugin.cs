using System;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SeaPowerFileExistsFix
{
    // Workaround for Sea Power >= 0.8.3 under CrossOver/Wine: when the game loads data on many
    // threads at once (unit metadata, ini preloading, terrain), File.Exists can return true for
    // files that do not exist. The game then resolves base-game files to StreamingAssets/user/...,
    // fails to read them, and units end up with no data (no ships, zero points, missing terrain).
    // This plugin confirms every positive File.Exists answer inside StreamingAssets with a
    // directory listing, which stays reliable.
    [BepInPlugin("soph.seapower.fileexistsfix", "Sea Power File Exists Fix", "2.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> VerifyFileExists;
        internal static string StreamingAssetsPrefix;

        private void Awake()
        {
            Log = Logger;
            VerifyFileExists = Config.Bind("Fix", "VerifyFileExists", true,
                "When true, positive File.Exists answers for paths inside StreamingAssets are confirmed with a directory listing, because File.Exists can wrongly return true on worker threads under CrossOver/Wine.");

            // Captured here because Unity APIs must not be called from worker threads.
            StreamingAssetsPrefix = Normalize(Application.streamingAssetsPath).TrimEnd('/') + "/";
            Log.LogInfo($"Verifying File.Exists under '{StreamingAssetsPrefix}': {VerifyFileExists.Value}");
            new Harmony("soph.seapower.fileexistsfix").PatchAll(typeof(Plugin).Assembly);
        }

        private void OnApplicationQuit()
        {
            Log.LogInfo($"Rejected {FileExistsPatch.Rejected} false File.Exists answers this session.");
        }

        internal static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }

    [HarmonyPatch(typeof(File), nameof(File.Exists))]
    internal static class FileExistsPatch
    {
        internal static int Rejected;

        [ThreadStatic]
        private static bool _inside;

        private static void Postfix(string path, ref bool __result)
        {
            if (!__result || _inside || !Plugin.VerifyFileExists.Value || string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!Plugin.Normalize(path).StartsWith(Plugin.StreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _inside = true;
            try
            {
                string dir = Path.GetDirectoryName(path);
                string name = Path.GetFileName(path);
                bool confirmed;
                try
                {
                    confirmed = Directory.Exists(dir) && Directory.EnumerateFiles(dir, name).Any();
                }
                catch (Exception)
                {
                    confirmed = false;
                }
                if (!confirmed)
                {
                    __result = false;
                    int n = Interlocked.Increment(ref Rejected);
                    if (n <= 5 || n % 1000 == 0)
                    {
                        Plugin.Log.LogWarning($"Rejected false File.Exists for '{path}' on thread {Thread.CurrentThread.ManagedThreadId} ({n} so far)");
                    }
                }
            }
            finally
            {
                _inside = false;
            }
        }
    }
}

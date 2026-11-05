using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Rootborn.Editor.BuildScripts
{
    public static class BuildScript
    {
        private const string ClientName = "rootborn";
        private const string ServerName = "rootborn-server";
        private const string ClientAddressablesRuntimeDataPath = "Builds/Client/Windows/rootborn_Data/StreamingAssets/aa";

        // Nothing in the project reads the Addressables build layout, and generating it is what pops the
        // Addressables Report window open after every content build. Keep it off for every editor session.
        [InitializeOnLoadMethod]
        private static void KeepAddressablesReportClosed()
        {
            if (ProjectConfigData.GenerateBuildLayout) ProjectConfigData.GenerateBuildLayout = false;
            // The auto-open switch is internal to the package, so set it by name; ignore it if a package update renames it.
            var autoOpen = typeof(ProjectConfigData).GetProperty("AutoOpenAddressablesReport",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (autoOpen != null && autoOpen.CanWrite && autoOpen.GetValue(null) is bool open && open) autoOpen.SetValue(null, false);
            // A report window saved in the editor layout comes back on every launch; close it once the layout is restored.
            EditorApplication.delayCall += () =>
            {
                var windowType = typeof(ProjectConfigData).Assembly.GetType("UnityEditor.AddressableAssets.BuildReportVisualizer.BuildReportWindow");
                if (windowType == null) return;
                foreach (var window in Resources.FindObjectsOfTypeAll(windowType))
                    if (window is EditorWindow editorWindow) editorWindow.Close();
            };
        }

        // The enabled build settings list is the one source of playable scenes; travel destinations must be in it.
        public static string[] ClientScenePaths()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && !string.IsNullOrEmpty(scene.path)) scenes.Add(scene.path);
            return scenes.ToArray();
        }


        private static readonly string[] ServerScenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Town.unity",
            "Assets/Scenes/Beach.unity",
            "Assets/Scenes/Farm.unity"
        };

        [MenuItem("Rootborn/Build/Client Windows 64")]
        public static void BuildClientWindows64()
        {
            BuildClient(BuildTarget.StandaloneWindows64, $"Builds/Client/Windows/{ClientName}.exe");
        }

        [MenuItem("Rootborn/Build/Server Windows 64")]
        public static void BuildServerWindows64()
        {
            BuildServer(BuildTarget.StandaloneWindows64, NamedBuildTarget.Server, $"Builds/Server/Windows/{ServerName}.exe");
        }

        [MenuItem("Rootborn/Build/Server Linux 64")]
        public static void BuildServerLinux64()
        {
            BuildServer(BuildTarget.StandaloneLinux64, NamedBuildTarget.Server, $"Builds/Server/Linux/{ServerName}");
        }

        private static void BuildClient(BuildTarget target, string outputPath)
        {
            EnsureDir(outputPath);
            var addressablesRuntimePath = BuildAddressables();
            var opts = new BuildPlayerOptions
            {
                scenes = ClientScenePaths(),
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                locationPathName = outputPath,
                options = BuildOptions.None
            };
            var report = BuildPlayerWithAddressablesHookDisabled(opts);
            CopyAddressablesRuntimeDataToPlayer(addressablesRuntimePath, outputPath);
            Debug.Log($"[ROOTBORN] Client build -> {outputPath} result={report.summary.result}");
        }

        private static string BuildAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("[ROOTBORN] No AddressableAssetSettings; client builds require Addressables runtime data.");
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"[ROOTBORN] Addressables build error: {result.Error}");
            }

            var runtimePath = Addressables.BuildPath;
            var settingsPath = Path.Combine(runtimePath, "settings.json");
            if (!File.Exists(settingsPath))
            {
                throw new FileNotFoundException("[ROOTBORN] Addressables build did not produce runtime settings.json.", settingsPath);
            }

            Debug.Log($"[ROOTBORN] Addressables built: duration={result.Duration:F2}s path={runtimePath}");
            return runtimePath;
        }

        private static UnityEditor.Build.Reporting.BuildReport BuildPlayerWithAddressablesHookDisabled(BuildPlayerOptions opts)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return BuildPipeline.BuildPlayer(opts);
            }

            var previousOption = settings.BuildAddressablesWithPlayerBuild;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            try
            {
                return BuildPipeline.BuildPlayer(opts);
            }
            finally
            {
                settings.BuildAddressablesWithPlayerBuild = previousOption;
            }
        }

        private static void CopyExistingClientAddressablesRuntimeDataToPlayer(string outputPath)
        {
            CopyAddressablesRuntimeDataToPlayer(ClientAddressablesRuntimeDataPath, outputPath);
        }

        private static void CopyAddressablesRuntimeDataToPlayer(string addressablesRuntimePath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(addressablesRuntimePath) || !Directory.Exists(addressablesRuntimePath))
            {
                throw new DirectoryNotFoundException($"[ROOTBORN] Addressables runtime data path is missing: {addressablesRuntimePath}");
            }

            var playerDirectory = Path.GetDirectoryName(outputPath);
            var playerName = Path.GetFileNameWithoutExtension(outputPath);
            if (string.IsNullOrEmpty(playerDirectory) || string.IsNullOrEmpty(playerName))
            {
                throw new InvalidOperationException($"[ROOTBORN] Invalid output path: {outputPath}");
            }

            var streamingAssetsAaPath = Path.Combine(playerDirectory, $"{playerName}_Data", "StreamingAssets", "aa");
            if (Directory.Exists(streamingAssetsAaPath))
            {
                Directory.Delete(streamingAssetsAaPath, true);
            }

            CopyDirectory(addressablesRuntimePath, streamingAssetsAaPath);
            var settingsPath = Path.Combine(streamingAssetsAaPath, "settings.json");
            if (!File.Exists(settingsPath))
            {
                throw new FileNotFoundException("[ROOTBORN] Copied Addressables runtime data is missing settings.json.", settingsPath);
            }

            Debug.Log($"[ROOTBORN] Addressables runtime data copied -> {streamingAssetsAaPath}");
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (var sourceFile in Directory.GetFiles(sourceDirectory))
            {
                var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destinationFile, true);
            }

            foreach (var sourceSubdirectory in Directory.GetDirectories(sourceDirectory))
            {
                var destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory));
                CopyDirectory(sourceSubdirectory, destinationSubdirectory);
            }
        }

        private static void BuildServer(BuildTarget target, NamedBuildTarget namedTarget, string outputPath)
        {
            EnsureDir(outputPath);
            var opts = new BuildPlayerOptions
            {
                scenes = ServerScenes,
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                locationPathName = outputPath,
                options = BuildOptions.None
            };
            var report = BuildPlayerWithAddressablesHookDisabled(opts);
            CopyExistingClientAddressablesRuntimeDataToPlayer(outputPath);
            Debug.Log($"[ROOTBORN] Server build -> {outputPath} result={report.summary.result}");
        }

        private static void EnsureDir(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}

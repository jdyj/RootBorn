using System;
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

        private static readonly string[] ClientScenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/HostLobby.unity",
            "Assets/Scenes/Town.unity",
            "Assets/Scenes/Farm.unity"
        };

        private static readonly string[] ServerScenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Town.unity",
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
                scenes = ClientScenes,
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
                throw new InvalidOperationException($"[ROOTBORN] Invalid client output path: {outputPath}");
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
                throw new FileNotFoundException("[ROOTBORN] Copied client Addressables runtime data is missing settings.json.", settingsPath);
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
            var report = BuildPipeline.BuildPlayer(opts);
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

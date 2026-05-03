using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

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
            "Assets/Scenes/Farm.unity"
        };

        private static readonly string[] ServerScenes =
        {
            "Assets/Scenes/Boot.unity",
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
            var opts = new BuildPlayerOptions
            {
                scenes = ClientScenes,
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                locationPathName = outputPath,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[ROOTBORN] Client build → {outputPath} result={report.summary.result}");
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
            Debug.Log($"[ROOTBORN] Server build → {outputPath} result={report.summary.result}");
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

using System;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public static class TownSinglePlayerFallbackInstaller
    {
        private const string TownSceneName = "Town";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            if (!ShouldCreateLocalPlayerFallback() || FindPlayerRoot(scene) != null)
            {
                return;
            }

            var player = new GameObject("Player");
            player.transform.position = ResolveSpawnPosition(scene);
            SceneManager.MoveGameObjectToScene(player, scene);
            player.AddComponent<PlayerIdentity>();
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInteractionRouter>();
        }

        private static Vector3 ResolveSpawnPosition(Scene scene)
        {
            var spawn = FindRoot(scene, "TownSpawnPoint");
            return spawn != null ? spawn.transform.position : Vector3.zero;
        }

        private static GameObject FindPlayerRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root.name == "Player" || root.name == "Player(Clone)" || root.GetComponent<PlayerIdentity>() != null || root.GetComponent<PlayerController>() != null)
                {
                    return root;
                }
            }

            return null;
        }

        private static bool ShouldCreateLocalPlayerFallback()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "-mode", StringComparison.OrdinalIgnoreCase))
                {
                    string mode = i + 1 < args.Length ? args[i + 1] : string.Empty;
                    return IsSinglePlayerMode(mode);
                }

                const string prefix = "-mode=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return IsSinglePlayerMode(arg.Substring(prefix.Length));
                }
            }

            return true;
        }

        private static bool IsSinglePlayerMode(string mode)
        {
            return string.IsNullOrEmpty(mode) ||
                   (!string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase));
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
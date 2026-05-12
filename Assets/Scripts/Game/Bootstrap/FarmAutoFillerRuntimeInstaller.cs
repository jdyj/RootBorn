using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public static class FarmAutoFillerRuntimeInstaller
    {
        private const string FarmSceneName = "Farm";
        private const string FillerName = "[FarmAutoFiller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FarmSceneName)
            {
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == FarmSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponent<FarmAutoFiller>() != null)
                {
                    return;
                }
            }

            var go = new GameObject(FillerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<FarmAutoFiller>();
        }
    }
}

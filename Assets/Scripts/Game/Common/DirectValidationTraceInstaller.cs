using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Common
{
    public static class DirectValidationTraceInstaller
    {
        private const string RunnerName = "[DirectValidationTrace]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure(scene);
        }

        private static void EnsureForActiveScene()
        {
            Ensure(SceneManager.GetActiveScene());
        }

        private static void Ensure(Scene scene)
        {
            if (!DirectValidationTrace.Enabled || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private static readonly List<PlayerIdentity> PlayerBuffer = new List<PlayerIdentity>(8);

            private IEnumerator Start()
            {
                DirectValidationTrace.Log("trace started scene=" + SceneManager.GetActiveScene().name);
                while (DirectValidationTrace.Enabled)
                {
                    TraceInputAndPlayers();
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }

            private static void TraceInputAndPlayers()
            {
                var keyboard = Keyboard.current;
                string input = keyboard == null
                    ? "keyboard=null"
                    : $"keys a={keyboard.aKey.isPressed} d={keyboard.dKey.isPressed} s={keyboard.sKey.isPressed} w={keyboard.wKey.isPressed} e={keyboard.eKey.isPressed} space={keyboard.spaceKey.isPressed}";

                CollectPlayers(SceneManager.GetActiveScene(), PlayerBuffer);
                for (int i = 0; i < PlayerBuffer.Count; i++)
                {
                    var identity = PlayerBuffer[i];
                    if (identity == null)
                    {
                        continue;
                    }

                    var controller = identity.GetComponent<PlayerController>();
                    var gather = identity.GetComponent<GatherInteractor>();
                    var router = identity.GetComponent<PlayerInteractionRouter>();
                    if (router != null && router.enabled)
                    {
                        router.RefreshPromptNow();
                    }

                    bool inputEnabled = controller != null && controller.enabled || gather != null && gather.enabled || router != null && router.enabled;
                    if (!inputEnabled)
                    {
                        continue;
                    }

                    string prompt = router != null && router.PromptVisible ? router.PromptText : string.Empty;
                    DirectValidationTrace.Log($"player={identity.PlayerId} pos={identity.transform.position} controllerEnabled={(controller != null && controller.enabled)} gatherEnabled={(gather != null && gather.enabled)} routerEnabled={(router != null && router.enabled)} prompt='{prompt}' {input}");
                }
            }

            private static void CollectPlayers(Scene scene, List<PlayerIdentity> players)
            {
                players.Clear();
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    roots[i].GetComponentsInChildren(false, players);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}

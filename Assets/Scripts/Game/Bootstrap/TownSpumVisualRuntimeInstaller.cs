using System.Collections;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public sealed class TownSpumVisualRuntimeInstaller : MonoBehaviour
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownSpumVisualRuntimeInstaller]";
        private const string SpumVisualKind = "spum";

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
                EnsureRunner();
            }
        }

        private static void EnsureForActiveScene()
        {
            if (SceneManager.GetActiveScene().name == TownSceneName)
            {
                EnsureRunner();
            }
        }

        private static void EnsureRunner()
        {
            if (!HasSpumSnapshot())
                return;

            var existing = GameObject.Find(RunnerName);
            if (existing != null)
                return;

            var runner = new GameObject(RunnerName);
            runner.AddComponent<TownSpumVisualRuntimeInstaller>();
        }

        private void Start()
        {
            StartCoroutine(BindWhenPlayerExists());
        }

        private IEnumerator BindWhenPlayerExists()
        {
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                if (!HasSpumSnapshot())
                    yield break;

                GameObject player = GameObject.Find("Player");
                if (player != null)
                {
                    BindSpumVisual(player);
                    yield break;
                }

                elapsed += UnityEngine.Time.deltaTime;
                yield return null;
            }
        }

        private static void BindSpumVisual(GameObject player)
        {
            var view = player.GetComponent<SpumCharacterVisualView>();
            if (view == null)
            {
                view = player.AddComponent<SpumCharacterVisualView>();
            }

            var adapter = player.GetComponent<PlayerCharacterVisualAdapter>();
            if (adapter == null)
            {
                adapter = player.AddComponent<PlayerCharacterVisualAdapter>();
            }

            adapter.ConfigureForTests(view);
        }

        private static bool HasSpumSnapshot()
        {
            var snapshot = ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.CharacterAppearanceSnapshot : null;
            return snapshot != null && snapshot.VisualKind == SpumVisualKind;
        }
    }
}

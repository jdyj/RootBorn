using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.World;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public static class TownStudentLifeRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownStudentLifeRuntimeInstaller]";

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
            if (FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                Scene scene = SceneManager.GetActiveScene();
                var players = new List<GameObject>();
                float playerSearchDeadline = UnityEngine.Time.realtimeSinceStartup + 5f;
                while (players.Count == 0 && UnityEngine.Time.realtimeSinceStartup < playerSearchDeadline)
                {
                    CollectPlayerRoots(scene, players);
                    if (players.Count == 0)
                    {
                        yield return null;
                    }
                }

                if (players.Count == 0)
                {
                    yield break;
                }

                // Town is authored: its saved interactors already carry their SO bindings, so only progress is attached and restored.
                foreach (var candidate in players)
                {
                    EnsureProgress(candidate);
                    RestoreProgress(candidate, scene);
                }
            }
        }

        private static void EnsureProgress(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            var progress = player.GetComponent<StudentLifeProgressComponent>();
            if (progress == null)
            {
                progress = player.AddComponent<StudentLifeProgressComponent>();
            }

            progress.EnsureProgress();
        }

        private static void RestoreProgress(GameObject player, Scene scene)
        {
            var progress = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            if (progress == null)
            {
                return;
            }

            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            var traits = new List<TraitDefinition>();
            var skills = new List<SkillDefinition>();
            var careers = new List<CareerDefinition>();
            AddRange(traits, registry != null ? registry.StudentLifeTraits : null);
            AddRange(skills, registry != null ? registry.StudentLifeSkills : null);
            AddRange(careers, registry != null ? registry.Careers : null);

            var interactors = new List<StudentLifeActivityInteractor>();
            CollectComponents(scene, interactors);
            for (int i = 0; i < interactors.Count; i++)
            {
                AddUnique(traits, interactors[i].PrimaryTrait);
                AddUnique(skills, interactors[i].PrimarySkill);
                AddUnique(careers, interactors[i].PrimaryCareer);
            }

            var boards = new List<CareerPracticeBoard>();
            CollectComponents(scene, boards);
            for (int i = 0; i < boards.Count; i++)
            {
                var practices = boards[i].Practices;
                for (int j = 0; j < practices.Count; j++)
                {
                    if (practices[j] != null)
                    {
                        AddUnique(careers, practices[j].CareerHint);
                    }
                }
            }

            StudentLifeProgressPersistence.TryLoad(progress, traits, skills, careers);
        }

        private static void CollectPlayerRoots(Scene scene, List<GameObject> players)
        {
            players.Clear();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsPlayerRoot(roots[i]))
                {
                    players.Add(roots[i]);
                }
            }
        }

        private static bool IsPlayerRoot(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return root.name == "Player" ||
                   root.name == "Player(Clone)" ||
                   root.GetComponent<PlayerIdentity>() != null ||
                   root.GetComponent<PlayerController>() != null;
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

        private static void AddRange<T>(List<T> list, IReadOnlyList<T> values) where T : StudentLifeDefinitionBase
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                AddUnique(list, values[i]);
            }
        }

        private static void AddUnique<T>(List<T> list, T value) where T : StudentLifeDefinitionBase
        {
            if (value == null || string.IsNullOrEmpty(value.Id))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == value.Id)
                {
                    return;
                }
            }

            list.Add(value);
        }

        private static void CollectComponents<T>(Scene scene, List<T> results) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CollectComponents(roots[i].transform, results);
            }
        }

        private static void CollectComponents<T>(Transform root, List<T> results) where T : Component
        {
            if (root.TryGetComponent<T>(out var component))
            {
                results.Add(component);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                CollectComponents(root.GetChild(i), results);
            }
        }
    }
}

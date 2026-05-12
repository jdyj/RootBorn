using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Common
{
    public static class DirectValidationAutoplayInstaller
    {
        private const string Flag = "-directValidationAutoplay";
        private const string QuestInventoryFlag = "-directValidationAutoplayQuestInventory";
        private const string DelayFlag = "-directValidationAutoplayDelaySeconds";
        private const string RunnerName = "[DirectValidationAutoplay]";
        private const string TownSceneName = "Town";
        private const string StudyObjectName = "StudyBasicsActivity";
        private const string DayEndObjectName = "StudentDayEndBoard";
        private const string GuideNpcObjectName = "GuideNpc";
        private const string QuestResourceObjectName = "QuestResource_00";
        private static bool? s_enabled;

        private static bool Enabled
        {
            get
            {
                if (!s_enabled.HasValue)
                {
                    s_enabled = HasFlag();
                }

                return s_enabled.Value;
            }
        }

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
            if (!Enabled || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private static bool HasFlag()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasQuestInventoryFlag()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], QuestInventoryFlag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ReadDelaySeconds()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, DelayFlag, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseDelay(i + 1 < args.Length ? args[i + 1] : string.Empty);
                }

                string prefix = DelayFlag + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseDelay(arg.Substring(prefix.Length));
                }
            }

            return 0f;
        }

        private static float ParseDelay(string raw)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
            {
                return Mathf.Max(0f, seconds);
            }

            return 0f;
        }

        private sealed class Runner : MonoBehaviour
        {
            private static readonly List<PlayerIdentity> Players = new List<PlayerIdentity>(8);

            private IEnumerator Start()
            {
                EnsureKeyboardDevice();
                DirectValidationTrace.Log("autoplay started scene=" + SceneManager.GetActiveScene().name);

                PlayerIdentity identity = null;
                yield return WaitForLocalInputPlayer(value => identity = value);

                float delaySeconds = ReadDelaySeconds();
                if (delaySeconds > 0f)
                {
                    DirectValidationTrace.Log("autoplay delaying seconds=" + delaySeconds.ToString("0.###", CultureInfo.InvariantCulture));
                    yield return new WaitForSecondsRealtime(delaySeconds);
                }

                if (identity == null)
                {
                    yield return WaitForLocalInputPlayer(value => identity = value);
                }

                if (identity == null)
                {
                    DirectValidationTrace.Log("autoplay failed: local player not found");
                    yield break;
                }

                if (HasQuestInventoryFlag())
                {
                    yield return RunQuestInventoryPath(identity);
                    ReleaseAllKeys();
                    DirectValidationTrace.Log("autoplay completed quest-inventory player=" + identity.PlayerId);
                    yield break;
                }

                GameObject studyObject = null;
                float elapsed = 0f;
                while (studyObject == null && elapsed < 8f)
                {
                    studyObject = FindSceneObjectByName(SceneManager.GetActiveScene(), StudyObjectName);
                    if (studyObject == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (studyObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: study object not found");
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + StudyObjectName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, studyObject.transform.position, 0.35f, 10f);
                yield return PressKey(Key.E);
                DirectValidationTrace.Log("autoplay interacted target=" + StudyObjectName + " player=" + identity.PlayerId);
                DirectValidationTrace.Log("autoplay student progress snapshot player=" + identity.PlayerId + " " + StudentProgressSnapshot(identity));

                GameObject dayEndObject = null;
                elapsed = 0f;
                while (dayEndObject == null && elapsed < 8f)
                {
                    dayEndObject = FindSceneObjectByName(SceneManager.GetActiveScene(), DayEndObjectName);
                    if (dayEndObject == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (dayEndObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: day-end object not found");
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + DayEndObjectName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, dayEndObject.transform.position, 0.35f, 10f);
                yield return PressKey(Key.E);
                DirectValidationTrace.Log("autoplay interacted target=" + DayEndObjectName + " player=" + identity.PlayerId);
                ReleaseAllKeys();
                DirectValidationTrace.Log("autoplay completed player=" + identity.PlayerId);
            }

            private static IEnumerator RunQuestInventoryPath(PlayerIdentity identity)
            {
                GameObject guideNpc = null;
                float elapsed = 0f;
                while (guideNpc == null && elapsed < 10f)
                {
                    guideNpc = FindSceneObjectByName(SceneManager.GetActiveScene(), GuideNpcObjectName);
                    if (guideNpc == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (guideNpc == null)
                {
                    DirectValidationTrace.Log("autoplay failed: guide npc not found");
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + GuideNpcObjectName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, guideNpc.transform.position, 0.45f, 12f);
                yield return PressKey(Key.E);
                yield return new WaitForSecondsRealtime(0.35f);
                yield return PressKey(Key.E);
                DirectValidationTrace.Log("autoplay interacted target=" + GuideNpcObjectName + " player=" + identity.PlayerId);

                GameObject resourceObject = null;
                elapsed = 0f;
                while (resourceObject == null && elapsed < 10f)
                {
                    resourceObject = FindSceneObjectByName(SceneManager.GetActiveScene(), QuestResourceObjectName);
                    if (resourceObject == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (resourceObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: quest resource not found");
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + QuestResourceObjectName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, resourceObject.transform.position, 0.45f, 12f);
                yield return PressKeyRepeated(Key.E, 28, 0.08f);
                DirectValidationTrace.Log("autoplay interacted target=" + QuestResourceObjectName + " player=" + identity.PlayerId);
                DirectValidationTrace.Log("autoplay inventory snapshot player=" + identity.PlayerId + " " + InventorySnapshot(identity));
            }

            private static IEnumerator WaitForLocalInputPlayer(Action<PlayerIdentity> assign)
            {
                PlayerIdentity identity = null;
                float elapsed = 0f;
                while (identity == null && elapsed < 12f)
                {
                    identity = FindLocalInputPlayer();
                    if (identity == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(identity);
            }

            private static PlayerIdentity FindLocalInputPlayer()
            {
                CollectPlayers(SceneManager.GetActiveScene(), Players);
                for (int i = 0; i < Players.Count; i++)
                {
                    var identity = Players[i];
                    if (identity == null)
                    {
                        continue;
                    }

                    var controller = identity.GetComponent<PlayerController>();
                    var router = identity.GetComponent<PlayerInteractionRouter>();
                    if (controller != null && controller.enabled && router != null && router.enabled)
                    {
                        return identity;
                    }
                }

                return null;
            }

            private static IEnumerator MovePlayerTo(Transform player, Vector3 target, float tolerance, float timeoutSeconds)
            {
                float elapsed = 0f;
                while (player != null && elapsed < timeoutSeconds && Vector3.Distance(player.position, target) > tolerance)
                {
                    Vector3 delta = target - player.position;
                    Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                    HoldKey(key);
                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }

                ReleaseAllKeys();
                yield return null;
            }

            private static IEnumerator PressKey(Key key)
            {
                HoldKey(key);
                yield return null;
                yield return new WaitForFixedUpdate();
                ReleaseAllKeys();
                yield return null;
            }

            private static IEnumerator PressKeyRepeated(Key key, int count, float intervalSeconds)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return PressKey(key);
                    if (intervalSeconds > 0f)
                    {
                        yield return new WaitForSecondsRealtime(intervalSeconds);
                    }
                }
            }

            private static string InventorySnapshot(PlayerIdentity identity)
            {
                var inventory = identity != null ? identity.GetComponent<PlayerInventory>() : null;
                if (inventory == null)
                {
                    return "inventory=missing";
                }

                var saveData = inventory.ToSaveData();
                if (saveData == null || saveData.Items == null || saveData.Items.Length == 0)
                {
                    return "inventory=empty";
                }

                string summary = "inventory=";
                for (int i = 0; i < saveData.Items.Length; i++)
                {
                    var item = saveData.Items[i];
                    if (string.IsNullOrEmpty(item.ItemId) || item.Count <= 0)
                    {
                        continue;
                    }

                    summary += (summary == "inventory=" ? string.Empty : ",") + item.ItemId + ":" + item.Count;
                }

                return summary == "inventory=" ? "inventory=empty" : summary;
            }

            private static string StudentProgressSnapshot(PlayerIdentity identity)
            {
                var component = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var progress = component != null ? component.EnsureProgress() : null;
                if (progress == null)
                {
                    return "relationships=missing statuses=missing";
                }

                return "relationships=" + BuildRelationshipSnapshot(progress)
                    + " statuses=" + BuildStatusSnapshot(progress);
            }

            private static string BuildRelationshipSnapshot(StudentLifeProgress progress)
            {
                var ids = progress.GetRelationshipIds();
                if (ids == null || ids.Length == 0)
                {
                    return "empty";
                }

                string summary = string.Empty;
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    summary += (summary.Length == 0 ? string.Empty : ",") + id + ":" + progress.GetRelationshipValueById(id);
                }

                return summary.Length == 0 ? "empty" : summary;
            }

            private static string BuildStatusSnapshot(StudentLifeProgress progress)
            {
                var ids = progress.GetStatusIds();
                if (ids == null || ids.Length == 0)
                {
                    return "empty";
                }

                string summary = string.Empty;
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    summary += (summary.Length == 0 ? string.Empty : ",") + id + ":" + progress.GetStatusValueById(id);
                }

                return summary.Length == 0 ? "empty" : summary;
            }

            private static Keyboard EnsureKeyboardDevice()
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    return keyboard;
                }

                keyboard = InputSystem.AddDevice<Keyboard>();
                DirectValidationTrace.Log("autoplay created keyboard device for batchmode input");
                return keyboard;
            }

            private static void HoldKey(Key key)
            {
                var keyboard = EnsureKeyboardDevice();
                if (keyboard == null)
                {
                    return;
                }

                var state = new KeyboardState();
                state.Set(key, true);
                InputSystem.QueueStateEvent(keyboard, state);
            }

            private static void ReleaseAllKeys()
            {
                var keyboard = EnsureKeyboardDevice();
                if (keyboard == null)
                {
                    return;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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

        private static GameObject FindSceneObjectByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindChildByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChildByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Game.Common
{
    public static class DirectValidationAutoplayInstaller
    {
        private const string Flag = "-directValidationAutoplay";
        private const string QuestInventoryFlag = "-directValidationAutoplayQuestInventory";
        private const string WorldStateClaimFlag = "-directValidationAutoplayWorldStateClaim";
        private const string WorldStateObserveFlag = "-directValidationAutoplayWorldStateObserve";
        private const string DelayFlag = "-directValidationAutoplayDelaySeconds";
        private const string RunnerName = "[DirectValidationAutoplay]";
        private const string TownSceneName = "Town";
        private const string StudyObjectName = "StudyBasicsActivity";
        private const string DayEndObjectName = "StudentDayEndBoard";
        private const string GuideNpcObjectName = "GuideNpc";
        private const string QuestResourceObjectName = "QuestResource_00";
        private const string WorldStateLogButtonName = "WorldStateLogButton";
        private const string WorldStateMarkerPrefix = "WorldStateChange_";
        private static bool? s_enabled;

        private static bool Enabled
        {
            get
            {
                if (!s_enabled.HasValue)
                {
                    s_enabled = HasFlag(Flag);
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

        private static bool HasFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
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

                if (HasFlag(WorldStateClaimFlag))
                {
                    yield return RunWorldStateClaimPath(identity);
                    ReleaseAllKeys();
                    DirectValidationTrace.Log("autoplay completed world-state-claim player=" + identity.PlayerId);
                    yield break;
                }

                if (HasFlag(WorldStateObserveFlag))
                {
                    yield return RunWorldStateObservePath(identity);
                    ReleaseAllKeys();
                    DirectValidationTrace.Log("autoplay completed world-state-observe player=" + identity.PlayerId);
                    yield break;
                }

                if (HasFlag(QuestInventoryFlag))
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

            private static IEnumerator RunWorldStateClaimPath(PlayerIdentity identity)
            {
                yield return RunQuestInventoryPath(identity);
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
                    DirectValidationTrace.Log("autoplay failed: guide npc not found for world-state claim");
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + GuideNpcObjectName + " claim player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, guideNpc.transform.position + new Vector3(0.75f, 0f, 0f), 0.35f, 12f);
                yield return PressKey(Key.E);
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickFirstDialogueChoiceButton("world-state-claim", identity);
                yield return new WaitForSecondsRealtime(1f);
                yield return ClickWorldStateLogButton(identity, "world-state-claim-refresh");
                yield return WaitForWorldStateMarker(5f);
                DirectValidationTrace.Log("autoplay world-state snapshot player=" + identity.PlayerId + " " + WorldStateSnapshot(identity));
                DirectValidationTrace.Log("autoplay world-state marker snapshot player=" + identity.PlayerId + " " + WorldStateMarkerSnapshot());
            }

            private static IEnumerator RunWorldStateObservePath(PlayerIdentity identity)
            {
                GameObject buttonObject = null;
                float elapsed = 0f;
                while (buttonObject == null && elapsed < 15f)
                {
                    buttonObject = FindSceneObjectByName(SceneManager.GetActiveScene(), WorldStateLogButtonName);
                    if (buttonObject == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (buttonObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: WorldStateLogButton not found player=" + identity.PlayerId);
                    DirectValidationTrace.Log("autoplay world-state snapshot player=" + identity.PlayerId + " " + WorldStateSnapshot(identity));
                    yield break;
                }

                var button = buttonObject.GetComponent<Button>();
                if (button != null && EventSystem.current != null)
                {
                    ExecuteEvents.Execute(buttonObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    yield return null;
                    DirectValidationTrace.Log("autoplay clicked WorldStateLogButton player=" + identity.PlayerId);
                }
                else
                {
                    DirectValidationTrace.Log("autoplay skipped WorldStateLogButton click player=" + identity.PlayerId + " button=" + (button != null) + " eventSystem=" + (EventSystem.current != null));
                }

                yield return new WaitForSecondsRealtime(0.5f);
                DirectValidationTrace.Log("autoplay world-state snapshot player=" + identity.PlayerId + " " + WorldStateSnapshot(identity));
                DirectValidationTrace.Log("autoplay world-state marker snapshot player=" + identity.PlayerId + " " + WorldStateMarkerSnapshot());
            }

            private static IEnumerator ClickWorldStateLogButton(PlayerIdentity identity, string purpose)
            {
                GameObject buttonObject = null;
                float elapsed = 0f;
                while (buttonObject == null && elapsed < 15f)
                {
                    buttonObject = FindSceneObjectByName(SceneManager.GetActiveScene(), WorldStateLogButtonName);
                    if (buttonObject == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (buttonObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: WorldStateLogButton not found purpose=" + purpose + " player=" + identity.PlayerId);
                    yield break;
                }

                var button = buttonObject.GetComponent<Button>();
                if (button != null && EventSystem.current != null)
                {
                    ExecuteEvents.Execute(buttonObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    yield return null;
                    DirectValidationTrace.Log("autoplay clicked WorldStateLogButton purpose=" + purpose + " player=" + identity.PlayerId);
                }
                else
                {
                    DirectValidationTrace.Log("autoplay skipped WorldStateLogButton click purpose=" + purpose + " player=" + identity.PlayerId + " button=" + (button != null) + " eventSystem=" + (EventSystem.current != null));
                }
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
                yield return MovePlayerTo(identity.transform, guideNpc.transform.position + new Vector3(0.75f, 0f, 0f), 0.35f, 12f);
                yield return PressKey(Key.E);
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickFirstDialogueChoiceButton("quest-accept", identity);
                yield return new WaitForSecondsRealtime(0.5f);
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
                yield return MovePlayerTo(identity.transform, resourceObject.transform.position + new Vector3(0.35f, 0f, 0f), 0.3f, 12f);
                for (int i = 0; i < 80 && InventorySnapshot(identity) == "inventory=empty"; i++)
                {
                    yield return PressKey(Key.E);
                    yield return new WaitForSecondsRealtime(0.08f);
                }
                DirectValidationTrace.Log("autoplay interacted target=" + QuestResourceObjectName + " player=" + identity.PlayerId);
                DirectValidationTrace.Log("autoplay inventory snapshot player=" + identity.PlayerId + " " + InventorySnapshot(identity));
            }

            private static IEnumerator ClickFirstDialogueChoiceButton(string purpose, PlayerIdentity identity)
            {
                GameObject buttonObject = null;
                float elapsed = 0f;
                while (buttonObject == null && elapsed < 5f)
                {
                    buttonObject = FindFirstActiveDialogueChoiceButtonObject();
                    if (buttonObject == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (buttonObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: dialogue choice not found purpose=" + purpose + " player=" + identity.PlayerId);
                    yield break;
                }

                var button = buttonObject.GetComponent<Button>();
                if (button != null && button.interactable && EventSystem.current != null)
                {
                    string buttonName = buttonObject.name;
                    ExecuteEvents.Execute(buttonObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    yield return null;
                    DirectValidationTrace.Log("autoplay clicked dialogue choice purpose=" + purpose + " player=" + identity.PlayerId + " button=" + buttonName);
                }
                else
                {
                    DirectValidationTrace.Log("autoplay skipped dialogue choice purpose=" + purpose + " player=" + identity.PlayerId + " button=" + (button != null) + " interactable=" + (button != null && button.interactable) + " eventSystem=" + (EventSystem.current != null));
                }
            }

            private static GameObject FindFirstActiveDialogueChoiceButtonObject()
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    var choiceRoot = FindChildByName(roots[i].transform, "ChoiceButtons");
                    if (choiceRoot == null || !choiceRoot.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    for (int childIndex = 0; childIndex < choiceRoot.childCount; childIndex++)
                    {
                        var child = choiceRoot.GetChild(childIndex);
                        if (child != null && child.gameObject.activeInHierarchy && child.GetComponent<Button>() != null)
                        {
                            return child.gameObject;
                        }
                    }
                }

                return null;
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

            private static string WorldStateSnapshot(PlayerIdentity identity)
            {
                string playerId = identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
                string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId) ? ActiveSaveContext.Metadata.SlotId : "default";
                var progress = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
                var saveData = progress.ToSaveData();
                if (saveData == null || saveData.Records == null || saveData.Records.Length == 0)
                {
                    return "worldState=empty saveSlot=" + saveSlot;
                }

                string summary = "worldState=";
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var record = saveData.Records[i];
                    if (record == null || string.IsNullOrEmpty(record.FlagId)) continue;
                    summary += (summary == "worldState=" ? string.Empty : ",") + record.FlagId + ":" + record.Scope;
                }

                return summary + " saveSlot=" + saveSlot;
            }

            private static IEnumerator WaitForWorldStateMarker(float timeoutSeconds)
            {
                float elapsed = 0f;
                while (elapsed < timeoutSeconds && WorldStateMarkerSnapshot() == "markers=empty")
                {
                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            private static string WorldStateMarkerSnapshot()
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                string summary = "markers=";
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i] == null || !roots[i].name.StartsWith(WorldStateMarkerPrefix, StringComparison.Ordinal)) continue;
                    summary += (summary == "markers=" ? string.Empty : ",") + roots[i].name;
                }

                return summary == "markers=" ? "markers=empty" : summary;
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

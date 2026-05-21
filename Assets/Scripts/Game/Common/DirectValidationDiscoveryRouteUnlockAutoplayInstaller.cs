using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
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
    public static class DirectValidationDiscoveryRouteUnlockAutoplayInstaller
    {
        private const string Flag = "-directValidationAutoplayDiscoveryRouteUnlock";
        private const string ObserveOnlyFlag = "-directValidationAutoplayDiscoveryRouteUnlockObserveOnly";
        private const string ScreenshotFlag = "-directValidationScreenshot";
        private const string RunnerName = "[DirectValidationDiscoveryRouteUnlockAutoplay]";
        private const string TownSceneName = "Town";
        private const string ClueId = "clue.discovery.play-loop";
        private const string WorldStateId = "world.library.archive-open";
        private const string RouteId = "daily.route.library.archive-research";
        private const string RouteLabel = "daily.route.library.archive-research";
        private const string RouteObjectiveTitle = "objective.daily.library.archive.title";
        private const string DuplicateRowsCleanMarker = "duplicateRows:0";
        private const string InterpretationButtonText = "restore";
        private const string DailyPlanBoardName = "DailyPlanBoard";
        private const string DailyPlanPanelTypeName = "Rootborn.UI.StudentLife.DailyPlanPanel";
        private const string ClueInterpretationInteractorTypeName = "Rootborn.UI.DiscoveryClues.ClueInterpretationInteractor";
        private const string ObjectiveJournalPanelTypeName = "Rootborn.UI.Objectives.ObjectiveJournalPanel";
        private const string ObjectiveJournalRuntimeProviderTypeName = "Rootborn.UI.Objectives.ObjectiveJournalRuntimeProvider";
        private const string TrackedObjectiveHudTypeName = "Rootborn.UI.Objectives.TrackedObjectiveHud";
        private static bool? s_enabled;

        private static bool Enabled
        {
            get
            {
                if (!s_enabled.HasValue) s_enabled = HasFlag(Flag) || HasFlag(ObserveOnlyFlag);
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
            if (scene.name == TownSceneName) Ensure(scene);
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName) Ensure(scene);
        }

        private static void Ensure(Scene scene)
        {
            if (!Enabled || FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private static bool HasFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ReadArgumentValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return i + 1 < args.Length ? args[i + 1] : string.Empty;
                string prefix = flag + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return arg.Substring(prefix.Length);
            }

            return string.Empty;
        }

        private sealed class Runner : MonoBehaviour
        {
            private static readonly List<PlayerIdentity> Players = new List<PlayerIdentity>(8);

            private IEnumerator Start()
            {
                EnsureKeyboardDevice();
                DirectValidationTrace.Log("autoplay started discovery-route-unlock scene=" + SceneManager.GetActiveScene().name + " observeOnly=" + HasFlag(ObserveOnlyFlag));

                PlayerIdentity identity = null;
                yield return WaitForLocalInputPlayer(value => identity = value);
                if (identity == null)
                {
                    DirectValidationTrace.Log("autoplay failed: discovery-route-unlock local player not found");
                    yield break;
                }

                GameDataRegistry registry = null;
                yield return WaitForRegistry(value => registry = value);
                if (registry == null)
                {
                    DirectValidationTrace.Log("autoplay failed: discovery-route-unlock registry not ready");
                    yield break;
                }

                if (HasFlag(ObserveOnlyFlag))
                {
                    yield return CaptureAndLogObserveOnly(identity, registry);
                    yield break;
                }

                bool success = false;
                yield return RunPlayerFlow(identity, registry, value => success = value);
                ReleaseAllKeys();
                if (!success) yield break;
                yield return CaptureAndLogActionCompletion(identity, registry);
            }

            private static IEnumerator CaptureAndLogObserveOnly(PlayerIdentity identity, GameDataRegistry registry)
            {
                RefreshObjectiveJournal();
                yield return OpenObjectiveJournal();
                yield return ClickButtonByName("ObjectiveCategory_Goals", false);
                yield return null;
                LogEvidence("observe", identity, registry);
                string screenshotPath = ReadArgumentValue(ScreenshotFlag);
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    CaptureScreenshotEvidence(screenshotPath);
                    yield return null;
                    yield return null;
                }

                DirectValidationTrace.Log("autoplay completed discovery-route-unlock-observe player=" + identity.PlayerId);
            }

            private static IEnumerator CaptureAndLogActionCompletion(PlayerIdentity identity, GameDataRegistry registry)
            {
                RefreshObjectiveJournal();
                yield return OpenObjectiveJournal();
                yield return ClickButtonByName("ObjectiveCategory_Goals", false);
                yield return null;
                LogEvidence("action", identity, registry);
                string screenshotPath = ReadArgumentValue(ScreenshotFlag);
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    CaptureScreenshotEvidence(screenshotPath);
                    yield return null;
                    yield return null;
                }

                DirectValidationTrace.Log("autoplay completed discovery-route-unlock player=" + identity.PlayerId);
            }

            private static IEnumerator RunPlayerFlow(PlayerIdentity identity, GameDataRegistry registry, Action<bool> assign)
            {
                var clue = FindClue(registry, ClueId);
                var worldFlag = FindWorldState(registry, WorldStateId);
                if (clue == null || worldFlag == null)
                {
                    DirectValidationTrace.Log("autoplay failed: discovery route data missing clue=" + (clue != null) + " worldState=" + (worldFlag != null));
                    assign(false);
                    yield break;
                }

                var clueObject = SpawnClueInteractor(identity, clue);
                DirectValidationTrace.Log("autoplay move target=DiscoveryRouteUnlock_ClueSource player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, clueObject.transform.position + new Vector3(0.15f, 0f, 0f), 0.35f, 12f);
                yield return PressKey(Key.E);
                bool clueSeen = false;
                yield return WaitForClueSeen(identity, value => clueSeen = value);
                if (!clueSeen)
                {
                    DirectValidationTrace.Log("autoplay failed: clue not seen " + ClueSnapshot(identity));
                    assign(false);
                    yield break;
                }

                var interpretationObject = SpawnInterpretationInteractor(identity, registry);
                DirectValidationTrace.Log("autoplay move target=DiscoveryRouteUnlock_InterpretationPoint player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, interpretationObject.transform.position + new Vector3(0.15f, 0f, 0f), 0.35f, 12f);
                yield return PressKey(Key.E);
                Button choiceButton = null;
                yield return WaitForButtonContaining(InterpretationButtonText, 5f, value => choiceButton = value);
                if (choiceButton == null)
                {
                    if (!IsInterpretationCompleted(identity))
                    {
                        DirectValidationTrace.Log("autoplay failed: interpretation choice button missing text=" + InterpretationButtonText + " " + InterpretationSnapshot(identity));
                        assign(false);
                        yield break;
                    }

                    DirectValidationTrace.Log("autoplay interpretation already completed player=" + identity.PlayerId + " " + InterpretationSnapshot(identity));
                }
                else
                {
                    yield return ClickButton(choiceButton);
                }

                bool worldActive = false;
                yield return WaitForWorldState(identity, worldFlag, value => worldActive = value);
                if (!worldActive)
                {
                    DirectValidationTrace.Log("autoplay failed: world state did not activate " + WorldStateSnapshot(identity, registry));
                    assign(false);
                    yield break;
                }

                GameObject dailyPlanBoard = null;
                yield return WaitForSceneObject(DailyPlanBoardName, 12f, value => dailyPlanBoard = value);
                if (dailyPlanBoard == null)
                {
                    DirectValidationTrace.Log("autoplay failed: Daily Plan board not found");
                    assign(false);
                    yield break;
                }

                yield return MovePlayerTo(identity.transform, dailyPlanBoard.transform.position, 0.35f, 12f);
                yield return PressKey(Key.E);
                Button routeButton = null;
                yield return WaitForButtonContaining(RouteLabel, 6f, value => routeButton = value);
                if (routeButton == null)
                {
                    DirectValidationTrace.Log("autoplay failed: unlocked route button missing " + DescribeDailyPlanUiState());
                    assign(false);
                    yield break;
                }

                yield return ClickButton(routeButton);
                DailyPlanRouteDefinition route = null;
                yield return WaitForSelectedRoute(identity, registry, value => route = value);
                if (route == null)
                {
                    DirectValidationTrace.Log("autoplay failed: selected route not resolved " + DailyPlanSnapshot(identity));
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay selected discovery route player=" + identity.PlayerId + " route=" + Safe(route.Id) + " " + DailyPlanSnapshot(identity));
                yield return TrackSelectedRoute(route);
                DirectValidationTrace.Log("autoplay tracked discovery route player=" + identity.PlayerId + " " + JournalSnapshot(route) + " " + HudSnapshot(route));
                assign(true);
            }

            private static GameObject SpawnClueInteractor(PlayerIdentity identity, DiscoveryClueDefinition clue)
            {
                DestroyNamed("DiscoveryRouteUnlock_ClueSource");
                var go = new GameObject("DiscoveryRouteUnlock_ClueSource", typeof(BoxCollider2D));
                go.transform.position = identity.transform.position + new Vector3(1.2f, 0f, 0f);
                var collider = go.GetComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(0.8f, 0.8f);
                var interactor = go.AddComponent<DiscoveryClueInteractor>();
                interactor.Bind(clue, FindSource(clue, DiscoveryClueSourceKind.NpcDialogue), null, DiscoveryClueCompletionKind.ObjectInteracted, string.Empty);
                return go;
            }

            private static GameObject SpawnInterpretationInteractor(PlayerIdentity identity, GameDataRegistry registry)
            {
                DestroyNamed("DiscoveryRouteUnlock_InterpretationPoint");
                var go = new GameObject("DiscoveryRouteUnlock_InterpretationPoint", typeof(BoxCollider2D));
                go.transform.position = identity.transform.position + new Vector3(1.2f, 0f, 0f);
                var collider = go.GetComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(0.8f, 0.8f);
                var type = FindType(ClueInterpretationInteractorTypeName);
                if (type != null)
                {
                    var component = go.AddComponent(type);
                    var bind = type.GetMethod("Bind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    bind?.Invoke(component, new object[] { registry.ClueInterpretations, ClueInterpretationSourceKind.ObjectInteraction, "object.workbench", null });
                }

                return go;
            }

            private static IEnumerator TrackSelectedRoute(DailyPlanRouteDefinition route)
            {
                RefreshObjectiveJournal();
                yield return OpenObjectiveJournal();
                yield return ClickButtonByName("ObjectiveCategory_Goals", true);
                Button row = null;
                yield return WaitForButtonContaining(route.ObjectiveTitleKey, 4f, value => row = value);
                if (row == null)
                {
                    DirectValidationTrace.Log("autoplay objective row unavailable title=" + Safe(route.ObjectiveTitleKey));
                    yield break;
                }

                yield return ClickButton(row);
                yield return ClickButtonByName("ObjectiveTrackButton", true);
            }

            private static IEnumerator WaitForLocalInputPlayer(Action<PlayerIdentity> assign)
            {
                PlayerIdentity identity = null;
                float elapsed = 0f;
                while (identity == null && elapsed < 15f)
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
                    if (identity == null) continue;
                    var controller = identity.GetComponent<PlayerController>();
                    var router = identity.GetComponent<PlayerInteractionRouter>();
                    if (controller != null && controller.enabled && router != null && router.enabled) return identity;
                }

                return null;
            }

            private static IEnumerator WaitForRegistry(Action<GameDataRegistry> assign)
            {
                GameDataRegistry registry = null;
                float elapsed = 0f;
                while (registry == null && elapsed < 15f)
                {
                    registry = Managers.Managers.Data != null ? Managers.Managers.Data.Registry : null;
                    if (registry == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(registry);
            }

            private static IEnumerator WaitForSceneObject(string objectName, float timeoutSeconds, Action<GameObject> assign)
            {
                GameObject found = null;
                float elapsed = 0f;
                while (found == null && elapsed < timeoutSeconds)
                {
                    found = FindSceneObjectByName(SceneManager.GetActiveScene(), objectName);
                    if (found == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(found);
            }

            private static IEnumerator WaitForButtonContaining(string text, float timeoutSeconds, Action<Button> assign)
            {
                Button found = null;
                float elapsed = 0f;
                while (found == null && elapsed < timeoutSeconds)
                {
                    found = FindActiveButtonContaining(text);
                    if (found == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(found);
            }

            private static IEnumerator WaitForClueSeen(PlayerIdentity identity, Action<bool> assign)
            {
                bool seen = false;
                float elapsed = 0f;
                while (!seen && elapsed < 5f)
                {
                    seen = DiscoveryClueProgressPersistence.LoadOrCreate(ResolveSaveSlot(identity), ResolvePlayerId(identity)).GetRecord(ClueId).Seen;
                    if (!seen)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(seen);
            }

            private static IEnumerator WaitForWorldState(PlayerIdentity identity, WorldStateFlagDefinition flag, Action<bool> assign)
            {
                bool active = false;
                float elapsed = 0f;
                while (!active && elapsed < 5f)
                {
                    active = WorldStateProgressPersistence.LoadOrCreate(ResolveSaveSlot(identity), ResolvePlayerId(identity)).IsActive(flag);
                    if (!active)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(active);
            }

            private static IEnumerator WaitForSelectedRoute(PlayerIdentity identity, GameDataRegistry registry, Action<DailyPlanRouteDefinition> assign)
            {
                DailyPlanRouteDefinition route = null;
                float elapsed = 0f;
                while (route == null && elapsed < 5f)
                {
                    route = FindSelectedRoute(identity, registry);
                    if (route == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(route);
            }

            private static Button FindActiveButtonContaining(string text)
            {
                if (string.IsNullOrEmpty(text)) return null;
                var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    if (button == null || !button.interactable || !button.gameObject.activeInHierarchy) continue;
                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null && !string.IsNullOrEmpty(label.text) && label.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return button;
                }

                return null;
            }

            private static IEnumerator ClickButtonByName(string name, bool required)
            {
                Button button = null;
                float elapsed = 0f;
                while (button == null && elapsed < 4f)
                {
                    button = FindActiveButtonByName(name);
                    if (button == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (button != null) yield return ClickButton(button);
                else if (required) DirectValidationTrace.Log("autoplay failed: button not found name=" + name);
            }

            private static Button FindActiveButtonByName(string name)
            {
                var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    if (button != null && button.interactable && button.gameObject.activeInHierarchy && button.gameObject.name == name) return button;
                }

                return null;
            }

            private static IEnumerator ClickButton(Button button)
            {
                string buttonName = button.gameObject.name;
                if (EventSystem.current != null)
                {
                    ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                }
                else
                {
                    button.onClick.Invoke();
                }

                DirectValidationTrace.Log("autoplay clicked button=" + buttonName);
                yield return null;
            }

            private static IEnumerator OpenObjectiveJournal()
            {
                var panel = FindBehaviourByTypeName(ObjectiveJournalPanelTypeName);
                if (panel != null && ReadBool(panel, "IsVisible")) yield break;
                yield return PressKey(Key.Tab);
                float elapsed = 0f;
                while (elapsed < 4f)
                {
                    panel = FindBehaviourByTypeName(ObjectiveJournalPanelTypeName);
                    if (panel != null && ReadBool(panel, "IsVisible")) yield break;
                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }
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

            private static void RefreshObjectiveJournal()
            {
                var provider = FindBehaviourByTypeName(ObjectiveJournalRuntimeProviderTypeName);
                var method = provider != null ? provider.GetType().GetMethod("RefreshNow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
                method?.Invoke(provider, Array.Empty<object>());
            }

            private static DiscoveryClueDefinition FindClue(GameDataRegistry registry, string id)
            {
                if (registry == null || registry.DiscoveryClues == null) return null;
                for (int i = 0; i < registry.DiscoveryClues.Length; i++) if (registry.DiscoveryClues[i] != null && registry.DiscoveryClues[i].Id == id) return registry.DiscoveryClues[i];
                return null;
            }

            private static WorldStateFlagDefinition FindWorldState(GameDataRegistry registry, string id)
            {
                if (registry == null || registry.WorldStateFlags == null) return null;
                for (int i = 0; i < registry.WorldStateFlags.Length; i++) if (registry.WorldStateFlags[i] != null && registry.WorldStateFlags[i].Id == id) return registry.WorldStateFlags[i];
                return null;
            }

            private static DiscoveryClueSourceDefinition FindSource(DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind)
            {
                if (clue == null || clue.Sources == null) return null;
                for (int i = 0; i < clue.Sources.Count; i++) if (clue.Sources[i] != null && clue.Sources[i].Kind == kind) return clue.Sources[i];
                return null;
            }

            private static DailyPlanRouteDefinition FindSelectedRoute(PlayerIdentity identity, GameDataRegistry registry)
            {
                var progress = GetDailyPlanProgress(identity);
                if (progress == null || registry == null || string.IsNullOrEmpty(progress.SelectedRouteId)) return null;
                return new DailyPlanRouteRunner().FindRoute(registry.DailyPlanRouteSets, progress.SelectedRouteId);
            }

            private static DailyPlanProgress GetDailyPlanProgress(PlayerIdentity identity)
            {
                var component = identity != null ? identity.GetComponent<DailyPlanProgressComponent>() : null;
                var student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                return component != null ? component.EnsureProgress(student != null ? student.EnsureProgress() : null) : null;
            }

            private static string ResolveSaveSlot(PlayerIdentity identity)
            {
                var progress = GetDailyPlanProgress(identity);
                if (progress != null && !string.IsNullOrEmpty(progress.SaveSlot)) return progress.SaveSlot;
                var student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var studentProgress = student != null ? student.EnsureProgress() : null;
                return studentProgress != null && !string.IsNullOrEmpty(studentProgress.SaveSlot) ? studentProgress.SaveSlot : "default";
            }

            private static string ResolvePlayerId(PlayerIdentity identity)
            {
                if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
                var student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var progress = student != null ? student.EnsureProgress() : null;
                return progress != null && !string.IsNullOrEmpty(progress.PlayerId) ? progress.PlayerId : PlayerIdentity.DefaultPlayerId;
            }

            private static string ClueSnapshot(PlayerIdentity identity)
            {
                var progress = DiscoveryClueProgressPersistence.LoadOrCreate(ResolveSaveSlot(identity), ResolvePlayerId(identity));
                var record = progress.GetRecord(ClueId);
                return "clue=id:" + ClueId + ",seen:" + BoolText(record.Seen) + ",completed:" + BoolText(record.Completed) + ",records:" + progress.ToSaveData().Records.Length;
            }

            private static bool IsInterpretationCompleted(PlayerIdentity identity)
            {
                var progress = ClueInterpretationProgressPersistence.LoadOrCreate(ResolveSaveSlot(identity), ResolvePlayerId(identity));
                var data = progress.ToSaveData();
                for (int i = 0; i < data.Records.Length; i++) if (data.Records[i] != null && data.Records[i].Completed) return true;
                return false;
            }

            private static string InterpretationSnapshot(PlayerIdentity identity)
            {
                var progress = ClueInterpretationProgressPersistence.LoadOrCreate(ResolveSaveSlot(identity), ResolvePlayerId(identity));
                var data = progress.ToSaveData();
                return "interpretation=completed:" + BoolText(IsInterpretationCompleted(identity)) + ",records:" + data.Records.Length;
            }

            private static string WorldStateSnapshot(PlayerIdentity identity, GameDataRegistry registry)
            {
                var flag = FindWorldState(registry, WorldStateId);
                bool active = WorldStateProgressPersistence.LoadOrCreate(ResolveSaveSlot(identity), ResolvePlayerId(identity)).IsActive(flag);
                return "worldState=id:" + WorldStateId + ",active:" + BoolText(active);
            }

            private static string DailyPlanSnapshot(PlayerIdentity identity)
            {
                var progress = GetDailyPlanProgress(identity);
                if (progress == null) return "dailyPlan=missing";
                return "dailyPlan=day:" + progress.CurrentDay + ",selected:" + Safe(progress.SelectedRouteId) + ",completed:" + Safe(progress.CompletedRouteId);
            }

            private static string JournalSnapshot(DailyPlanRouteDefinition route)
            {
                var panel = FindBehaviourByTypeName(ObjectiveJournalPanelTypeName);
                if (panel == null) return "journal=missing";
                string text = ReadString(panel, "VisibleText");
                string routeTitle = route != null ? route.ObjectiveTitleKey : RouteObjectiveTitle;
                int occurrences = CountOccurrences(text, routeTitle);
                return "journal=visible:" + BoolText(ReadBool(panel, "IsVisible")) + ",category:" + Safe(ReadString(panel, "SelectedCategoryIdForTests")) + ",rows:" + ReadInt(panel, "VisibleRowCountForTests") + ",routeRows:" + occurrences + ",duplicateRows:" + Mathf.Max(0, occurrences - 1) + ",text:" + Safe(Compact(text));
            }

            private static string HudSnapshot(DailyPlanRouteDefinition route)
            {
                var behaviours = FindBehavioursByTypeName(TrackedObjectiveHudTypeName);
                int visible = 0;
                string text = string.Empty;
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] == null) continue;
                    if (ReadBool(behaviours[i], "IsVisible"))
                    {
                        visible++;
                        text += (text.Length == 0 ? string.Empty : " | ") + ReadString(behaviours[i], "VisibleText");
                    }
                }

                string routeTitle = route != null ? route.ObjectiveTitleKey : RouteObjectiveTitle;
                int routeTextCount = CountOccurrences(text, routeTitle);
                return "hud=instances:" + behaviours.Length + ",visible:" + visible + ",routeTextCount:" + routeTextCount + ",duplicates:" + Mathf.Max(0, behaviours.Length - 1) + ",text:" + Safe(Compact(text));
            }

            private static string SaveSnapshot(PlayerIdentity identity)
            {
                string saveSlot = ResolveSaveSlot(identity);
                string playerId = ResolvePlayerId(identity);
                string cluePath = Path.Combine(Application.persistentDataPath, saveSlot, DiscoveryClueProgressPersistence.FileNameFor(playerId));
                string interpretationPath = Path.Combine(Application.persistentDataPath, saveSlot, ClueInterpretationProgressPersistence.FileNameFor(playerId));
                string worldPath = Path.Combine(Application.persistentDataPath, saveSlot, WorldStateProgressPersistence.FileNameFor(playerId));
                string dailyPath = Path.Combine(Application.persistentDataPath, saveSlot, DailyPlanProgressPersistence.FileNameFor(playerId));
                return "save=clue:" + File.Exists(cluePath) + ",interpretation:" + File.Exists(interpretationPath) + ",worldState:" + File.Exists(worldPath) + ",dailyPlan:" + File.Exists(dailyPath);
            }

            private static void LogEvidence(string phase, PlayerIdentity identity, GameDataRegistry registry)
            {
                var route = FindSelectedRoute(identity, registry) ?? new DailyPlanRouteRunner().FindRoute(registry.DailyPlanRouteSets, RouteId);
                DirectValidationTrace.Log("autoplay discovery snapshot phase=" + phase + " player=" + identity.PlayerId + " " + ClueSnapshot(identity));
                DirectValidationTrace.Log("autoplay interpretation snapshot phase=" + phase + " player=" + identity.PlayerId + " " + InterpretationSnapshot(identity));
                DirectValidationTrace.Log("autoplay world snapshot phase=" + phase + " player=" + identity.PlayerId + " " + WorldStateSnapshot(identity, registry));
                DirectValidationTrace.Log("autoplay daily snapshot phase=" + phase + " player=" + identity.PlayerId + " " + DailyPlanSnapshot(identity));
                DirectValidationTrace.Log("autoplay journal snapshot phase=" + phase + " player=" + identity.PlayerId + " " + JournalSnapshot(route));
                DirectValidationTrace.Log("autoplay hud snapshot phase=" + phase + " player=" + identity.PlayerId + " " + HudSnapshot(route));
                DirectValidationTrace.Log("autoplay save snapshot phase=" + phase + " player=" + identity.PlayerId + " " + SaveSnapshot(identity));
            }

            private static string DescribeDailyPlanUiState()
            {
                var panel = FindBehaviourByTypeName(DailyPlanPanelTypeName);
                if (panel == null) return "dailyPlanUi=panel:missing";
                int rowCount = ReadInt(panel, "RouteRowCountForTests");
                string summary = "dailyPlanUi=panel:present,active:" + panel.gameObject.activeInHierarchy + ",rows:" + rowCount + ",empty:" + Safe(ReadString(panel, "EmptyStateTextForTests"));
                for (int i = 0; i < rowCount; i++)
                {
                    var button = InvokeGetRouteButtonForTests(panel, i);
                    var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
                    summary += " row" + i + "=" + (button == null ? "missing" : "active:" + button.gameObject.activeInHierarchy + ",interactable:" + button.interactable + ",text:" + Safe(text != null ? text.text : string.Empty));
                }

                return summary;
            }

            private static MonoBehaviour FindBehaviourByTypeName(string typeName)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var behaviour = behaviours[i];
                    if (behaviour != null && behaviour.GetType().FullName == typeName) return behaviour;
                }

                return null;
            }

            private static MonoBehaviour[] FindBehavioursByTypeName(string typeName)
            {
                var matches = new List<MonoBehaviour>();
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var behaviour = behaviours[i];
                    if (behaviour != null && behaviour.GetType().FullName == typeName) matches.Add(behaviour);
                }

                return matches.ToArray();
            }

            private static Type FindType(string fullName)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var type = assemblies[i].GetType(fullName);
                    if (type != null) return type;
                }

                DirectValidationTrace.Log("autoplay failed: type not found " + fullName);
                return null;
            }

            private static int ReadInt(MonoBehaviour target, string propertyName)
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null && property.GetValue(target) is int value ? value : 0;
            }

            private static bool ReadBool(MonoBehaviour target, string propertyName)
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null && property.GetValue(target) is bool value && value;
            }

            private static string ReadString(MonoBehaviour target, string propertyName)
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null ? property.GetValue(target) as string ?? string.Empty : string.Empty;
            }

            private static Button InvokeGetRouteButtonForTests(MonoBehaviour target, int index)
            {
                var method = target.GetType().GetMethod("GetRouteButtonForTests", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                return method != null ? method.Invoke(target, new object[] { index }) as Button : null;
            }

            private static int CountOccurrences(string text, string value)
            {
                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) return 0;
                int count = 0;
                int index = 0;
                while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    index += value.Length;
                }

                return count;
            }

            private static string Compact(string value)
            {
                return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            }

            private static string Safe(string value)
            {
                return string.IsNullOrEmpty(value) ? "empty" : value;
            }

            private static string BoolText(bool value)
            {
                return value ? "true" : "false";
            }

            private static void CaptureScreenshotEvidence(string screenshotPath)
            {
                string directory = Path.GetDirectoryName(screenshotPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                ScreenCapture.CaptureScreenshot(screenshotPath);
                DirectValidationTrace.Log("autoplay screenshot path=" + screenshotPath);
            }

            private static Keyboard EnsureKeyboardDevice()
            {
                var keyboard = Keyboard.current;
                if (keyboard != null) return keyboard;
                keyboard = InputSystem.AddDevice<Keyboard>();
                DirectValidationTrace.Log("autoplay created keyboard device for discovery-route-unlock batchmode input");
                return keyboard;
            }

            private static void HoldKey(Key key)
            {
                var keyboard = EnsureKeyboardDevice();
                if (keyboard == null) return;
                var state = new KeyboardState();
                state.Set(key, true);
                InputSystem.QueueStateEvent(keyboard, state);
            }

            private static void ReleaseAllKeys()
            {
                var keyboard = EnsureKeyboardDevice();
                if (keyboard == null) return;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }

            private static void CollectPlayers(Scene scene, List<PlayerIdentity> players)
            {
                players.Clear();
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++) roots[i].GetComponentsInChildren(false, players);
            }

            private static void DestroyNamed(string name)
            {
                var existing = GameObject.Find(name);
                if (existing != null) UnityEngine.Object.Destroy(existing);
            }
        }

        private static GameObject FindSceneObjectByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindChildByName(roots[i].transform, objectName);
                if (match != null) return match.gameObject;
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChildByName(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == name) return roots[i];
            return null;
        }
    }
}

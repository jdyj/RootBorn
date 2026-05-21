using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Game.Common
{
    public static class DirectValidationDailyPlanCompletionAutoplayInstaller
    {
        private const string Flag = "-directValidationAutoplayDailyPlanCompletion";
        private const string ObserveOnlyFlag = "-directValidationAutoplayDailyPlanCompletionObserveOnly";
        private const string ScreenshotFlag = "-directValidationScreenshot";
        private const string RunnerName = "[DirectValidationDailyPlanCompletionAutoplay]";
        private const string TownSceneName = "Town";
        private const string DailyPlanBoardName = "DailyPlanBoard";
        private const string DailyPlanRouteRowName = "DailyPlanRouteRow";
        private const string DayEndObjectName = "StudentDayEndBoard";
        private const string DailyPlanPanelTypeName = "Rootborn.UI.StudentLife.DailyPlanPanel";
        private const string LocationPanelTypeName = "Rootborn.UI.StudentLife.LocationIdentityPanel";
        private const string DayResultPanelTypeName = "Rootborn.UI.StudentLife.StudentDayResultPanel";
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
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }

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
                DirectValidationTrace.Log("autoplay started daily-plan-completion scene=" + SceneManager.GetActiveScene().name + " observeOnly=" + HasFlag(ObserveOnlyFlag));

                PlayerIdentity identity = null;
                yield return WaitForLocalInputPlayer(value => identity = value);
                if (identity == null)
                {
                    DirectValidationTrace.Log("autoplay failed: daily-plan-completion local player not found");
                    yield break;
                }

                StudentLifeProgressComponent student = null;
                DailyPlanProgressComponent dailyPlan = null;
                yield return WaitForProgress(identity, value => student = value, value => dailyPlan = value);
                if (student == null || dailyPlan == null)
                {
                    DirectValidationTrace.Log("autoplay failed: daily-plan-completion progress not ready player=" + identity.PlayerId);
                    yield break;
                }

                GameDataRegistry registry = null;
                yield return WaitForRegistry(value => registry = value);
                if (registry == null)
                {
                    DirectValidationTrace.Log("autoplay failed: daily-plan-completion registry not ready");
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

                DirectValidationTrace.Log("autoplay completed daily-plan-completion-observe player=" + identity.PlayerId);
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

                DirectValidationTrace.Log("autoplay completed daily-plan-completion player=" + identity.PlayerId);
            }

            private static IEnumerator RunPlayerFlow(PlayerIdentity identity, GameDataRegistry registry, Action<bool> assign)
            {
                GameObject dailyPlanBoard = null;
                yield return WaitForSceneObject(DailyPlanBoardName, 12f, value => dailyPlanBoard = value);
                if (dailyPlanBoard == null)
                {
                    DirectValidationTrace.Log("autoplay failed: Daily Plan board not found");
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + DailyPlanBoardName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, dailyPlanBoard.transform.position, 0.35f, 12f);
                yield return PressKey(Key.E);

                Button routeButton = null;
                yield return WaitForRouteButton(value => routeButton = value);
                if (routeButton == null)
                {
                    DirectValidationTrace.Log("autoplay failed: Daily Plan route button not selectable " + DescribeDailyPlanUiState());
                    assign(false);
                    yield break;
                }

                yield return ClickButton(routeButton);
                DailyPlanRouteDefinition route = null;
                yield return WaitForSelectedRoute(identity, registry, value => route = value);
                if (route == null)
                {
                    DirectValidationTrace.Log("autoplay failed: selected Daily Plan route not resolved " + DailyPlanSnapshot(identity));
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay selected daily route player=" + identity.PlayerId + " route=" + Safe(route.Id) + " " + DailyPlanSnapshot(identity));

                yield return TrackSelectedRoute(route);
                DirectValidationTrace.Log("autoplay tracked selected route player=" + identity.PlayerId + " " + JournalSnapshot(route) + " " + HudSnapshot(route));
                yield return CloseObjectiveJournalIfVisible();

                MatchingActivityTarget target = default;
                yield return WaitForMatchingLocation(route, value => target = value);
                if (target.Interactor == null || target.Activity == null || target.ActivityIndex < 0)
                {
                    DirectValidationTrace.Log("autoplay failed: matching location activity not found route=" + Safe(route.Id));
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + target.Interactor.gameObject.name + " activityIndex=" + target.ActivityIndex + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, target.Interactor.transform.position, 0.35f, 12f);
                yield return PressKey(Key.E);
                yield return WaitForPanelOpen(LocationPanelTypeName, 4f);
                Button activityButton = null;
                yield return WaitForActiveButtonByName("LocationActivityButton_" + target.ActivityIndex, 5f, value => activityButton = value);
                if (activityButton == null)
                {
                    DirectValidationTrace.Log("autoplay failed: matching location activity button missing index=" + target.ActivityIndex + " activity=" + Safe(target.Activity.Id));
                    assign(false);
                    yield break;
                }

                yield return ClickButton(activityButton);
                bool completed = false;
                yield return WaitForDailyPlanCompletion(identity, value => completed = value);
                if (!completed)
                {
                    DirectValidationTrace.Log("autoplay failed: selected Daily Plan route did not finish after activity " + DailyPlanSnapshot(identity));
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay completed route activity player=" + identity.PlayerId + " activity=" + Safe(target.Activity.Id) + " " + DailyPlanSnapshot(identity));
                RefreshObjectiveJournal();
                yield return null;
                DirectValidationTrace.Log("autoplay post-completion objective player=" + identity.PlayerId + " " + JournalSnapshot(route) + " " + HudSnapshot(route));

                GameObject dayEndObject = null;
                yield return WaitForSceneObject(DayEndObjectName, 12f, value => dayEndObject = value);
                if (dayEndObject == null)
                {
                    DirectValidationTrace.Log("autoplay failed: day-end object not found");
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + DayEndObjectName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, dayEndObject.transform.position, 0.35f, 12f);
                yield return PressKey(Key.E);
                yield return WaitForPanelOpen(DayResultPanelTypeName, 4f);
                DirectValidationTrace.Log("autoplay day result player=" + identity.PlayerId + " " + ResultSnapshot());
                assign(true);
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

            private static IEnumerator WaitForProgress(PlayerIdentity identity, Action<StudentLifeProgressComponent> assignStudent, Action<DailyPlanProgressComponent> assignDaily)
            {
                StudentLifeProgressComponent student = null;
                DailyPlanProgressComponent daily = null;
                float elapsed = 0f;
                while ((student == null || daily == null) && elapsed < 12f)
                {
                    student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                    daily = identity != null ? identity.GetComponent<DailyPlanProgressComponent>() : null;
                    if (student == null || daily == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assignStudent(student);
                assignDaily(daily);
            }

            private static IEnumerator WaitForRegistry(Action<GameDataRegistry> assign)
            {
                GameDataRegistry registry = null;
                float elapsed = 0f;
                while (registry == null && elapsed < 12f)
                {
                    if (Rootborn.Game.Managers.Managers.Data != null && Rootborn.Game.Managers.Managers.Data.Registry != null)
                    {
                        registry = Rootborn.Game.Managers.Managers.Data.Registry;
                        break;
                    }

                    GameDataRegistryProvider.TryGet(out registry);
                    if (registry == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(registry);
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

            private static IEnumerator WaitForRouteButton(Action<Button> assign)
            {
                Button found = null;
                float elapsed = 0f;
                while (found == null && elapsed < 8f)
                {
                    var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var button = buttons[i];
                        if (button != null && button.interactable && button.gameObject.activeInHierarchy && button.gameObject.name == DailyPlanRouteRowName)
                        {
                            found = button;
                            break;
                        }
                    }

                    if (found == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(found);
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

            private static IEnumerator WaitForDailyPlanCompletion(PlayerIdentity identity, Action<bool> assign)
            {
                bool completed = false;
                float elapsed = 0f;
                while (!completed && elapsed < 5f)
                {
                    var progress = GetDailyPlanProgress(identity);
                    completed = progress != null && !string.IsNullOrEmpty(progress.CompletedRouteId) && progress.AppliedCompletionKeys.Count == 1;
                    if (!completed)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(completed);
            }

            private static IEnumerator WaitForMatchingLocation(DailyPlanRouteDefinition route, Action<MatchingActivityTarget> assign)
            {
                MatchingActivityTarget target = default;
                float elapsed = 0f;
                while (target.Interactor == null && elapsed < 12f)
                {
                    target = FindMatchingLocation(route);
                    if (target.Interactor == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(target);
            }

            private static MatchingActivityTarget FindMatchingLocation(DailyPlanRouteDefinition route)
            {
                var interactors = UnityEngine.Object.FindObjectsByType<LocationActivityInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < interactors.Length; i++)
                {
                    var interactor = interactors[i];
                    var identity = interactor != null ? interactor.Identity : null;
                    if (identity == null) continue;
                    for (int activityIndex = 0; activityIndex < identity.AvailableActivities.Count; activityIndex++)
                    {
                        var activity = identity.AvailableActivities[activityIndex];
                        if (activity != null && route != null && route.Matches(activity)) return new MatchingActivityTarget(interactor, activity, activityIndex);
                    }
                }

                return default;
            }

            private static IEnumerator WaitForPanelOpen(string typeName, float timeoutSeconds)
            {
                float elapsed = 0f;
                while (elapsed < timeoutSeconds)
                {
                    var panel = FindBehaviourByTypeName(typeName);
                    if (panel != null && ReadBool(panel, "IsOpen")) yield break;
                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }
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

            private static IEnumerator CloseObjectiveJournalIfVisible()
            {
                var panel = FindBehaviourByTypeName(ObjectiveJournalPanelTypeName);
                if (panel == null || !ReadBool(panel, "IsVisible")) yield break;
                yield return PressKey(Key.Tab);
                yield return null;
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


                    private static IEnumerator WaitForActiveButtonByName(string name, float timeoutSeconds, Action<Button> assign)
                    {
                        Button button = null;
                        float elapsed = 0f;
                        while (button == null && elapsed < timeoutSeconds)
                        {
                            button = FindActiveButtonByName(name);
                            if (button == null)
                            {
                                elapsed += UnityEngine.Time.unscaledDeltaTime;
                                yield return null;
                            }
                        }

                        assign(button);
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
                    if (label != null && !string.IsNullOrEmpty(label.text) && label.text.Contains(text)) return button;
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

            private static string DailyPlanSnapshot(PlayerIdentity identity)
            {
                var progress = GetDailyPlanProgress(identity);
                if (progress == null) return "dailyPlan=missing";
                return "dailyPlan=day:" + progress.CurrentDay + ",selected:" + Safe(progress.SelectedRouteId) + ",completed:" + Safe(progress.CompletedRouteId) + ",matchedActivity:" + Safe(progress.MatchedActivityId) + ",completionKeys:" + progress.AppliedCompletionKeys.Count;
            }

            private static string ResultSnapshot()
            {
                var panel = FindBehaviourByTypeName(DayResultPanelTypeName);
                if (panel == null) return "result=missing";
                return "result=open:" + ReadBool(panel, "IsOpen") + ",text:" + Safe(Compact(ReadString(panel, "ResultsTextForTests")));
            }

            private static string JournalSnapshot(DailyPlanRouteDefinition route)
            {
                var panel = FindBehaviourByTypeName(ObjectiveJournalPanelTypeName);
                if (panel == null) return "journal=missing";
                string text = ReadString(panel, "VisibleText");
                string routeTitle = route != null ? route.ObjectiveTitleKey : string.Empty;
                int occurrences = CountOccurrences(text, routeTitle);
                return "journal=visible:" + ReadBool(panel, "IsVisible") + ",category:" + Safe(ReadString(panel, "SelectedCategoryIdForTests")) + ",rows:" + ReadInt(panel, "VisibleRowCountForTests") + ",routeRows:" + occurrences + ",duplicateRows:" + Mathf.Max(0, occurrences - 1) + ",text:" + Safe(Compact(text));
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

                string routeTitle = route != null ? route.ObjectiveTitleKey : string.Empty;
                int routeTextCount = CountOccurrences(text, routeTitle);
                return "hud=instances:" + behaviours.Length + ",visible:" + visible + ",routeTextCount:" + routeTextCount + ",duplicates:" + Mathf.Max(0, behaviours.Length - 1) + ",text:" + Safe(Compact(text));
            }

            private static string SaveSnapshot(PlayerIdentity identity)
            {
                var progress = GetDailyPlanProgress(identity);
                if (progress == null) return "save=missing";
                string fileName = DailyPlanProgressPersistence.FileNameFor(progress.PlayerId);
                string path = Path.Combine(Application.persistentDataPath, progress.SaveSlot, fileName);
                if (!File.Exists(path)) return "save=missing,path:" + path;
                string json = File.ReadAllText(path);
                return "save=present,path:" + path + ",selected:" + json.Contains(progress.SelectedRouteId) + ",completed:" + json.Contains(progress.CompletedRouteId) + ",matchedActivity:" + json.Contains(progress.MatchedActivityId);
            }

            private static void LogEvidence(string phase, PlayerIdentity identity, GameDataRegistry registry)
            {
                var route = FindSelectedRoute(identity, registry);
                DirectValidationTrace.Log("autoplay daily snapshot phase=" + phase + " player=" + identity.PlayerId + " " + DailyPlanSnapshot(identity));
                DirectValidationTrace.Log("autoplay result snapshot phase=" + phase + " player=" + identity.PlayerId + " " + ResultSnapshot());
                DirectValidationTrace.Log("autoplay journal snapshot phase=" + phase + " player=" + identity.PlayerId + " " + JournalSnapshot(route));
                DirectValidationTrace.Log("autoplay hud snapshot phase=" + phase + " player=" + identity.PlayerId + " " + HudSnapshot(route));
                DirectValidationTrace.Log("autoplay save snapshot phase=" + phase + " player=" + identity.PlayerId + " " + SaveSnapshot(identity));
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
                DirectValidationTrace.Log("autoplay created keyboard device for daily-plan-completion batchmode input");
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

            private readonly struct MatchingActivityTarget
            {
                public readonly LocationActivityInteractor Interactor;
                public readonly LocationActivityDefinition Activity;
                public readonly int ActivityIndex;

                public MatchingActivityTarget(LocationActivityInteractor interactor, LocationActivityDefinition activity, int activityIndex)
                {
                    Interactor = interactor;
                    Activity = activity;
                    ActivityIndex = activityIndex;
                }
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
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name) return roots[i];
            }

            return null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Rootborn.Game.Crafting;
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
    public static class DirectValidationResourceRecipeActivityAutoplayInstaller
    {
        private const string Flag = "-directValidationAutoplayResourceRecipeActivity";
        private const string ObserveOnlyFlag = "-directValidationAutoplayResourceRecipeActivityObserveOnly";
        private const string ScreenshotFlag = "-directValidationScreenshot";
        private const string RunnerName = "[DirectValidationResourceRecipeActivityAutoplay]";
        private const string TownSceneName = "Town";
        private const string LocationObjectName = "LocationVisit_location_town_square";
        private const string ActivityButtonName = "LocationActivityButton_0";
        private const string ActivityId = "location.activity.square-help-request";
        private const string RecipeId = "recipe.town.field-notes";
        private const string RecipeTitle = "recipe.town.field-notes.name";
        private const string ResourceId = "Rock";
        private const string ObjectiveJournalPanelTypeName = "Rootborn.UI.Objectives.ObjectiveJournalPanel";
        private const string ObjectiveJournalRuntimeProviderTypeName = "Rootborn.UI.Objectives.ObjectiveJournalRuntimeProvider";
        private const string TrackedObjectiveHudTypeName = "Rootborn.UI.Objectives.TrackedObjectiveHud";
        private const string LocationIdentityPanelTypeName = "Rootborn.UI.StudentLife.LocationIdentityPanel";
        private const string RecipeRevealPassMarker = "recipe=recipe.town.field-notes revealed:true";
        private const string ResourceSeenPassMarker = "resource=Rock seen:true";
        private const string JournalCleanMarker = "journal=resource-recipe duplicateRows:0";
        private const string HudCleanMarker = "hud=tracked duplicates:0";
        private const string SavePassMarker = "save=resource-recipe exists:true";
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
                DirectValidationTrace.Log("autoplay started resource-recipe-activity scene=" + SceneManager.GetActiveScene().name + " observeOnly=" + HasFlag(ObserveOnlyFlag));

                PlayerIdentity identity = null;
                yield return WaitForLocalInputPlayer(value => identity = value);
                if (identity == null)
                {
                    DirectValidationTrace.Log("autoplay failed: resource-recipe-activity local player not found");
                    yield break;
                }

                GameDataRegistry registry = null;
                yield return WaitForRegistry(value => registry = value);
                if (registry == null || FindRecipe(registry) == null)
                {
                    DirectValidationTrace.Log("autoplay failed: resource-recipe-activity recipe missing id=" + RecipeId);
                    yield break;
                }

                if (HasFlag(ObserveOnlyFlag))
                {
                    bool restored = false;
                    yield return WaitForRecipeRevealed(identity, value => restored = value);
                    if (!restored)
                    {
                        DirectValidationTrace.Log("autoplay failed: resource-recipe-activity observe state missing " + ProgressSnapshot(identity));
                        yield break;
                    }

                    yield return CaptureAndLogCompletion("observe", identity, "autoplay completed resource-recipe-activity-observe");
                    yield break;
                }

                bool success = false;
                yield return RunPlayerFlow(identity, value => success = value);
                ReleaseAllKeys();
                if (!success) yield break;
                yield return CaptureAndLogCompletion("action", identity, "autoplay completed resource-recipe-activity");
            }

            private static IEnumerator RunPlayerFlow(PlayerIdentity identity, Action<bool> assign)
            {
                GameObject square = null;
                yield return WaitForSceneObject(LocationObjectName, 15f, value => square = value);
                if (square == null)
                {
                    DirectValidationTrace.Log("autoplay failed: resource-recipe-activity town square object missing");
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=ResourceRecipe_TownSquare player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, square.transform.position + new Vector3(-1.45f, 0f, 0f), 0.35f, 12f);
                yield return PressKey(Key.E);

                UnityEngine.Object panel = null;
                yield return WaitForPanelOpen(value => panel = value);
                if (panel == null)
                {
                    DirectValidationTrace.Log("autoplay failed: resource-recipe-activity location panel did not open");
                    assign(false);
                    yield break;
                }

                Button activityButton = null;
                yield return WaitForButtonByName(ActivityButtonName, 5f, value => activityButton = value);
                if (activityButton == null)
                {
                    DirectValidationTrace.Log("autoplay failed: resource-recipe-activity activity button missing");
                    assign(false);
                    yield break;
                }

                yield return ClickButton(activityButton);
                bool revealed = false;
                yield return WaitForRecipeRevealed(identity, value => revealed = value);
                if (!revealed)
                {
                    DirectValidationTrace.Log("autoplay failed: resource-recipe-activity recipe not revealed " + ProgressSnapshot(identity));
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay applied resource recipe activity player=" + identity.PlayerId + " activity=" + ActivityId + " " + ProgressSnapshot(identity));
                assign(true);
            }

            private static IEnumerator CaptureAndLogCompletion(string phase, PlayerIdentity identity, string completion)
            {
                RefreshObjectiveJournal();
                yield return OpenObjectiveJournal();
                yield return ClickButtonByName("ObjectiveCategory_Hints", false);
                yield return null;
                LogEvidence(phase, identity);
                string screenshotPath = ReadArgumentValue(ScreenshotFlag);
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    CaptureScreenshotEvidence(screenshotPath);
                    yield return null;
                    yield return null;
                }

                DirectValidationTrace.Log(completion + " player=" + identity.PlayerId);
            }

            private static void LogEvidence(string phase, PlayerIdentity identity)
            {
                DirectValidationTrace.Log("autoplay resource-recipe snapshot phase=" + phase + " player=" + identity.PlayerId + " " + ProgressSnapshot(identity));
                DirectValidationTrace.Log("autoplay journal snapshot phase=" + phase + " player=" + identity.PlayerId + " " + JournalSnapshot());
                DirectValidationTrace.Log("autoplay hud snapshot phase=" + phase + " player=" + identity.PlayerId + " " + HudSnapshot());
                DirectValidationTrace.Log("autoplay save snapshot phase=" + phase + " player=" + identity.PlayerId + " " + SaveSnapshot(identity));
            }

            private static string ProgressSnapshot(PlayerIdentity identity)
            {
                var progress = LoadProgress(identity);
                bool recipeRevealed = progress != null && Contains(progress.RevealedRecipeIds, RecipeId);
                bool resourceSeen = progress != null && Contains(progress.SeenResourceIds, ResourceId);
                return "recipe=" + RecipeId + " revealed:" + Lower(recipeRevealed) + " resource=" + ResourceId + " seen:" + Lower(resourceSeen);
            }

            private static string JournalSnapshot()
            {
                var panel = FindBehaviourByTypeName(ObjectiveJournalPanelTypeName);
                int rows = 0;
                if (panel is Component component)
                {
                    var buttons = component.GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var button = buttons[i];
                        if (button == null || !button.name.StartsWith("ObjectiveRow_", StringComparison.Ordinal)) continue;
                        var text = button.GetComponentInChildren<Text>(true);
                        if (text != null && !string.IsNullOrEmpty(text.text) && text.text.Contains(RecipeTitle)) rows++;
                    }
                }

                return "journal=resource-recipe duplicateRows:" + Mathf.Max(0, rows - 1) + " rows:" + rows;
            }

            private static string HudSnapshot()
            {
                var behaviours = FindBehavioursByTypeName(TrackedObjectiveHudTypeName);
                int visible = 0;
                for (int i = 0; i < behaviours.Length; i++) if (behaviours[i] != null && ReadBool(behaviours[i], "IsVisible")) visible++;
                return "hud=tracked duplicates:" + Mathf.Max(0, behaviours.Length - 1) + " instances:" + behaviours.Length + " visible:" + visible;
            }

            private static string SaveSnapshot(PlayerIdentity identity)
            {
                string saveSlot = ResolveSaveSlot(identity);
                string playerId = ResolvePlayerId(identity);
                string fileName = string.IsNullOrEmpty(playerId) || playerId == "player" ? "resource-recipe-progress.json" : "resource-recipe-progress-" + SanitizeFileName(playerId) + ".json";
                string path = Path.Combine(Application.persistentDataPath, "saves", saveSlot, fileName);
                return "save=resource-recipe exists:" + Lower(File.Exists(path));
            }

            private static IEnumerator WaitForRecipeRevealed(PlayerIdentity identity, Action<bool> assign)
            {
                float elapsed = 0f;
                while (elapsed < 6f)
                {
                    var progress = LoadProgress(identity);
                    if (progress != null && Contains(progress.RevealedRecipeIds, RecipeId))
                    {
                        assign(true);
                        yield break;
                    }

                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }

                assign(false);
            }

            private static ResourceRecipeProgress LoadProgress(PlayerIdentity identity)
            {
                var student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var studentProgress = student != null ? student.EnsureProgress() : null;
                string saveSlot = studentProgress != null ? studentProgress.SaveSlot : ResolveSaveSlot(identity);
                string playerId = ResolvePlayerId(identity);
                return ResourceRecipeProgressPersistence.LoadOrCreate(saveSlot, playerId);
            }

            private static RecipeDefinition FindRecipe(GameDataRegistry registry)
            {
                if (registry == null || registry.Recipes == null) return null;
                for (int i = 0; i < registry.Recipes.Length; i++) if (registry.Recipes[i] != null && registry.Recipes[i].Id == RecipeId) return registry.Recipes[i];
                return null;
            }

            private static IEnumerator WaitForPanelOpen(Action<UnityEngine.Object> assign)
            {
                float elapsed = 0f;
                while (elapsed < 5f)
                {
                    var panel = FindBehaviourByTypeName(LocationIdentityPanelTypeName);
                    if (panel != null && ReadBool(panel, "IsOpen"))
                    {
                        assign(panel);
                        yield break;
                    }

                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }

                assign(null);
            }

            private static IEnumerator WaitForButtonByName(string name, float timeoutSeconds, Action<Button> assign)
            {
                float elapsed = 0f;
                while (elapsed < timeoutSeconds)
                {
                    var button = FindButtonByName(name, true);
                    if (button != null)
                    {
                        assign(button);
                        yield break;
                    }

                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }

                assign(null);
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

            private static IEnumerator WaitForSceneObject(string name, float timeoutSeconds, Action<GameObject> assign)
            {
                GameObject found = null;
                float elapsed = 0f;
                while (found == null && elapsed < timeoutSeconds)
                {
                    found = GameObject.Find(name);
                    if (found == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(found);
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

            private static IEnumerator ClickButtonByName(string buttonName, bool requireActive)
            {
                var button = FindButtonByName(buttonName, requireActive);
                if (button != null) yield return ClickButton(button);
            }

            private static IEnumerator ClickButton(Button button)
            {
                if (button == null) yield break;
                if (EventSystem.current == null)
                {
                    var go = new GameObject("EventSystem", typeof(EventSystem));
                    go.AddComponent<PassiveInputModule>();
                }

                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                yield return null;
            }

            private static Button FindButtonByName(string buttonName, bool requireActive)
            {
                var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    if (button == null || button.name != buttonName) continue;
                    if (requireActive && !button.gameObject.activeInHierarchy) continue;
                    if (!button.interactable) continue;
                    return button;
                }

                return null;
            }

            private static void RefreshObjectiveJournal()
            {
                var provider = FindBehaviourByTypeName(ObjectiveJournalRuntimeProviderTypeName);
                var method = provider != null ? provider.GetType().GetMethod("RefreshNow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
                method?.Invoke(provider, Array.Empty<object>());
            }

            private static MonoBehaviour FindBehaviourByTypeName(string typeName)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++) if (behaviours[i] != null && behaviours[i].GetType().FullName == typeName) return behaviours[i];
                return null;
            }

            private static MonoBehaviour[] FindBehavioursByTypeName(string typeName)
            {
                var matches = new List<MonoBehaviour>();
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++) if (behaviours[i] != null && behaviours[i].GetType().FullName == typeName) matches.Add(behaviours[i]);
                return matches.ToArray();
            }

            private static bool ReadBool(object target, string propertyName)
            {
                var property = target != null ? target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
                return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(target);
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
                DirectValidationTrace.Log("autoplay created keyboard device for resource-recipe-activity batchmode input");
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

            private static bool Contains(string[] values, string expected)
            {
                if (values == null || string.IsNullOrEmpty(expected)) return false;
                for (int i = 0; i < values.Length; i++) if (values[i] == expected) return true;
                return false;
            }

            private static string ResolveSaveSlot(PlayerIdentity identity)
            {
                var student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var progress = student != null ? student.EnsureProgress() : null;
                return progress != null && !string.IsNullOrEmpty(progress.SaveSlot) ? progress.SaveSlot : "default";
            }

            private static string ResolvePlayerId(PlayerIdentity identity)
            {
                if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
                return PlayerIdentity.DefaultPlayerId;
            }

            private static string SanitizeFileName(string value)
            {
                var chars = value.ToCharArray();
                for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
                return new string(chars);
            }

            private static string Lower(bool value) => value ? "true" : "false";
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i] != null && roots[i].name == rootName) return roots[i];
            return null;
        }
    }
}

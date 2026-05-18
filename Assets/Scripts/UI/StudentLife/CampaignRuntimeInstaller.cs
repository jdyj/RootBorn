using System;
using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class CampaignRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[CampaignRuntimeInstaller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        public static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName) EnsureScene(scene);
        }

        public static void EnsureScene(Scene scene)
        {
            if (scene.name != TownSceneName || FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        public static CampaignDayResultSummary BuildDayEndSummary(GameObject player, StudentLifeProgress progress, bool endedCurrentDay)
        {
            var component = player != null ? player.GetComponent<CampaignProgressComponent>() : null;
            if (component == null || component.Progress == null || component.Campaign == null) return default;
            if (progress != null) component.Progress.SetCurrentDayNumber(progress.CurrentDay);
            var result = endedCurrentDay ? ApplyProgress(progress, component, "campaign-result-day-" + (progress != null ? progress.CurrentDay.ToString() : "0")) : new CampaignApplyResult(Array.Empty<string>());
            if (endedCurrentDay) CampaignProgressPersistence.Save(component.Progress);
            return CampaignSummaryBuilder.BuildDayResultSummary(component.Campaign, component.Progress, result);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureScene(scene);
        }

        private sealed class Runner : MonoBehaviour
        {
            private Scene _scene;
            private CampaignHudPanel _hud;
            private CampaignProgressComponent _campaignProgress;
            private StudentLifeProgressComponent _student;
            private CampaignDefinition _campaign;

            private IEnumerator Start()
            {
                _scene = SceneManager.GetActiveScene();
                float elapsed = 0f;
                GameObject player = null;
                while (elapsed < 5f)
                {
                    player = FindRoot(_scene, "Player");
                    if (player != null && player.GetComponent<StudentLifeProgressComponent>() != null) break;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                var canvas = EnsureCanvas(_scene);
                EnsureEventSystem(_scene);
                _hud = CampaignHudPanel.EnsureInScene(canvas);
                if (player != null) BindPlayer(player);
                Refresh();
            }

            private void OnEnable()
            {
                LocationActivityInteractor.OnAnyInteracted += HandleLocationActivity;
                NpcInteractor.OnAnyInteracted += HandleNpc;
                DailyEventPanel.OnAnyChoiceApplied += HandleDailyEventChoice;
            }

            private void OnDisable()
            {
                LocationActivityInteractor.OnAnyInteracted -= HandleLocationActivity;
                NpcInteractor.OnAnyInteracted -= HandleNpc;
                DailyEventPanel.OnAnyChoiceApplied -= HandleDailyEventChoice;
            }

            private void HandleLocationActivity(LocationActivityInteractor interactor, GameObject player)
            {
                BindPlayer(player);
                ApplyAndRefresh("campaign-location:" + (interactor != null && interactor.Identity != null && interactor.Identity.Location != null ? interactor.Identity.Location.Id : Time.frameCount.ToString()));
            }

            private void HandleNpc(NpcInteractor npc, StudentLifeProgress progress)
            {
                var player = FindRoot(_scene, "Player");
                BindPlayer(player);
                ApplyAndRefresh("campaign-npc:" + (npc != null && npc.Npc != null ? npc.Npc.Id : Time.frameCount.ToString()));
            }

            private void HandleDailyEventChoice(StudentLifeProgress progress)
            {
                var player = FindRoot(_scene, "Player");
                BindPlayer(player);
                ApplyAndRefresh("campaign-daily-event:" + (progress != null ? progress.CurrentDay.ToString() : Time.frameCount.ToString()));
            }

            private void BindPlayer(GameObject player)
            {
                if (player == null) return;
                _student = player.GetComponent<StudentLifeProgressComponent>();
                if (_student == null) return;
                var studentProgress = _student.EnsureProgress();
                _campaign = ResolveCampaign();
                if (_campaign == null) return;
                _campaignProgress = player.GetComponent<CampaignProgressComponent>();
                if (_campaignProgress == null) _campaignProgress = player.AddComponent<CampaignProgressComponent>();
                if (!CampaignProgressPersistence.TryLoad(_campaignProgress, _campaign, studentProgress)) _campaignProgress.EnsureProgress(_campaign, studentProgress);
                _campaignProgress.Progress.SetCurrentDayNumber(studentProgress.CurrentDay);
            }

            private void ApplyAndRefresh(string resultId)
            {
                if (_student == null || _campaignProgress == null) return;
                ApplyProgress(_student.EnsureProgress(), _campaignProgress, resultId);
                CampaignProgressPersistence.Save(_campaignProgress.Progress);
                Refresh();
            }

            private void Refresh()
            {
                if (_hud == null) return;
                if (_student != null && _campaignProgress != null) _campaignProgress.Progress.SetCurrentDayNumber(_student.EnsureProgress().CurrentDay);
                _hud.Refresh(CampaignSummaryBuilder.BuildHudSummary(_campaign, _campaignProgress != null ? _campaignProgress.Progress : null));
            }
        }

        private static CampaignApplyResult ApplyProgress(StudentLifeProgress studentProgress, CampaignProgressComponent component, string resultId)
        {
            if (component == null || component.Progress == null || component.Campaign == null) return new CampaignApplyResult(Array.Empty<string>());
            var context = new CampaignEvaluationContext(studentProgress, component.Progress, ResolveVisitedLocations(studentProgress, ResolveRegistry()), Array.Empty<string>());
            var runner = new CampaignRunner();
            runner.TryApplyProgress(component.Campaign, context, resultId, out var result);
            return result;
        }

        private static LocationDefinition[] ResolveVisitedLocations(StudentLifeProgress progress, GameDataRegistry registry)
        {
            if (progress == null || registry == null || registry.Locations == null) return Array.Empty<LocationDefinition>();
            var visits = new LocationVisitProgress(progress);
            var visitedIds = visits.VisitedLocationIds;
            var locations = new List<LocationDefinition>();
            for (int i = 0; i < registry.Locations.Length; i++)
            {
                var location = registry.Locations[i];
                if (location == null) continue;
                for (int j = 0; j < visitedIds.Length; j++) if (visitedIds[j] == location.Id && !locations.Contains(location)) locations.Add(location);
            }
            return locations.ToArray();
        }

        private static CampaignDefinition ResolveCampaign()
        {
            var registry = ResolveRegistry();
            var testCampaigns = GameDataRegistryCampaignExtensions.GetCampaigns(registry);
            if (testCampaigns.Length > 0) return testCampaigns[0];
            var registeredCampaigns = registry != null ? registry.Campaigns : null;
            return registeredCampaigns != null && registeredCampaigns.Length > 0 ? registeredCampaigns[0] : null;
        }

        private static GameDataRegistry ResolveRegistry()
        {
            if (Managers.Data != null && Managers.Data.Registry != null) return Managers.Data.Registry;
            return Resources.Load<GameDataRegistry>("GameDataRegistry");
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var canvas = FindComponentInScene<Canvas>(scene);
            if (canvas != null) return canvas;
            var canvasGo = new GameObject("[CampaignCanvas]", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 82;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            var eventSystem = FindComponentInScene<EventSystem>(scene);
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                UiInputModuleInstaller.AddPreferredInputModule(go);
                SceneManager.MoveGameObjectToScene(go, scene);
                return;
            }
            if (eventSystem.GetComponent<BaseInputModule>() == null) UiInputModuleInstaller.AddPreferredInputModule(eventSystem.gameObject);
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null) return match;
            }
            return null;
        }
    }
}

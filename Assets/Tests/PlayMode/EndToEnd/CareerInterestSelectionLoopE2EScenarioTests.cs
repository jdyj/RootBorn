using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class CareerInterestSelectionLoopE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-career-interest-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CAREER_INTEREST_E2E_001_013_SaveSlotMovementLocationUiInterestSelectionQuestResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var data = CareerInterestE2EData.Create();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "CAREER-INTEREST-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            CareerInterestRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            data.AttachReward(FindRegistryRewardItem());
            var runtime = FindRuntime();
            var candidateComponent = runtime.Player.GetComponent<CareerCandidateProgressComponent>();
            if (candidateComponent == null) candidateComponent = runtime.Player.AddComponent<CareerCandidateProgressComponent>();
            candidateComponent.Bind(data.Candidates, data.Hints);
            var interestComponent = runtime.Player.GetComponent<CareerInterestProgressComponent>();
            if (interestComponent == null) interestComponent = runtime.Player.AddComponent<CareerInterestProgressComponent>();
            interestComponent.Bind(data.Interests);
            var interestPanel = CareerInterestPanel.EnsureInScene(EnsureCanvas());
            var interestHud = CareerInterestHudWidget.EnsureInScene(EnsureCanvas());
            var openButton = MakeCareerInterestButton(EnsureCanvas(), () => interestPanel.Show(data.Interests, interestComponent, candidateComponent.Progress, runtime.Student.Progress, runtime.Student.Progress.CurrentDay));
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "CAREER-INTEREST-E2E-002 failed: library location object missing.");
            var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(locationPanel, "CAREER-INTEREST-E2E-003 failed: location panel missing.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "CAREER-INTEREST-E2E-002 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "CAREER-INTEREST-E2E-003 failed: location prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationPanel(2f, "CAREER-INTEREST-E2E-003 failed: location UI did not open from actual input.");
            yield return ClickButton(FindButton(locationPanel.transform, "LocationActivityButton_0"));
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, locationPanel.LastResult.Kind, "CAREER-INTEREST-E2E-004 failed: location activity button did not apply.");
            Assert.IsTrue(candidateComponent.LastResult.Applied, "CAREER-INTEREST-E2E-004 failed: career candidate hint was not applied from actual location result.");
            Assert.AreEqual(CareerCandidateState.Revealed, data.Learning.GetState(candidateComponent.Progress), "CAREER-INTEREST-E2E-004 failed: outside-school route did not reveal selectable candidate.");

            yield return ClickButton(openButton);
            Assert.IsTrue(interestPanel.IsOpen, "CAREER-INTEREST-E2E-005 failed: career interest UI did not open from actual UI button.");
            yield return ClickButton(FindButton(interestPanel.transform, "InterestButton_0"));
            Assert.AreEqual("interest.learning", interestComponent.Progress.CurrentInterestId, "CAREER-INTEREST-E2E-006 failed: interest was not selected through UI button.");
            yield return ClickButton(FindButton(interestPanel.transform, "InterestButton_1"));
            Assert.AreEqual("interest.learning", interestComponent.Progress.CurrentInterestId, "CAREER-INTEREST-E2E-015 failed: same-day change limit allowed a second interest selection through UI.");
            AssertPanelContains(interestPanel.transform, "ChangedToday", "CAREER-INTEREST-E2E-015 failed: interest panel did not show same-day change limit state.");
            yield return ClickButton(FindButton(interestPanel.transform, "InterestButton_0"));
            AssertPanelContains(interestPanel.transform, "Selected", "CAREER-INTEREST-E2E-006 failed: interest panel did not show selected state.");
            AssertPanelContains(interestPanel.transform, "action.visit-library", "CAREER-INTEREST-E2E-007 failed: interest panel did not show recommended next action.");
            interestHud.Refresh(data.Interests, interestComponent.Progress, candidateComponent.Progress, runtime.Student.Progress, runtime.Student.Progress.CurrentDay);
            AssertPanelContains(interestHud.transform, "interest.learning", "CAREER-INTEREST-E2E-007 failed: interest HUD omitted selected interest.");
            AssertPanelContains(interestHud.transform, "action.visit-library", "CAREER-INTEREST-E2E-007 failed: interest HUD omitted recommended next action.");

            string interestFile = Path.Combine(_saveRoot, TestSlotId, "career-interest-progress.json");
            Assert.IsTrue(File.Exists(interestFile), "CAREER-INTEREST-E2E-008 failed: interest selection did not save through slot file.");
            StringAssert.Contains("interest.learning", File.ReadAllText(interestFile), "CAREER-INTEREST-E2E-008 failed: interest save file omitted selected interest.");

            var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(questPanel, "CAREER-INTEREST-E2E-011 failed: quest log UI missing after interest selection.");
            Assert.AreEqual(QuestState.Active, questPanel.QuestLog.GetState(data.InterestQuest), "CAREER-INTEREST-E2E-011 failed: linked interest quest was not accepted by the interest UI selection.");
            AssertPanelContains(questPanel.transform, data.InterestQuest.DisplayNameKey, "CAREER-INTEREST-E2E-011 failed: linked interest quest did not appear in QuestLog UI.");
            AssertPanelContains(questPanel.transform, "0 / 2", "CAREER-INTEREST-E2E-011 failed: linked interest quest did not start at zero progress in QuestLog UI.");

            yield return ClickButton(FindButton(interestPanel.transform, "CloseButton"));
            yield return ClickButton(FindButton(locationPanel.transform, "LocationActivityButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, locationPanel.LastResult.Kind, "CAREER-INTEREST-E2E-012 failed: actual location button did not apply after interest selection.");
            Assert.AreEqual(QuestState.Active, questPanel.QuestLog.GetState(data.InterestQuest), "CAREER-INTEREST-E2E-012 failed: partial linked quest progress should remain active.");
            Assert.AreEqual(1, questPanel.QuestLog.GetObjectiveCount(data.InterestQuest, 0), "CAREER-INTEREST-E2E-012 failed: actual location activity did not advance linked quest to 1/2.");
            AssertPanelContains(questPanel.transform, "1 / 2", "CAREER-INTEREST-E2E-012 failed: QuestLog UI did not show partial linked quest progress.");
            string questFile = Path.Combine(_saveRoot, TestSlotId, "quest-log.json");
            Assert.IsTrue(File.Exists(questFile), "CAREER-INTEREST-E2E-011 failed: linked interest quest did not save to quest-log.json.");
            StringAssert.Contains(data.InterestQuest.Id, File.ReadAllText(questFile), "CAREER-INTEREST-E2E-011 failed: quest save omitted linked interest quest id.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "CAREER-INTEREST-E2E-009 failed: movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "CAREER-INTEREST-E2E-009 failed: day result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, "interest.learning", "CAREER-INTEREST-E2E-009 failed: result UI omitted selected interest.");
            AssertPanelContains(resultPanel.transform, "action.visit-library", "CAREER-INTEREST-E2E-009 failed: result UI omitted interest recommendation.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "CAREER-INTEREST-E2E-010 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            CareerInterestRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            var reloadedCandidate = reloaded.Player.GetComponent<CareerCandidateProgressComponent>();
            if (reloadedCandidate == null) reloadedCandidate = reloaded.Player.AddComponent<CareerCandidateProgressComponent>();
            reloadedCandidate.Bind(data.Candidates, data.Hints);
            var reloadedInterest = reloaded.Player.GetComponent<CareerInterestProgressComponent>();
            if (reloadedInterest == null) reloadedInterest = reloaded.Player.AddComponent<CareerInterestProgressComponent>();
            reloadedInterest.Bind(data.Interests);
            Assert.AreEqual("interest.learning", reloadedInterest.Progress.CurrentInterestId, "CAREER-INTEREST-E2E-010 failed: selected interest did not survive save/load.");

            var reloadedInterestPanel = CareerInterestPanel.EnsureInScene(EnsureCanvas());
            var reloadedOpenButton = MakeCareerInterestButton(EnsureCanvas(), () => reloadedInterestPanel.Show(data.Interests, reloadedInterest, reloadedCandidate.Progress, reloaded.Student.Progress, reloaded.Student.Progress.CurrentDay));
            yield return ClickButton(reloadedOpenButton);
            var reloadedQuestPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            Assert.AreEqual(QuestState.Active, reloadedQuestPanel.QuestLog.GetState(data.InterestQuest), "CAREER-INTEREST-E2E-011 failed: linked interest quest accepted state did not restore after opening interest UI post-reload.");
            Assert.AreEqual(1, reloadedQuestPanel.QuestLog.GetObjectiveCount(data.InterestQuest, 0), "CAREER-INTEREST-E2E-012 failed: linked interest quest partial progress did not restore from save.");
            AssertPanelContains(reloadedQuestPanel.transform, data.InterestQuest.DisplayNameKey, "CAREER-INTEREST-E2E-011 failed: reloaded QuestLog UI omitted linked interest quest.");
            AssertPanelContains(reloadedQuestPanel.transform, "1 / 2", "CAREER-INTEREST-E2E-012 failed: reloaded QuestLog UI omitted saved partial quest progress.");
            yield return ClickButton(FindButton(reloadedInterestPanel.transform, "CloseButton"));

            var reloadedLocationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            var reloadedLibrary = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "CAREER-INTEREST-E2E-012 failed: reloaded library location object missing.");
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloadedLibrary.transform.position, 0.25f, "CAREER-INTEREST-E2E-012 failed: reloaded player could not reach library.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationPanel(2f, "CAREER-INTEREST-E2E-012 failed: reloaded location UI did not open through input.");
            yield return ClickButton(FindButton(reloadedLocationPanel.transform, "LocationActivityButton_0"));
            yield return null;
            Assert.AreEqual(QuestState.Completed, reloadedQuestPanel.QuestLog.GetState(data.InterestQuest), "CAREER-INTEREST-E2E-013 failed: continued actual location activity did not complete linked quest after reload.");
            Assert.AreEqual(2, reloadedQuestPanel.QuestLog.GetObjectiveCount(data.InterestQuest, 0), "CAREER-INTEREST-E2E-013 failed: completed linked quest did not preserve required count.");
            AssertPanelContains(reloadedQuestPanel.transform, "2 / 2", "CAREER-INTEREST-E2E-013 failed: QuestLog UI did not show completion-ready linked quest.");

            int rewardBeforeClaim = reloaded.Inventory.Inventory.CountOf(data.RewardItem);
            yield return ClickButton(FindButton(reloadedQuestPanel.transform, "ClaimButton"));
            yield return null;
            int rewardAfterClaim = reloaded.Inventory.Inventory.CountOf(data.RewardItem);
            Assert.AreEqual(rewardBeforeClaim + data.Reward.Count, rewardAfterClaim, "CAREER-INTEREST-E2E-013 failed: linked interest quest reward was not paid exactly once.");
            Assert.AreEqual(QuestState.RewardClaimed, reloadedQuestPanel.QuestLog.GetState(data.InterestQuest), "CAREER-INTEREST-E2E-013 failed: linked interest reward claim state did not update.");
            AssertPanelContains(reloadedQuestPanel.transform, QuestState.RewardClaimed.ToString(), "CAREER-INTEREST-E2E-013 failed: QuestLog UI did not show reward claimed state.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "CAREER-INTEREST-E2E-013 failed: SaveSlot Load after claim did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            CareerInterestRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var claimedReload = FindRuntime();
            var claimedInterest = claimedReload.Player.GetComponent<CareerInterestProgressComponent>();
            if (claimedInterest == null) claimedInterest = claimedReload.Player.AddComponent<CareerInterestProgressComponent>();
            claimedInterest.Bind(data.Interests);
            var claimedCandidate = claimedReload.Player.GetComponent<CareerCandidateProgressComponent>();
            if (claimedCandidate == null) claimedCandidate = claimedReload.Player.AddComponent<CareerCandidateProgressComponent>();
            claimedCandidate.Bind(data.Candidates, data.Hints);
            var claimedPanel = CareerInterestPanel.EnsureInScene(EnsureCanvas());
            var claimedOpenButton = MakeCareerInterestButton(EnsureCanvas(), () => claimedPanel.Show(data.Interests, claimedInterest, claimedCandidate.Progress, claimedReload.Student.Progress, claimedReload.Student.Progress.CurrentDay));
            yield return ClickButton(claimedOpenButton);
            var claimedQuestPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            Assert.AreEqual(QuestState.RewardClaimed, claimedQuestPanel.QuestLog.GetState(data.InterestQuest), "CAREER-INTEREST-E2E-013 failed: linked quest reward claimed state did not survive second reload.");
            Assert.AreEqual(rewardAfterClaim, claimedReload.Inventory.Inventory.CountOf(data.RewardItem), "CAREER-INTEREST-E2E-013 failed: reward inventory count changed after reload.");
            Assert.IsFalse(claimedQuestPanel.QuestLog.CanClaimReward(data.InterestQuest, new RewardRuntimeContext(claimedQuestPanel.QuestLog, claimedReload.Inventory.Inventory, null, null, claimedReload.Student.Progress)), "CAREER-INTEREST-E2E-013 failed: linked quest reward was claimable again after reload.");

            int historyCount = claimedInterest.Progress.SelectionHistoryIds.Length;
            var duplicate = claimedInterest.Progress.TrySelect(data.LearningInterest, null, claimedReload.Student.Progress, claimedReload.Student.Progress.CurrentDay, "duplicate-select");
            CareerInterestProgressPersistence.Save(claimedInterest);
            Assert.IsFalse(duplicate.Applied, "CAREER-INTEREST-E2E-010 failed: duplicate same-interest selection changed state.");
            Assert.AreEqual(historyCount, claimedInterest.Progress.SelectionHistoryIds.Length, "CAREER-INTEREST-E2E-010 failed: duplicate same-interest selection added history.");
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && library != null && library.GetComponent<LocationActivityInteractor>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && panel != null && questPanel != null && questPanel.QuestLog != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("CAREER-INTEREST-E2E runtime did not expose player, library, day-end point, QuestLog, and UI within timeout.");
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(dayEnd);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), player.GetComponent<PlayerInventory>(), dayEnd.GetComponent<StudentDayEndInteractor>());
        }

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static IEnumerator WaitForLocationPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 480 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure);
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            Press(keyboard.eKey);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Release(keyboard.eKey);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            float elapsed = 0f;
            while (!op.isDone && elapsed < 10f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(op.isDone, "Scene load timed out: " + sceneName);
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "Save slot UI card missing: " + cardName);
            return FindButton(card.transform, buttonName);
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            Assert.IsNotNull(child, "UI button missing: " + name);
            var button = child.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(message + " Expected '" + expected + "' in text: " + string.Join(" | ", all));
        }

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null) return canvas;
            var go = new GameObject("CareerInterestE2ECanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return go.GetComponent<Canvas>();
        }

        private static Button MakeCareerInterestButton(Canvas canvas, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("CareerInterestOpenButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -76f);
            rt.sizeDelta = new Vector2(220f, 42f);
            go.GetComponent<Image>().color = Color.white;
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Interest";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }

        private static ItemDefinition FindRegistryRewardItem()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            Assert.IsNotNull(registry, "CAREER-INTEREST-E2E-013 setup failed: runtime registry missing.");
            Assert.IsNotNull(registry.Items, "CAREER-INTEREST-E2E-013 setup failed: registry items missing.");
            for (int i = 0; i < registry.Items.Length; i++)
            {
                if (registry.Items[i] != null) return registry.Items[i];
            }
            Assert.Fail("CAREER-INTEREST-E2E-013 setup failed: registry has no reward item.");
            return null;
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<PassiveInputModule>();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("CareerInterestE2ECanvas");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) Object.DestroyImmediate(transform.gameObject);
            }
        }

        private sealed class CareerInterestE2EData
        {
            public CareerCandidateDefinition Learning;
            public CareerCandidateDefinition[] Candidates;
            public CareerHintDefinition[] Hints;
            public CareerInterestDefinition LearningInterest;
            public CareerInterestDefinition ExplorationInterest;
            public CareerInterestDefinition[] Interests;
            public QuestDefinition InterestQuest;
            public LocationActivityQuestObjective Objective;
            public ItemQuestReward Reward;
            public ItemDefinition RewardItem;

            public void AttachReward(ItemDefinition item)
            {
                Assert.IsNotNull(item, "CAREER-INTEREST-E2E-013 setup failed: reward item missing.");
                RewardItem = item;
                Reward = ScriptableObject.CreateInstance<ItemQuestReward>();
                SetPrivateField(Reward, "_item", item);
                SetPrivateField(Reward, "_count", 1);
                InterestQuest.ConfigureForRuntime("quest.interest.learning", "quest.interest.learning", "desc.quest.interest.learning", new QuestObjectiveBase[] { Objective }, new QuestRewardBase[] { Reward }, null);
            }

            public static CareerInterestE2EData Create()
            {
                var libraryRoute = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
                libraryRoute.ConfigureForTests("route.library", "route.library", "desc.route.library", null, null, new[] { "action.visit-library" });
                var learning = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                learning.ConfigureForTests("candidate.learning", "candidate.learning", "desc.candidate.learning", new[] { libraryRoute }, null, new[] { "action.visit-library" }, null, null, null, 1, 2, 3);
                var source = ScriptableObject.CreateInstance<CareerResultLogHintSource>();
                source.ConfigureForTests("location-activity:");
                var hint = ScriptableObject.CreateInstance<CareerHintDefinition>();
                hint.ConfigureForTests("hint.learning.library", "hint.learning.library", learning, libraryRoute, "insight.learning.library", 1, new CareerHintSourceBase[] { source });
                var requirement = ScriptableObject.CreateInstance<CareerInterestCandidateStateRequirement>();
                requirement.ConfigureForTests(CareerCandidateState.Revealed);
                var changeRule = ScriptableObject.CreateInstance<CareerInterestDailyChangeLimitRule>();
                changeRule.ConfigureForTests(1, true);
                var recommendation = ScriptableObject.CreateInstance<CareerInterestStaticRecommendation>();
                recommendation.ConfigureForTests(new[] { "action.visit-library" });
                var objective = ScriptableObject.CreateInstance<LocationActivityQuestObjective>();
                objective.ConfigureForRuntime("objective.interest.location-activity", 2, null);
                var quest = ScriptableObject.CreateInstance<QuestDefinition>();
                quest.ConfigureForRuntime("quest.interest.learning", "quest.interest.learning", "desc.quest.interest.learning", new QuestObjectiveBase[] { objective }, null, null);
                var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
                interest.ConfigureForTests("interest.learning", "interest.learning", "desc.interest.learning", learning, null, null, new CareerInterestUnlockRequirementBase[] { requirement }, new CareerInterestSelectionRuleBase[] { changeRule }, new CareerInterestRecommendationBase[] { recommendation }, new CareerInterestRewardBase[0], new[] { quest });
                var alternateRule = ScriptableObject.CreateInstance<CareerInterestDailyChangeLimitRule>();
                alternateRule.ConfigureForTests(1, true);
                var alternate = ScriptableObject.CreateInstance<CareerInterestDefinition>();
                alternate.ConfigureForTests("interest.exploration", "interest.exploration", "desc.interest.exploration", learning, null, null, new CareerInterestUnlockRequirementBase[] { requirement }, new CareerInterestSelectionRuleBase[] { alternateRule }, new CareerInterestRecommendationBase[] { recommendation }, new CareerInterestRewardBase[0]);
                return new CareerInterestE2EData { Learning = learning, Candidates = new[] { learning }, Hints = new[] { hint }, LearningInterest = interest, ExplorationInterest = alternate, Interests = new[] { interest, alternate }, InterestQuest = quest, Objective = objective };
            }

            private static void SetPrivateField(object target, string fieldName, object value)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, target.GetType().Name + " missing field " + fieldName);
                field.SetValue(target, value);
            }
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly PlayerInventory Inventory;
            public readonly StudentDayEndInteractor DayEnd;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, PlayerInventory inventory, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                Inventory = inventory;
                DayEnd = dayEnd;
            }
        }
    }
}

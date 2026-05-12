using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
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
    public sealed class StudentRelationshipConditionDayLoopE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-relcond-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
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
        public IEnumerator RELCOND_E2E_001_011_PlayedRelationshipConditionDayLoopPersistsAndDoesNotDuplicate()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            var relationship = registry.Relationships[0];
            var status = registry.StudentConditionStatuses[0];

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "RELCOND-E2E-001 failed: 저장 슬롯 UI 새 게임이 Town에 진입하지 못했다.");
            yield return WaitForDayRuntime(10f);
            LogAssert.ignoreFailingMessages = true;

            var runtime = FindDayRuntime();
            AssertPanelNotOpen();
            int initialRelationship = runtime.Student.Progress.GetRelationshipValue(relationship);
            int initialStatus = runtime.Student.Progress.GetStatusValue(status);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Study.transform.position, 0.25f, "RELCOND-E2E-002 failed: NPC/도움 행동 지점까지 실제 키보드 이동 실패");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "RELCOND-E2E-002 failed: 상호작용 프롬프트 없음");
            yield return PressInteractKey(keyboard);
            yield return null;

            int changedRelationship = runtime.Student.Progress.GetRelationshipValue(relationship);
            int changedStatus = runtime.Student.Progress.GetStatusValue(status);
            Assert.Greater(changedRelationship, initialRelationship, "RELCOND-E2E-004 failed: 관계 변화 미기록");
            Assert.Greater(changedStatus, initialStatus, "RELCOND-E2E-004 failed: 컨디션 변화 미기록");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Study.Activity.Id + ":+" + relationship.Id + "=2", "RELCOND-E2E-004 failed: 오늘 관계 변화 로그 누락");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Study.Activity.Id + ":+" + status.Id + "=3", "RELCOND-E2E-004 failed: 오늘 컨디션 변화 로그 누락");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "RELCOND-E2E-005 failed: 하루 종료 지점까지 실제 키보드 이동 실패");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "RELCOND-E2E-005 failed: 하루 종료 프롬프트 없음");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "RELCOND-E2E-007 failed: 실제 하루 종료 입력 후 결과 UI가 열리지 않음");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, relationship.DisplayNameKey, "RELCOND-E2E-007 failed: 하루 결과 관계 요약 누락");
            AssertPanelContains(panel.transform, initialRelationship + " -> " + changedRelationship, "RELCOND-E2E-007 failed: 하루 결과 관계 이전/이후 값 누락");
            AssertPanelContains(panel.transform, status.DisplayNameKey, "RELCOND-E2E-007 failed: 컨디션 변화 미표시");
            AssertPanelContains(panel.transform, initialStatus + " -> " + changedStatus, "RELCOND-E2E-007 failed: 하루 결과 컨디션 이전/이후 값 누락");
            AssertPanelContains(panel.transform, "tomorrow", "RELCOND-E2E-007 failed: 다음 날 영향 표시 누락");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "RELCOND-E2E-010 failed: 저장 파일 없음");
            string resultReadyJson = File.ReadAllText(studentFile);
            StringAssert.Contains(relationship.Id, resultReadyJson, "RELCOND-E2E-010 failed: 저장 후 관계 복원 데이터 누락");
            StringAssert.Contains(status.Id, resultReadyJson, "RELCOND-E2E-010 failed: 저장 후 컨디션 복원 데이터 누락");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "RELCOND-E2E-010 failed: 저장 슬롯 UI 로드 후 Town 재진입 실패");
            yield return WaitForDayRuntime(10f);

            var reloaded = FindDayRuntime();
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationship), "RELCOND-E2E-010 failed: 저장 후 관계 복원 실패");
            Assert.AreEqual(changedStatus, reloaded.Student.Progress.GetStatusValue(status), "RELCOND-E2E-010 failed: 저장 후 컨디션 복원 실패");
            Assert.AreEqual(1, reloaded.Student.Progress.GetPreviousDayRelationshipDeltas().Length, "RELCOND-E2E-011 failed: 중복 관계 정산 발생");
            Assert.AreEqual(1, reloaded.Student.Progress.GetPreviousDayStatusDeltas().Length, "RELCOND-E2E-011 failed: 중복 컨디션 정산 발생");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "RELCOND-E2E-011 failed: 저장/로드 후 하루 결과 재확인 지점 이동 실패");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "RELCOND-E2E-011 failed: 저장/로드 후 결과 UI 재확인 실패");
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationship), "RELCOND-E2E-011 failed: 결과 재확인으로 관계 중복 적용");
            Assert.AreEqual(changedStatus, reloaded.Student.Progress.GetStatusValue(status), "RELCOND-E2E-011 failed: 결과 재확인으로 컨디션 중복 적용");

            panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(panel.transform, "NextDayButton"));
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Npc.transform.position, 0.25f, "RELCOND-E2E-009 failed: 2일차 NPC까지 실제 키보드 이동 실패");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(reloaded.DialoguePanel, 3f, "RELCOND-E2E-009 failed: 2일차 NPC 대화 UI 열림 실패");
            AssertPanelContains(reloaded.DialoguePanel.transform, "dialogue.guide.day2", "RELCOND-E2E-009 failed: 2일차 대사 변화 없음");
        }

        private static IEnumerator WaitForDayRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var study = GameObject.Find("StudyBasicsActivity");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    study != null && study.GetComponent<StudentLifeActivityInteractor>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && npc != null && dialoguePanel != null && panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("RELCOND-E2E setup failed: runtime objects not ready.");
        }

        private static DayRuntime FindDayRuntime()
        {
            var player = GameObject.Find("Player");
            var study = GameObject.Find("StudyBasicsActivity");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(player);
            Assert.IsNotNull(study);
            Assert.IsNotNull(dayEnd);
            Assert.IsNotNull(npc);
            Assert.IsNotNull(dialoguePanel);
            return new DayRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), study.GetComponent<StudentLifeActivityInteractor>(), dayEnd.GetComponent<StudentDayEndInteractor>(), npc, dialoguePanel);
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 360 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
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
            while (!op.isDone) yield return null;
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

        private static IEnumerator WaitForDialoguePanel(DialoguePanel panel, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
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
            Assert.IsFalse(string.IsNullOrEmpty(expected));
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new System.Collections.Generic.List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }

            Assert.Fail(message + " Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
        }

        private static void AssertPanelNotOpen()
        {
            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel == null || !panel.IsOpen);
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
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

        private readonly struct DayRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly StudentLifeActivityInteractor Study;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly NpcInteractor Npc;
            public readonly DialoguePanel DialoguePanel;

            public DayRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, StudentLifeActivityInteractor study, StudentDayEndInteractor dayEnd, NpcInteractor npc, DialoguePanel dialoguePanel)
            {
                Player = player;
                Router = router;
                Student = student;
                Study = study;
                DayEnd = dayEnd;
                Npc = npc;
                DialoguePanel = dialoguePanel;
            }
        }
    }
}

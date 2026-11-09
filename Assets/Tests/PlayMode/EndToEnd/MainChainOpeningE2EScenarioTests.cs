using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    /// <summary>
    /// MAIN-E2E-001: the whole school opening chain (BackToSchool → AfterSchoolTour → FirstPartTimeJob → MyDream →
    /// MyOwnSpace) from a new game, played the way a player does: keyboard walking, E at people and places, mouse-device
    /// clicks on UI, and the day board to sleep. The new game itself and the character creator are preparation.
    /// </summary>
    public sealed class MainChainOpeningE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string ChainId = "main-story.school-opening";
        private const string Evidence = "output/main-chain-e2e/";
        private const string F = "MAIN-E2E-001 failed:";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 8f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-main-chain-e2e", System.Guid.NewGuid().ToString("N"));
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
            Time.timeScale = 1f;
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
        [Timeout(1500000)]
        public IEnumerator MAIN_E2E_001_SchoolOpeningChainFromNewGameToMyRoom()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot("slot-0", "NewGameButton", "Town", F + " New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), F + " no game data.");
            var chain = System.Array.Find(registry.QuestChains, c => c != null && c.Id == ChainId);
            Assert.IsNotNull(chain, F + " the registry has no " + ChainId + ".");

            // Day 1 — BackToSchool: Mom at home starts the chain, then the day plan and the Teacher at school.
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return TalkTo(keyboard, "TownNpc_npc_first_guide", "House", "Mom");
            AssertStep(chain, "mainquest.BackToSchool", "talking to Mom should start the chain");

            yield return ChooseDayPlan(keyboard);
            yield return Capture("01-day-plan-chosen.png");

            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "School", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return TalkTo(keyboard, "TownNpc_npc_teacher", "School", "the Teacher");
            yield return Capture("02-teacher-talked.png");
            Assert.AreEqual(2, ObjectivesDone(chain, 2), F + " BackToSchool should have both objectives after Mom's plan and the Teacher (progress " + DescribeProgress(chain, 2) + ").");

            yield return GoHomeAndSleep(keyboard);
            AssertStep(chain, "mainquest.AfterSchoolTour", "day 2 should open the after-school tour");

            // Day 2 — AfterSchoolTour: visit the four career places in Town.
            if (SceneManager.GetActiveScene().name != "Town")
            {
                yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", F);
                yield return PlayerFlowE2EDriver.DismissDailyEvent();
            }

            var visits = new System.Text.StringBuilder();
            foreach (var place in new[] { "location_restaurant", "location_medical_clinic", "location_design_studio", "location_training_ground" })
            {
                yield return VisitPlace(keyboard, "FacilityActivities_" + place);
                visits.Append(place).Append(": step=").Append(StepId(chain)).Append(" progress=").Append(DescribeProgress(chain, 4))
                    .Append(" scene=").Append(SceneManager.GetActiveScene().name).Append(" | ");
                Debug.Log("[MAIN-E2E] after " + place + " step=" + StepId(chain) + " progress=" + DescribeProgress(chain, 4));
            }
            yield return Capture("03-tour-done.png");
            AssertStep(chain, "mainquest.FirstPartTimeJob", "visiting the four places should finish the tour. Visits: " + visits);

            // FirstPartTimeJob: one part-time job played through its minigame, then sleep into day 3.
            yield return WorkOneShift(keyboard, "work.shift.corner-store");
            yield return GoHomeAndSleep(keyboard);
            AssertStep(chain, "mainquest.MyDream", "a finished job and day 3 should open MyDream");

            // MyDream: read job books at the library (one of the three accepted ways).
            if (SceneManager.GetActiveScene().name != "Town")
            {
                yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", F);
                yield return PlayerFlowE2EDriver.DismissDailyEvent();
            }

            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_library", 1, F);
            yield return Capture("04-dream-library.png");
            AssertStep(chain, "mainquest.MyOwnSpace", "reading job books should finish MyDream");

            // MyOwnSpace: place one accepted piece of furniture in the room at home.
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlaceAcceptedFurniture(chain);
            yield return Capture("05-room-furniture.png");
            Assert.AreEqual(QuestChainState.Completed, Log().GetState(chain), F + " placing furniture at home should complete the chain (step " + StepId(chain) + ").");
        }

        private IEnumerator TalkTo(Keyboard keyboard, string npcName, string sceneName, string label)
        {
            GameObject npc = null;
            for (float t = 0f; t < 10f && (npc == null || npc.scene.name != sceneName); t += Time.unscaledDeltaTime)
            {
                npc = GameObject.Find(npcName);
                yield return null;
            }

            Assert.IsTrue(npc != null && npc.scene.name == sceneName, F + " " + label + " is not in " + sceneName + " (time " + DescribeTime() + ").");
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, npc.transform.position, F + " " + label);
            var dialogue = Object.FindFirstObjectByType<Rootborn.UI.Quests.DialoguePanel>(FindObjectsInactive.Include);
            Assert.IsTrue(dialogue != null && dialogue.IsOpen, F + " E next to " + label + " did not open a dialogue.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton"));
        }

        private IEnumerator ChooseDayPlan(Keyboard keyboard)
        {
            var board = FindInActiveScene("DailyPlanBoard");
            Assert.IsNotNull(board, F + " " + SceneManager.GetActiveScene().name + " has no DailyPlanBoard.");
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, board.transform.position, F + " day plan board");
            DailyPlanPanel panel = null;
            for (int i = 0; i < 120 && (panel == null || !panel.gameObject.activeInHierarchy); i++)
            {
                panel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                yield return null;
            }

            Assert.IsTrue(panel != null && panel.gameObject.activeInHierarchy, F + " E at the day plan board did not open the plan.");
            Button row = null;
            foreach (var button in panel.GetComponentsInChildren<Button>(false))
                if (button.name == "DailyPlanRouteRow" && button.interactable) { row = button; break; }
            Assert.IsNotNull(row, F + " the day plan offers no route to choose. " + PlayerFlowE2EDriver.DescribeButtons(panel.transform));
            yield return PlayerFlowE2EDriver.ClickButton(row);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "PlanCloseButton"));
        }

        private IEnumerator VisitPlace(Keyboard keyboard, string pointName)
        {
            var point = GameObject.Find(pointName);
            Assert.IsNotNull(point, F + " Town has no " + pointName + ".");
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, point.transform.position, F + " " + pointName);
            var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            if (panel != null && panel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "CloseLocation"));
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
        }

        private IEnumerator WorkOneShift(Keyboard keyboard, string workId)
        {
            PartTimeWorkInteractor work = null;
            foreach (var candidate in Object.FindObjectsByType<PartTimeWorkInteractor>(FindObjectsSortMode.None))
                if (candidate.Work != null && candidate.Work.Id == workId) { work = candidate; break; }
            Assert.IsNotNull(work, F + " Town has no work point for " + workId + ".");
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, work.transform.position + new Vector3(0f, -0.5f, 0f), 0.45f, 900, F + " could not reach " + workId + ".");
            var router = player.GetComponent<PlayerInteractionRouter>();
            router.RefreshPromptNow();
            string before = "prompt=" + router.PromptVisible + " '" + router.PromptText + "' target=" + (router.CurrentInteractable as Component)?.name
                + " locks=" + PlayerFlowE2EDriver.InputLockOwners() + " remaining=" + player.GetComponent<StudentLifeProgressComponent>().Progress.TimePointsRemaining
                + " pendingMinigame=" + player.GetComponent<StudentLifeProgressComponent>().Progress.HasPendingMinigame + " " + DescribeTime();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            Rootborn.UI.Minigames.MinigamePanel panel = null;
            for (int i = 0; i < 120 && (panel == null || !panel.IsOpen); i++)
            {
                panel = Object.FindFirstObjectByType<Rootborn.UI.Minigames.MinigamePanel>(FindObjectsInactive.Include);
                yield return null;
            }

            if (panel == null || !panel.IsOpen) yield return Capture("fail-work-" + workId + ".png");
            Assert.IsTrue(panel != null && panel.IsOpen, F + " E at " + workId + " did not open its minigame. Before E: " + before);
            yield return PlayerFlowE2EDriver.PlayMinigameToEnd(panel, 120f, F);
            Assert.IsTrue(panel.Session.Succeeded, F + " the shift minigame did not succeed (score " + panel.Session.Score + ").");
            if (panel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "Close"));
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
        }

        private IEnumerator GoHomeAndSleep(Keyboard keyboard)
        {
            for (int hop = 0; hop < 3 && SceneManager.GetActiveScene().name != "House"; hop++)
            {
                string next = SceneManager.GetActiveScene().name == "Town" ? "House" : "Town";
                yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, next, F);
                yield return PlayerFlowE2EDriver.DismissDailyEvent();
            }

            var board = FindInActiveScene("StudentDayEndBoard");
            Assert.IsNotNull(board, F + " House has no StudentDayEndBoard.");
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, board.transform.position, 0.9f, 900, F + " could not walk to the day-end board.");
            var interactor = board.GetComponent<StudentDayEndInteractor>();
            for (int i = 0; i < 300 && !interactor.CanInteract(player); i++) yield return null;
            player.GetComponent<PlayerInteractionRouter>().RefreshPromptNow();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            StudentDayResultPanel panel = null;
            for (float t = 0f; t < 3f && (panel == null || !panel.IsOpen); t += Time.unscaledDeltaTime)
            {
                panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                yield return null;
            }

            Assert.IsTrue(panel != null && panel.IsOpen, F + " the day result panel did not open.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "NextDayButton"));
            yield return null;
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
        }

        private IEnumerator PlaceAcceptedFurniture(QuestChainDefinition chain)
        {
            var step = Log().GetCurrentStep(chain);
            var objective = step.Objectives[0] as Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective;
            Assert.IsNotNull(objective, F + " MyOwnSpace's first objective is not furniture placement.");
            var accepted = new System.Collections.Generic.List<string>();
            foreach (var furniture in objective.AcceptedFurniture) if (furniture != null) accepted.Add(furniture.StableId);

            var toggle = GameObject.Find(HousePlacementTestDriver.ToggleName);
            Assert.IsNotNull(toggle, F + " House has no furniture button.");
            yield return ClickWithMouse(toggle, F + " furniture button");
            GameObject paletteButton = null;
            for (int page = 0; page < 40 && paletteButton == null; page++)
            {
                paletteButton = FindPaletteButton(accepted);
                if (paletteButton != null) break;
                var next = PlayerFlowE2EDriver.FindButton(Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>().transform, "FurniturePaletteNextPage");
                Assert.IsTrue(next != null && next.interactable, F + " none of the accepted room furniture is in the House palette.");
                yield return ClickWithMouse(next.gameObject, F + " palette next page");
            }

            Assert.IsNotNull(paletteButton, F + " none of the accepted room furniture is in the House palette.");
            yield return ClickWithMouse(paletteButton, F + " palette item " + paletteButton.name);
            var overlay = Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorPlacementPreviewOverlay>();
            for (int i = 0; i < 60 && (overlay == null || overlay.ValidCellCount == 0); i++) { yield return null; overlay = Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorPlacementPreviewOverlay>(); }
            Assert.IsTrue(overlay != null && overlay.ValidCellCount > 0, F + " choosing " + paletteButton.name + " showed no free cells.");
            Assert.IsTrue(TryFindFreeRoomCell(out var screen), F + " no free room cell is visible outside the UI.");
            yield return PressMouseInWorld(screen);
            for (int i = 0; i < 60 && Log().GetState(chain) != QuestChainState.Completed; i++) yield return null;
        }

        private static QuestChainLog Log()
        {
            var service = Object.FindFirstObjectByType<Rootborn.UI.Quests.QuestChainRuntimeService>();
            Assert.IsNotNull(service, F + " no quest chain service.");
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            return service.GetLog(student.Progress);
        }

        private static string StepId(QuestChainDefinition chain)
        {
            var step = Log().GetCurrentStep(chain);
            return step != null ? step.Id : "<none>";
        }

        private static void AssertStep(QuestChainDefinition chain, string expected, string why)
        {
            var log = Log();
            Assert.AreEqual(expected, StepId(chain), F + " " + why + " (state " + log.GetState(chain) + ", time " + DescribeTime() + ").");
        }

        private static int ObjectivesDone(QuestChainDefinition chain, int objectiveCount)
        {
            var log = Log();
            var step = log.GetCurrentStep(chain);
            int done = 0;
            for (int i = 0; i < objectiveCount && step != null && i < step.Objectives.Length; i++)
                if (step.Objectives[i] != null && log.GetObjectiveProgress(chain, i) >= step.Objectives[i].RequiredCount) done++;
            return done;
        }

        private static string DescribeProgress(QuestChainDefinition chain, int objectiveCount)
        {
            var log = Log();
            var step = log.GetCurrentStep(chain);
            int count = step != null ? Mathf.Min(objectiveCount, step.Objectives.Length) : 0;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++) text.Append(i).Append('=').Append(log.GetObjectiveProgress(chain, i)).Append(' ');
            return text.ToString();
        }

        private static string DescribeTime()
        {
            var player = GameObject.Find("Player");
            var progress = player != null ? player.GetComponent<StudentLifeProgressComponent>()?.Progress : null;
            return progress == null ? "?" : "day " + progress.CurrentDay + " spent " + progress.TimePointsSpentToday + " scene " + SceneManager.GetActiveScene().name;
        }

        private static GameObject FindInActiveScene(string name)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = PlayerFlowE2EDriver.FindChild(root.transform, name);
                if (found != null && found.gameObject.activeInHierarchy) return found.gameObject;
            }

            return null;
        }

        private static GameObject FindPaletteButton(System.Collections.Generic.List<string> accepted)
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!button.name.StartsWith("PaletteFurniture_", System.StringComparison.Ordinal)) continue;
                if (accepted.Contains(button.name.Substring("PaletteFurniture_".Length))) return button.gameObject;
            }

            return null;
        }

        // A free (green) placement cell in view and not under any UI.
        private static bool TryFindFreeRoomCell(out Vector2 screen)
        {
            var tilemap = GameObject.Find("AuthoredPlacementAvailability")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            var camera = Camera.main;
            screen = default;
            if (tilemap == null || camera == null) return false;
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile<UnityEngine.Tilemaps.Tile>(cell);
                if (tile == null || tile.color.g <= 0.8f || tile.color.r >= 0.3f) continue;
                var point = camera.WorldToScreenPoint(tilemap.GetCellCenterWorld(cell));
                if (point.z <= 0f || point.x < Screen.width * 0.3f || point.x > Screen.width * 0.9f || point.y < Screen.height * 0.15f || point.y > Screen.height * 0.85f) continue;
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
                if (results.Count > 0) continue;
                screen = point;
                return true;
            }

            return false;
        }

        private static IEnumerator PressMouseInWorld(Vector2 point)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
        }

        // Clicks a UI object (also non-Button ones such as the furniture toggle) with a mouse device through the UI module.
        private static IEnumerator ClickWithMouse(GameObject target, string failure)
        {
            Assert.IsNotNull(target, failure + " target missing.");
            var button = target.GetComponent<Button>();
            if (button != null)
            {
                yield return PlayerFlowE2EDriver.ClickButton(button);
                yield break;
            }

            yield return PlayerFlowE2EDriver.ClickWithMouse(target, failure);
        }

        private static IEnumerator Capture(string fileName)
        {
            string path = Evidence + fileName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            for (float t = 0f; t < 3f && !(File.Exists(path) && new FileInfo(path).Length > 0L); t += Time.unscaledDeltaTime) yield return null;
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
            PlayerFlowE2EDriver.DestroyAllNamed("LocationNpcCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("TownHelpActionPoint");
        }
    }
}

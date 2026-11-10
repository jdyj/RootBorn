using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.Careers;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    /// <summary>
    /// ADULT-E2E: the adult career at the Chef facility, one step per test. The starting state is preparation (the adult
    /// career snapshot is edited, saved and rebound, as a reload would); the step itself is played with keyboard walking,
    /// E at the facility door and mouse-device clicks on the facility panel and the minigame.
    /// </summary>
    public sealed class AdultCareerE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string F = "ADULT-E2E failed:";
        private const string FacilityId = "facility.chef";
        private const string CandidateId = "candidate.chef";
        private const string CareerId = "career.chef";
        private const string FirstWorkId = "work.chef.level1";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-adult-career-e2e", Guid.NewGuid().ToString("N"));
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

        // ADULT-E2E-001: a student whose Chef candidate is ready on day 4 chooses the Chef route at the door; growth is kept.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ADULT_E2E_001_ReadyStudentChoosesTheChefRouteAtTheFacilityDoor()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            Prepare(s => MakeCandidateReady(s));
            var owner = Owner();
            Assert.IsFalse(owner.Progress.IsAdult, F + " the prepared player should still be a student.");
            string traitsBefore = TraitsOf(owner);

            var panel = default(FacilityManagementPanel);
            yield return OpenFacilityPanel(keyboard, p => panel = p);
            var choose = PlayerFlowE2EDriver.FindButton(panel.transform, "ChooseRoute");
            Assert.IsTrue(choose.interactable, F + " ChooseRoute is not available for a ready student on day 4. " + Describe(panel));
            yield return PlayerFlowE2EDriver.ClickButton(choose);
            yield return null;

            Assert.AreEqual(FacilityId, owner.Progress.FacilityId, F + " choosing the route did not make the player a Chef adult. " + Describe(panel));
            Assert.AreEqual(1, owner.Progress.FacilityLevel);
            Assert.AreEqual(traitsBefore, TraitsOf(owner), F + " becoming an adult changed the student's traits.");
            yield return Capture("adult-01-route-chosen.png");
        }

        // ADULT-E2E-002: a Chef adult at level 1 plays the first work from the panel through its minigame and it counts once.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ADULT_E2E_002_AdultPlaysTheFirstWorkFromThePanel()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            Prepare(s => { MakeCandidateReady(s); s.Adult.FacilityId = FacilityId; s.Adult.FacilityLevel = 1; });
            var owner = Owner();
            Assert.IsTrue(owner.Progress.IsAdult, F + " the prepared player should be a Chef adult.");

            var panel = default(FacilityManagementPanel);
            yield return OpenFacilityPanel(keyboard, p => panel = p);
            var play = FirstRowButton(panel, "PlayWork");
            Assert.IsTrue(play.interactable, F + " the first work cannot be played. " + Describe(panel));
            yield return PlayerFlowE2EDriver.ClickButton(play);

            Rootborn.UI.Minigames.MinigamePanel minigame = null;
            for (int i = 0; i < 180 && (minigame == null || !minigame.IsOpen); i++)
            {
                minigame = Object.FindFirstObjectByType<Rootborn.UI.Minigames.MinigamePanel>(FindObjectsInactive.Include);
                yield return null;
            }

            Assert.IsTrue(minigame != null && minigame.IsOpen, F + " PlayWork did not open the work minigame.");
            yield return PlayerFlowE2EDriver.PlayMinigameToEnd(minigame, 120f, F);
            Assert.IsTrue(minigame.Session.Succeeded, F + " the work minigame played from the screen did not pass (score " + minigame.Session.Score + ").");
            if (minigame.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(minigame.transform, "Close"));
            yield return null;

            var work = WorkOf(owner, FirstWorkId);
            Assert.IsNotNull(work, F + " the played work left no progress.");
            Assert.AreEqual(1, work.SuccessfulCompletions, F + " one passed work should count once.");
            Assert.Greater(work.Mastery, 0, F + " a passed work should raise mastery.");
            yield return Capture("adult-02-work-played.png");
        }

        // ADULT-E2E-003: at level 2 with the cost in hand the player upgrades to level 3, the route completes (ending), the
        // achievement shows, and work goes on: the unlocked auto-complete still pays.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ADULT_E2E_003_UpgradeToLevelThreeCompletesTheRouteAndWorkGoesOn()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            Prepare(s =>
            {
                MakeCandidateReady(s);
                s.Adult.FacilityId = FacilityId;
                s.Adult.FacilityLevel = 2;
                s.Adult.Works = new[] { new FacilityWorkProgressSaveData { WorkId = FirstWorkId, Mastery = 100, SuccessfulCompletions = 5 } };
                s.Inventory.Items = s.Inventory.Items.Concat(new[]
                {
                    new PlayerInventoryItemSaveData { ItemId = "item.allowance", Count = 300 },
                    new PlayerInventoryItemSaveData { ItemId = "item.facility-materials", Count = 10 },
                }).ToArray();
            });
            var owner = Owner();
            Assert.AreEqual(2, owner.Progress.FacilityLevel, F + " the prepared adult should be at level 2.");

            var panel = default(FacilityManagementPanel);
            yield return OpenFacilityPanel(keyboard, p => panel = p);
            var upgrade = PlayerFlowE2EDriver.FindButton(panel.transform, "UpgradeFacility");
            Assert.IsTrue(upgrade.interactable, F + " UpgradeFacility is not available with the level 3 cost in hand. " + Describe(panel));
            yield return PlayerFlowE2EDriver.ClickButton(upgrade);
            yield return null;

            Assert.AreEqual(3, owner.Progress.FacilityLevel, F + " the upgrade did not reach level 3. " + Describe(panel));
            Assert.That(owner.Progress.EndingIds, Does.Contain(FacilityId), F + " reaching level 3 did not complete the route.");
            var trophy = PlayerFlowE2EDriver.FindChild(panel.transform, "CareerAchievement");
            Assert.IsTrue(trophy != null && trophy.gameObject.activeInHierarchy, F + " the route-complete achievement is not shown. " + Describe(panel));
            Assert.IsFalse(upgrade.interactable, F + " the maximum level must not offer another upgrade.");
            yield return Capture("adult-03-route-complete.png");

            int before = WorkOf(owner, FirstWorkId).SuccessfulCompletions;
            var skip = FirstRowButton(panel, "AutoSkipWork");
            Assert.IsTrue(skip.interactable, F + " auto-complete is not offered after the route completes. " + Describe(panel));
            yield return PlayerFlowE2EDriver.ClickButton(skip);
            yield return null;
            Assert.AreEqual(before + 1, WorkOf(owner, FirstWorkId).SuccessfulCompletions, F + " work after the ending did not count. " + Describe(panel));
        }

        // ADULT-E2E-004 (audit 4.3/4.4): one more passed work from the screen unlocks auto-complete (mastery 100, five
        // successes, level 2); auto-complete pays the full-score reward once, the next press is refused for lack of time
        // without paying again, and the result survives Continue.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ADULT_E2E_004_LastPassedWorkUnlocksAutoCompleteThatPaysTheTopRewardOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            Prepare(s =>
            {
                MakeCandidateReady(s);
                s.Adult.FacilityId = FacilityId;
                s.Adult.FacilityLevel = 2;
                s.Adult.Works = new[] { new FacilityWorkProgressSaveData { WorkId = FirstWorkId, Mastery = 80, SuccessfulCompletions = 4 } };
            });
            var owner = Owner();
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), F + " no game data.");
            var work = registry.FacilityWorks.First(w => w != null && w.Id == FirstWorkId);

            var panel = default(FacilityManagementPanel);
            yield return OpenFacilityPanel(keyboard, p => panel = p);
            Assert.IsFalse(FirstRowButton(panel, "AutoSkipWork").interactable, F + " auto-complete is offered before mastery is full. " + Describe(panel));

            yield return PlayerFlowE2EDriver.ClickButton(FirstRowButton(panel, "PlayWork"));
            Rootborn.UI.Minigames.MinigamePanel minigame = null;
            for (int i = 0; i < 180 && (minigame == null || !minigame.IsOpen); i++)
            {
                minigame = Object.FindFirstObjectByType<Rootborn.UI.Minigames.MinigamePanel>(FindObjectsInactive.Include);
                yield return null;
            }
            Assert.IsTrue(minigame != null && minigame.IsOpen, F + " PlayWork did not open the work minigame.");
            yield return PlayerFlowE2EDriver.PlayMinigameToEnd(minigame, 120f, F);
            Assert.IsTrue(minigame.Session.Succeeded, F + " the work minigame played from the screen did not pass (score " + minigame.Session.Score + ").");
            if (minigame.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(minigame.transform, "Close"));
            yield return null;
            Assert.AreEqual(5, WorkOf(owner, FirstWorkId).SuccessfulCompletions, F + " the fifth passed work did not count.");
            Assert.AreEqual(work.MaximumMastery, WorkOf(owner, FirstWorkId).Mastery, F + " the fifth passed work did not fill mastery.");

            if (!panel.IsOpen) yield return OpenFacilityPanel(keyboard, p => panel = p);
            var skip = FirstRowButton(panel, "AutoSkipWork");
            Assert.IsTrue(skip.interactable, F + " auto-complete did not unlock after the fifth passed work. " + Describe(panel));
            Assert.IsTrue(work.TryBuildRewards(100, work.Facility.GetLevel(2).RewardPercent, out var top), F + " the top reward cannot be built.");
            var inventory = owner.PlayerInventory.Inventory;
            var before = top.Select(g => inventory.CountOf(g.Item)).ToArray();
            yield return PlayerFlowE2EDriver.ClickButton(skip);
            yield return null;
            for (int i = 0; i < top.Length; i++)
                Assert.AreEqual(before[i] + top[i].Count, inventory.CountOf(top[i].Item), F + " auto-complete did not pay the full-score reward of " + top[i].Item.name + " once. " + Describe(panel));
            Assert.AreEqual(6, WorkOf(owner, FirstWorkId).SuccessfulCompletions, F + " auto-complete did not count as a success.");
            yield return Capture("adult-04-auto-complete.png");

            // Today's time is spent (work 1 TP + auto-complete 1 TP); another press must not pay.
            var paid = top.Select(g => inventory.CountOf(g.Item)).ToArray();
            skip = FirstRowButton(panel, "AutoSkipWork");
            if (skip.interactable) yield return PlayerFlowE2EDriver.ClickButton(skip);
            yield return null;
            for (int i = 0; i < top.Length; i++)
                Assert.AreEqual(paid[i], inventory.CountOf(top[i].Item), F + " auto-complete paid again with no time left. " + Describe(panel));
            Assert.AreEqual(6, WorkOf(owner, FirstWorkId).SuccessfulCompletions, F + " a refused auto-complete counted.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot("slot-0", "LoadButton", "Town", F + " Continue did not return to Town.");
            var reloaded = Owner();
            Assert.AreEqual(6, WorkOf(reloaded, FirstWorkId).SuccessfulCompletions, F + " work progress did not survive Continue exactly once.");
            for (int i = 0; i < top.Length; i++)
                Assert.AreEqual(paid[i], reloaded.PlayerInventory.Inventory.CountOf(top[i].Item), F + " the reward of " + top[i].Item.name + " changed across Continue.");
        }

        private static IEnumerator EnterNewGame()
        {
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot("slot-0", "NewGameButton", "Town", F + " New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
        }

        private static AdultCareerRuntimeComponent Owner()
        {
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), F + " no game data.");
            var owner = AdultCareerRuntimeComponent.Ensure(GameObject.Find("Player"), registry);
            Assert.IsNotNull(owner, F + " the player has no adult career owner.");
            return owner;
        }

        // Preparation: edit the adult career snapshot (student, adult, inventory), save it and rebind, as loading the save would.
        private static void Prepare(Action<AdultCareerSnapshot> edit)
        {
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), F + " no game data.");
            var owner = Owner();
            var snapshot = AdultCareerSnapshotPersistence.Capture(owner.Progress, owner.StudentComponent.Progress, owner.PlayerInventory.Inventory, owner.World);
            Assert.IsNotNull(snapshot, F + " the adult career snapshot could not be captured.");
            snapshot.Inventory = owner.PlayerInventory.ToSaveData();
            snapshot.QuestChains = owner.QuestChains.ToSaveData();
            snapshot.FurnitureUnlocks = owner.FurnitureUnlocks.ToSaveData();
            edit(snapshot);
            var storage = new SaveService(ActiveSaveContext.SlotId);
            Assert.IsTrue(AdultCareerSnapshotPersistence.SaveSession(storage, snapshot), F + " the prepared snapshot was refused.");
            owner.Bind(registry, storage);
            Assert.IsFalse(owner.RecoveryRequired, F + " rebinding the prepared snapshot needs recovery.");
        }

        private static void MakeCandidateReady(AdultCareerSnapshot s)
        {
            s.Student.CurrentDay = Mathf.Max(4, s.Student.CurrentDay);
            // The day plan diary is saved per day; the save check refuses a diary from another day.
            if (s.Student.DailyPlan != null && !DailyPlanProgressSaveData.IsEmpty(s.Student.DailyPlan)) s.Student.DailyPlan.CurrentDay = s.Student.CurrentDay;
            s.Student.DayState = StudentDayState.InProgress.ToString();
            s.Student.TimePointsSpentToday = 0;
            if (!s.Student.CareerHintIds.Contains(CareerId)) s.Student.CareerHintIds = s.Student.CareerHintIds.Concat(new[] { CareerId }).ToArray();
            var candidates = s.Student.CareerCandidates ?? new CareerCandidateProgressSaveData { SaveSlot = s.Student.SaveSlot, PlayerId = s.Student.PlayerId };
            candidates.SaveSlot = s.Student.SaveSlot;
            candidates.PlayerId = s.Student.PlayerId;
            candidates.Candidates = candidates.Candidates.Where(c => c.CandidateId != CandidateId).Concat(new[]
            {
                new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = CandidateId, UnderstandingScore = 3, HintIds = new[] { "hint.chef.practice" } },
            }).ToArray();
            s.Student.CareerCandidates = candidates;
        }

        private static IEnumerator OpenFacilityPanel(Keyboard keyboard, Action<FacilityManagementPanel> found)
        {
            var facility = GameObject.Find("Facility_Facility_Chef");
            Assert.IsNotNull(facility, F + " Town has no Chef facility.");
            var door = PlayerFlowE2EDriver.FindChild(facility.transform, "Door");
            Assert.IsNotNull(door, F + " the Chef facility has no door.");
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, door.position, F + " Chef door");
            FacilityManagementPanel panel = null;
            for (int i = 0; i < 120 && (panel == null || !panel.IsOpen); i++)
            {
                panel = Object.FindFirstObjectByType<FacilityManagementPanel>(FindObjectsInactive.Include);
                yield return null;
            }

            Assert.IsTrue(panel != null && panel.IsOpen, F + " E at the Chef door did not open the facility panel. " + PlayerFlowE2EDriver.LastInteractTrace);
            found(panel);
        }

        private static Button FirstRowButton(FacilityManagementPanel panel, string name)
        {
            var row = PlayerFlowE2EDriver.FindChild(panel.transform, "WorkRow");
            Assert.IsNotNull(row, F + " the facility panel lists no work. " + Describe(panel));
            return PlayerFlowE2EDriver.FindButton(row, name);
        }

        private static FacilityWorkProgressSaveData WorkOf(AdultCareerRuntimeComponent owner, string workId)
            => owner.Progress.ToSaveData().Works.FirstOrDefault(w => w != null && w.WorkId == workId);

        private static string TraitsOf(AdultCareerRuntimeComponent owner)
        {
            var match = Regex.Match(JsonUtility.ToJson(owner.StudentComponent.Progress.ToSaveData()), "\"Traits\":\\[[^\\]]*\\]");
            return match.Success ? match.Value : string.Empty;
        }

        private static string Describe(FacilityManagementPanel panel)
        {
            var summary = PlayerFlowE2EDriver.FindChild(panel.transform, "Summary")?.GetComponent<Text>();
            var result = PlayerFlowE2EDriver.FindChild(panel.transform, "Result")?.GetComponent<Text>();
            return "summary='" + (summary != null ? summary.text : "-") + "' result='" + (result != null ? result.text : "-") + "' " + PlayerFlowE2EDriver.DescribeButtons(panel.transform);
        }

        private static IEnumerator Capture(string fileName)
        {
            string path = "output/adult-career-e2e/" + fileName;
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

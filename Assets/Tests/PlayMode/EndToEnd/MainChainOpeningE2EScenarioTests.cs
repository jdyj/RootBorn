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
        private const string F = "MAIN-E2E failed:";
        private string _saveRoot;
        private static string LastTarget = string.Empty;
        private static int _paletteFoundOnPage = -1;

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

        // Each step test prepares the chain at the start of one step (preparation: quest-chain save data loaded into the live
        // log) and finishes that step only with player input, so a test walks a short way instead of across three days.

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_001_BackToSchool_MomStartsChainThenPlanAndTeacherCount()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
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
            Assert.AreEqual(2, ObjectivesDone(chain, 2), F + " BackToSchool should have both objectives after the plan and the Teacher (progress " + DescribeProgress(chain, 2) + ").");
            AssertStep(chain, "mainquest.BackToSchool", "the step waits for day 2");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_002_BackToSchool_SleepingIntoDayTwoOpensTheTour()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.BackToSchool", 1, 1);
            yield return GoHomeAndSleep(keyboard);
            AssertStep(chain, "mainquest.AfterSchoolTour", "sleeping into day 2 with both objectives done should open the tour");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_003_AfterSchoolTour_VisitingTheFourCareerPlaces()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.AfterSchoolTour");
            var visits = new System.Text.StringBuilder();
            foreach (var place in new[] { "location_restaurant", "location_medical_clinic", "location_design_studio", "location_training_ground" })
            {
                yield return VisitPlace(keyboard, "FacilityActivities_" + place);
                visits.Append(place).Append(": step=").Append(StepId(chain)).Append(" progress=").Append(DescribeProgress(chain, 4))
                    .Append(" E-target=").Append(LastTarget).Append(" | ");
            }
            yield return Capture("03-tour-done.png");
            AssertStep(chain, "mainquest.FirstPartTimeJob", "visiting the four places should finish the tour. Visits: " + visits);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_004_FirstPartTimeJob_AShiftCounts()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.FirstPartTimeJob");
            yield return WorkOneShift(keyboard, "work.shift.corner-store");
            Assert.AreEqual(1, ObjectivesDone(chain, 1), F + " the finished shift should count for FirstPartTimeJob (progress " + DescribeProgress(chain, 1) + ").");
            AssertStep(chain, "mainquest.FirstPartTimeJob", "the step waits for day 3");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_005_FirstPartTimeJob_DayThreeOpensMyDream()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.FirstPartTimeJob", 1);
            yield return GoHomeAndSleep(keyboard);
            AssertStep(chain, "mainquest.FirstPartTimeJob", "day 2 is too early for the dream step");
            yield return GoHomeAndSleep(keyboard);
            AssertStep(chain, "mainquest.MyDream", "day 3 with a finished job should open MyDream");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_006_MyDream_ReadingJobBooksAtTheLibrary()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.MyDream");
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_library", 1, F);
            yield return Capture("04-dream-library.png");
            AssertStep(chain, "mainquest.MyOwnSpace", "reading job books should finish MyDream");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_007_MyOwnSpace_PlacingRoomFurnitureCompletesTheChain()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.MyOwnSpace");
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlaceAcceptedFurniture(chain);
            yield return Capture("05-room-furniture.png");
            Assert.AreEqual(QuestChainState.Completed, Log().GetState(chain), F + " placing furniture at home should complete the chain (step " + StepId(chain) + ").");
        }

        // MyOwnSpace tells the player to buy furniture with allowance. Earn it with two shifts, then buy at the corner store
        // with a mouse click: the allowance is charged once and the furniture is unlocked for the home catalog.
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator MAIN_E2E_008_MyOwnSpace_BuyingFurnitureWithShiftAllowance()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            PrepareChainAt(Chain(), "mainquest.MyOwnSpace");
            yield return EarnAndBuyAtShop(keyboard, CornerStoreOffer(), "06-bought-furniture.png");
        }

        // MyOwnSpace with bought furniture: the offer is not in the House palette before buying; after the shifts and the
        // purchase it is, and placing that exact piece at home with the mouse completes the chain.
        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator MAIN_E2E_010_MyOwnSpace_PlacingBoughtFurnitureCompletesTheChain()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            var chain = Chain();
            PrepareChainAt(chain, "mainquest.MyOwnSpace");
            var offer = CornerStoreOffer();
            var objective = Log().GetCurrentStep(chain).Objectives[0] as Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective;
            Assert.IsNotNull(objective, F + " MyOwnSpace's first objective is not furniture placement.");
            bool accepts = false;
            foreach (var furniture in objective.AcceptedFurniture) accepts |= furniture != null && furniture.StableId == offer.StableId;
            Assert.IsTrue(accepts, F + " MyOwnSpace does not accept the corner store furniture " + offer.StableId + ".");

            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            bool listed = false;
            yield return PaletteListsFurniture(offer.StableId, found => listed = found);
            Assert.IsFalse(listed, F + " " + offer.StableId + " is in the House palette before it is bought.");
            var placementPanel = Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>();
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(placementPanel.transform, "CloseFurniturePanelButton").gameObject, F + " furniture panel close");
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();

            yield return EarnAndBuyAtShop(keyboard, offer, null);
            for (int hop = 0; hop < 3 && SceneManager.GetActiveScene().name != "House"; hop++)
            {
                yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, SceneManager.GetActiveScene().name == "Town" ? "House" : "Town", F);
                yield return PlayerFlowE2EDriver.DismissDailyEvent();
            }

            Assert.AreEqual("House", SceneManager.GetActiveScene().name, F + " could not walk home after buying.");
            var catalog = Rootborn.UI.Interiors.InteriorFurnitureCatalog.LoadFurniture();
            var meta = ActiveSaveContext.Metadata;
            var owned = GameObject.Find("Player").GetComponent<Rootborn.UI.Careers.AdultCareerRuntimeComponent>().FurnitureUnlocks;
            var resolved = Rootborn.Game.Placement.FurnitureUnlockProgressPersistence.LoadOrCreate(meta?.SlotId, Rootborn.Game.Player.PlayerIdentity.DefaultPlayerId);
            var panels = Object.FindObjectsByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsTrue(catalog.Exists(f => f.Id == offer.StableId), F + " the home catalog lacks the bought " + offer.StableId
                + " (catalog " + catalog.Count + ", save slot '" + meta?.SlotId + "', owner " + owned.SaveSlot + "/" + owned.PlayerId
                + " owns=" + owned.IsUnlocked(offer.UnlockToken) + ", resolved " + resolved.SaveSlot + "/" + resolved.PlayerId
                + " owns=" + resolved.IsUnlocked(offer.UnlockToken) + ", panels " + panels.Length + ").");
            yield return PlaceAcceptedFurniture(chain, offer.StableId);
            yield return Capture("09-placed-bought-furniture.png");
            Assert.AreEqual(0, _paletteFoundOnPage, F + " bought furniture should be on the palette's first page, not buried among the free pieces.");
            Assert.AreEqual(QuestChainState.Completed, Log().GetState(chain), F + " placing the bought " + offer.StableId + " should complete the chain (step " + StepId(chain) + ").");
        }

        private static Rootborn.Game.Placement.FurnitureDefinition CornerStoreOffer()
        {
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), F + " no game data.");
            var offer = System.Array.Find(registry.FurnitureDefinitions, f => f != null && f.IsForSale && f.PurchaseLocationId == "location.corner-store");
            Assert.IsNotNull(offer, F + " nothing is for sale at the corner store.");
            return offer;
        }

        // Opens the House furniture palette with the mouse and pages through all of it, reporting whether the item is listed.
        private static IEnumerator PaletteListsFurniture(string stableId, System.Action<bool> result)
        {
            var toggle = GameObject.Find(HousePlacementTestDriver.ToggleName);
            Assert.IsNotNull(toggle, F + " House has no furniture button.");
            yield return ClickWithMouse(toggle, F + " furniture button");
            var only = new System.Collections.Generic.List<string> { stableId };
            for (int page = 0; page < 200; page++)
            {
                if (FindPaletteButton(only) != null) { result(true); yield break; }
                var next = PlayerFlowE2EDriver.FindButton(Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>().transform, "FurniturePaletteNextPage");
                if (next == null || !next.interactable) break;
                yield return ClickWithMouse(next.gameObject, F + " palette next page");
            }

            result(false);
        }

        // Works corner store shifts until the allowance covers the offer, then buys it at the store with a mouse click: the
        // allowance is charged once, the furniture unlocks, and the offer can no longer be pressed.
        private IEnumerator EarnAndBuyAtShop(Keyboard keyboard, Rootborn.Game.Placement.FurnitureDefinition offer, string captureName)
        {
            var player = GameObject.Find("Player");
            var wallet = player.GetComponent<PlayerInventory>();
            var owner = player.GetComponent<Rootborn.UI.Careers.AdultCareerRuntimeComponent>();
            Assert.IsNotNull(owner, F + " the player has no career owner.");
            Assert.IsFalse(owner.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), F + " " + offer.name + " is owned before buying.");

            while (wallet.Inventory.CountOf(offer.PriceItem) < offer.Price)
            {
                int before = wallet.Inventory.CountOf(offer.PriceItem);
                yield return WorkOneShift(keyboard, "work.shift.corner-store");
                Assert.Greater(wallet.Inventory.CountOf(offer.PriceItem), before, F + " a shift paid no " + offer.PriceItem.name + ".");
            }
            int money = wallet.Inventory.CountOf(offer.PriceItem);

            var shop = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName(offer.PurchaseLocationId));
            Assert.IsNotNull(shop, F + " no shop point for " + offer.PurchaseLocationId + ".");
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, shop.transform.position, F + " the corner store");
            var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel != null && panel.IsOpen, F + " E at the corner store did not open its panel.");
            string name = string.IsNullOrEmpty(offer.DisplayNameKey) ? offer.DisplayName : Rootborn.Game.Localization.GameText.Get(offer.DisplayNameKey);
            string label = Rootborn.Game.Localization.GameText.Format("dream.shop.buy", name, offer.Price);
            Button buy = null;
            foreach (var button in panel.GetComponentsInChildren<Button>())
            {
                var text = button.GetComponentInChildren<Text>(true);
                if (text != null && text.text == label) { buy = button; break; }
            }
            Assert.IsNotNull(buy, F + " the corner store shows no '" + label + "' button.");
            Assert.IsTrue(buy.interactable, F + " the buy button is disabled with " + money + " " + offer.PriceItem.name + ".");
            yield return PlayerFlowE2EDriver.ClickButton(buy);
            // Disabled buttons fade to the disabled tint over the button's fade time (real time); let it finish before looking.
            yield return new WaitForSecondsRealtime(buy.colors.fadeDuration + 0.2f);
            if (!string.IsNullOrEmpty(captureName)) yield return Capture(captureName);
            var shown = buy.targetGraphic != null ? buy.targetGraphic.canvasRenderer.GetColor() : Color.white;
            Assert.Less(shown.a, 0.9f, F + " the owned offer still looks pressable (rendered tint " + shown + ").");

            Assert.IsTrue(owner.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), F + " buying did not unlock " + offer.name + ".");
            Assert.AreEqual(money - offer.Price, wallet.Inventory.CountOf(offer.PriceItem), F + " buying should charge the price once.");
            Assert.IsFalse(buy.interactable, F + " an owned offer must not be buyable again.");
            if (panel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "CloseLocation"));
        }

        // MyDream: choose a dream from the HUD "장래희망" panel with the mouse, close it, then Tab: the journal hint row for that
        // dream names it as the current one.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_009_MyDream_ChoosingADreamShowsItInTheJournal()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            PrepareChainAt(Chain(), "mainquest.MyDream");
            // A dream opens once its career is introduced. Hear the restaurant's introduction (its first activity button).
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "FacilityActivities_location_restaurant", 0, F);
            var player = GameObject.Find("Player");
            var interests = player.GetComponent<CareerInterestProgressComponent>();
            Assert.IsTrue(interests != null && interests.Interests != null && interests.Interests.Length > 0, F + " the player has no dreams to choose.");
            var student = player.GetComponent<StudentLifeProgressComponent>().Progress;
            var candidates = player.GetComponent<CareerCandidateProgressComponent>()?.Progress;
            int pick = -1;
            for (int i = 0; i < interests.Interests.Length && pick < 0; i++)
                if (interests.Interests[i] != null && interests.Interests[i].Evaluate(interests.EnsureProgress(), candidates, student, student.CurrentDay).Allowed) pick = i;
            if (pick < 0)
            {
                var why = new System.Text.StringBuilder();
                foreach (var interest in interests.Interests)
                    if (interest != null)
                        why.Append(interest.Id).Append(": ").Append(interest.Evaluate(interests.EnsureProgress(), candidates, student, student.CurrentDay).Reason)
                            .Append(" score=").Append(interest.Candidate != null && candidates != null ? candidates.GetUnderstandingScore(interest.Candidate) : -1).Append(" | ");
                Assert.Fail(F + " no dream can be chosen at the MyDream step. E-target=" + LastTarget + " logs=" + string.Join(",", student.GetTodayResultLogIds())
                    + " studentCandidatesSame=" + ReferenceEquals(candidates, student.CareerCandidates) + " :: " + why);
            }
            var dream = interests.Interests[pick];

            var open = GameObject.Find("CareerInterestOpenButton");
            Assert.IsNotNull(open, F + " the HUD has no dream button.");
            yield return PlayerFlowE2EDriver.ClickButton(open.GetComponent<Button>());
            var panel = Object.FindFirstObjectByType<CareerInterestPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel != null && panel.IsOpen, F + " the dream button did not open the dream panel.");
            yield return PlayerFlowE2EDriver.ClickButton(panel.GetInterestButtonForTests(pick));
            Assert.AreEqual(dream.Id, interests.EnsureProgress().CurrentInterestId, F + " clicking the " + dream.Id + " card did not choose it.");
            yield return Capture("07-dream-chosen.png");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "CloseButton"));

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            var journal = Object.FindFirstObjectByType<Rootborn.UI.Objectives.ObjectiveJournalPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(journal != null && journal.IsVisible, F + " Tab did not open the journal.");
            string name = Rootborn.Game.Localization.GameText.Get(dream.DisplayNameKey);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, name, F + " journal row for " + dream.Id));
            yield return Capture("08-dream-in-journal.png");
            StringAssert.Contains(Rootborn.Game.Localization.GameText.Format("dream.journal.current", name), journal.VisibleText, F + " the journal does not name the chosen dream.");
        }

        // Changing the dream: hear two careers' introductions, choose the first dream with the mouse, then change to the second
        // the same day. The journal names the new dream as current, and Continue keeps the change (and the earlier one as previous).
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MAIN_E2E_011_ChangingTheDreamMovesTheJournalAndSurvivesContinue()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterNewGame();
            PrepareChainAt(Chain(), "mainquest.MyDream");
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "FacilityActivities_location_restaurant", 0, F);
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "FacilityActivities_location_medical_clinic", 0, F);
            var player = GameObject.Find("Player");
            var interests = player.GetComponent<CareerInterestProgressComponent>();
            var student = player.GetComponent<StudentLifeProgressComponent>().Progress;
            var candidates = player.GetComponent<CareerCandidateProgressComponent>()?.Progress;
            var allowed = new System.Collections.Generic.List<int>();
            for (int i = 0; i < interests.Interests.Length; i++)
                if (interests.Interests[i] != null && interests.Interests[i].Evaluate(interests.EnsureProgress(), candidates, student, student.CurrentDay).Allowed) allowed.Add(i);
            Assert.GreaterOrEqual(allowed.Count, 2, F + " after two introductions fewer than two dreams can be chosen. E-target=" + LastTarget);
            var first = interests.Interests[allowed[0]];
            var second = interests.Interests[allowed[1]];

            yield return PlayerFlowE2EDriver.ClickButton(GameObject.Find("CareerInterestOpenButton").GetComponent<Button>());
            var panel = Object.FindFirstObjectByType<CareerInterestPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel != null && panel.IsOpen, F + " the dream button did not open the dream panel.");
            yield return PlayerFlowE2EDriver.ClickButton(panel.GetInterestButtonForTests(allowed[0]));
            Assert.AreEqual(first.Id, interests.EnsureProgress().CurrentInterestId, F + " clicking " + first.Id + " did not choose it.");
            yield return PlayerFlowE2EDriver.ClickButton(panel.GetInterestButtonForTests(allowed[1]));
            Assert.AreEqual(second.Id, interests.EnsureProgress().CurrentInterestId, F + " clicking " + second.Id + " the same day did not change the dream.");
            Assert.AreEqual(first.Id, interests.EnsureProgress().PreviousInterestId, F + " the change did not remember " + first.Id + " as the previous dream.");
            yield return Capture("11-dream-changed.png");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "CloseButton"));

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            var journal = Object.FindFirstObjectByType<Rootborn.UI.Objectives.ObjectiveJournalPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(journal != null && journal.IsVisible, F + " Tab did not open the journal.");
            string secondName = Rootborn.Game.Localization.GameText.Get(second.DisplayNameKey);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, secondName, F + " journal row for " + second.Id));
            StringAssert.Contains(Rootborn.Game.Localization.GameText.Format("dream.journal.current", secondName), journal.VisibleText, F + " the journal does not name the new dream.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot("slot-0", "LoadButton", "Town", F + " Continue did not return to Town.");
            var reloaded = GameObject.Find("Player").GetComponent<CareerInterestProgressComponent>().EnsureProgress();
            Assert.AreEqual(second.Id, reloaded.CurrentInterestId, F + " Continue lost the changed dream.");
            Assert.AreEqual(first.Id, reloaded.PreviousInterestId, F + " Continue lost the previous dream.");
        }

        private static IEnumerator EnterNewGame()
        {
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot("slot-0", "NewGameButton", "Town", F + " New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
        }

        private static QuestChainDefinition Chain()
        {
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), F + " no game data.");
            var chain = System.Array.Find(registry.QuestChains, c => c != null && c.Id == ChainId);
            Assert.IsNotNull(chain, F + " the registry has no " + ChainId + ".");
            return chain;
        }

        // Preparation: the chain accepted and tracked at the start of stepId, earlier steps completed, given objective counts.
        private static void PrepareChainAt(QuestChainDefinition chain, string stepId, params int[] objectiveProgress)
        {
            var log = Log();
            var data = log.ToSaveData();
            var record = System.Array.Find(data.Chains, c => c != null && c.ChainId == chain.Id);
            Assert.IsNotNull(record, F + " the live quest log has no " + chain.Id + ".");
            var completed = new System.Collections.Generic.List<string>();
            foreach (var step in chain.Steps)
            {
                if (step.Id == stepId) break;
                completed.Add(step.Id);
            }

            record.ChainState = QuestChainState.Tracked.ToString();
            record.IsTracked = true;
            record.CurrentStepId = stepId;
            record.CompletedStepIds = completed.ToArray();
            record.ObjectiveProgressValues = objectiveProgress ?? new int[0];
            record.AcceptedDayIndex = 0;
            log.LoadFromSaveData(data);
            Assert.AreEqual(stepId, StepId(chain), F + " preparing the chain at " + stepId + " did not take.");
        }

        [UnityTest]
        [Explicit("Whole chain in one run; the test walker still stalls on some long Town routes. The step tests cover each step.")]
        [Timeout(1500000)]
        public IEnumerator MAIN_E2E_000_SchoolOpeningChainFromNewGameToMyRoom()
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
                    .Append(" E-target=").Append(LastTarget).Append(" | ");
                Debug.Log("[MAIN-E2E] after " + place + " step=" + StepId(chain) + " progress=" + DescribeProgress(chain, 4)
                    + " key=" + PlayerFlowE2EDriver.LastKeyTrace + " locks=" + PlayerFlowE2EDriver.InputLockOwners() + " panelOpened=" + _visitPanelOpened);
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
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, point.transform.position, 0.9f, 900, F + " " + pointName + ": could not walk there.");
            var router = player.GetComponent<PlayerInteractionRouter>();
            router.RefreshPromptNow();
            LastTarget = (router.CurrentInteractable as Component) != null ? ((Component)router.CurrentInteractable).name + "/" + router.CurrentInteractable.GetType().Name : "<none>";
            Assert.IsTrue(router.PromptVisible, F + " " + pointName + ": no prompt there.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            _visitPanelOpened = panel != null && panel.IsOpen;
            if (_visitPanelOpened) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "CloseLocation"));
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
        }

        private static bool _visitPanelOpened;

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
            // Spending the day's last time point ends the day on the spot and shows the day result; a player presses
            // Next Day there instead of walking home.
            StudentDayResultPanel shown = null;
            for (float t = 0f; t < 3f && (shown == null || !shown.IsOpen); t += Time.unscaledDeltaTime)
            {
                shown = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                yield return null;
            }
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>().Progress;
            Debug.Log("[MAIN-E2E] before going home: resultOpen=" + (shown != null && shown.IsOpen) + " dayState=" + student.DayState
                + " remaining=" + student.TimePointsRemaining + " " + PlayerFlowE2EDriver.InputLockOwners() + " " + DescribeTime());
            yield return Capture("before-home-day" + student.CurrentDay + ".png");
            if (shown != null && shown.IsOpen)
            {
                yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(shown.transform, "NextDayButton"));
                yield return null;
                yield return PlayerFlowE2EDriver.DismissDailyEvent();
                yield break;
            }

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

        private IEnumerator PlaceAcceptedFurniture(QuestChainDefinition chain, string onlyStableId = null)
        {
            var step = Log().GetCurrentStep(chain);
            var objective = step.Objectives[0] as Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective;
            Assert.IsNotNull(objective, F + " MyOwnSpace's first objective is not furniture placement.");
            var accepted = new System.Collections.Generic.List<string>();
            foreach (var furniture in objective.AcceptedFurniture)
                if (furniture != null && (onlyStableId == null || furniture.StableId == onlyStableId)) accepted.Add(furniture.StableId);

            var toggle = GameObject.Find(HousePlacementTestDriver.ToggleName);
            Assert.IsNotNull(toggle, F + " House has no furniture button.");
            yield return ClickWithMouse(toggle, F + " furniture button");
            GameObject paletteButton = null;
            for (int page = 0; page < 200 && paletteButton == null; page++)
            {
                paletteButton = FindPaletteButton(accepted);
                _paletteFoundOnPage = page;
                if (paletteButton != null) break;
                var next = PlayerFlowE2EDriver.FindButton(Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>().transform, "FurniturePaletteNextPage");
                Assert.IsTrue(next != null && next.interactable, F + " none of " + string.Join(",", accepted) + " is in the House palette (" + DescribePalette() + ").");
                yield return ClickWithMouse(next.gameObject, F + " palette next page");
            }

            Assert.IsNotNull(paletteButton, F + " none of " + string.Join(",", accepted) + " is in the House palette (" + DescribePalette() + ").");
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

        private static string DescribePalette()
        {
            var text = new System.Text.StringBuilder();
            foreach (var panel in Object.FindObjectsByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var page = PlayerFlowE2EDriver.FindChild(panel.transform, "FurniturePalettePageText");
                text.Append("panel scene=").Append(panel.gameObject.scene.name).Append(" active=").Append(panel.gameObject.activeInHierarchy)
                    .Append(" page='").Append(page != null ? page.GetComponent<Text>()?.text?.Replace('\n', ' ') : "?").Append("' ");
            }
            text.Append("buttons:");
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (button.name.StartsWith("PaletteFurniture_", System.StringComparison.Ordinal)) text.Append(' ').Append(button.name.Substring("PaletteFurniture_".Length));
            return text.ToString();
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

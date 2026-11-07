using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
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
    public sealed class FirstGuideRelationshipMilestoneE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string FirstGuideNpcId = "npc.first-guide";
        private const string RelationshipId = "relationship.first-guide.trust";
        private const string HelpActionId = "town.help.first-guide-errand";
        private const string MilestoneId = "milestone.first-guide-trust";
        private const string ObjectiveId = "milestone.objective.first-guide-trust";
        private const string ScreenshotPath = "Builds/Logs/first-guide-relationship-milestone-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 8f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-first-guide-milestone-e2e", System.Guid.NewGuid().ToString("N"));
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

        // REL-001: Mom lives at home. Walk into the House and talk to her, walk back out and help at the town square, and the
        // trust value rises by the data delta once per action, survives a reload, and a second visit adds exactly one more delta.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator REL_001_AuthoredWorld_TalkToMomAtHomeThenSquareHelpRaisesTrustOncePerAction()
        {
            const string TrustId = "relationship.first-guide.trust";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "REL-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var player = GameObject.Find("Player");
            var student = player.GetComponent<StudentLifeProgressComponent>();
            int trustStart = student.Progress.GetRelationshipValueById(TrustId);

            yield return WaitForMomInScene("Town", false, "REL-001 failed: Mom is still standing in Town.");
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "REL-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, "REL-001 failed: Mom is not at home.");
            var mom = GameObject.Find("TownNpc_npc_first_guide");
            var momNpc = mom.GetComponent<Rootborn.Game.Dialogue.NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "REL-001 failed: Mom");
            yield return CaptureScreenshot("output/creator-mom-20261107/rel-001-mom-at-home.png");
            Assert.IsTrue(momNpc.Session.IsOpen, "REL-001 failed: E next to Mom did not open her dialogue.");
            var dialogue = Object.FindFirstObjectByType<Rootborn.UI.Quests.DialoguePanel>(FindObjectsInactive.Include);
            if (dialogue != null && dialogue.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton"));
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", "REL-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();

            int trustAfterFirst = 0;
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_town_square", 0, "REL-001 failed:");
            trustAfterFirst = student.Progress.GetRelationshipValueById(TrustId);
            Assert.Greater(trustAfterFirst, trustStart, "REL-001 failed: helping at the square did not raise Mom's trust.");
            int delta = trustAfterFirst - trustStart;

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "REL-002 failed: Load did not return to Town.");
            var reloaded = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(trustAfterFirst, reloaded.Progress.GetRelationshipValueById(TrustId), "REL-002 failed: trust did not survive reload exactly once.");
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_town_square", 0, "REL-003 failed:");
            Assert.AreEqual(trustAfterFirst + delta, reloaded.Progress.GetRelationshipValueById(TrustId), "REL-003 failed: a second help must add exactly one more delta.");
        }

        // QUEST-DIALOGUE-E2E-001: the quest Mom offers at home is accepted with a mouse click on her dialogue choice, shows in the
        // Tab journal in the House and after walking out to Town, is saved to the slot's quest log file, and loads back once.
        // The hidden Town quest list panel is gone; the player's quest session carries the quest between scenes.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator QUEST_DIALOGUE_E2E_001_AuthoredHouse_ClickMomsAcceptChoiceThenJournalShowsItAcrossScenesAndReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "QUEST-DIALOGUE-E2E-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "QUEST-DIALOGUE-E2E-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            // Mom offers her quest from the second day: end day one at the home board and press Next Day.
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int firstDay = student.Progress.CurrentDay;
            yield return EndDayAtHomeBoard(keyboard, "QUEST-DIALOGUE-E2E-001 failed:");
            Assert.AreEqual(firstDay + 1, student.Progress.CurrentDay, "QUEST-DIALOGUE-E2E-001 failed: Next Day did not start day two.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, "QUEST-DIALOGUE-E2E-001 failed: Mom is not at home.");
            Assert.IsNull(GameObject.Find("QuestLogPanel"), "QUEST-DIALOGUE-E2E-001 failed: the removed hidden quest panel came back.");

            var mom = GameObject.Find("TownNpc_npc_first_guide");
            var momNpc = mom.GetComponent<NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "QUEST-DIALOGUE-E2E-001 failed: Mom");
            Assert.IsTrue(momNpc.Session.IsOpen, "QUEST-DIALOGUE-E2E-001 failed: E next to Mom did not open her dialogue.");
            int acceptIndex = FindQuestChoice(momNpc.Session.Current, DialogueQuestAction.AcceptQuest);
            Assert.GreaterOrEqual(acceptIndex, 0, "QUEST-DIALOGUE-E2E-001 failed: Mom's dialogue offers no quest to accept.");
            var quest = momNpc.Session.Current.Choices[acceptIndex].Quest;
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var quests), "QUEST-DIALOGUE-E2E-001 failed: no quest session for the player.");
            Assert.AreEqual(QuestState.NotStarted, quests.QuestLog.GetState(quest), "QUEST-DIALOGUE-E2E-001 failed: a new game must start with Mom's quest not accepted.");

            var dialogue = FindInActiveScene<DialoguePanel>();
            Assert.IsTrue(dialogue != null && dialogue.IsOpen, "QUEST-DIALOGUE-E2E-001 failed: Mom's dialogue panel is not on screen.");
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(dialogue.transform, "ChoiceButton_" + acceptIndex).gameObject, "QUEST-DIALOGUE-E2E-001 failed: accept choice");
            Assert.AreEqual(QuestState.Active, quests.QuestLog.GetState(quest), "QUEST-DIALOGUE-E2E-001 failed: clicking the accept choice did not accept the quest.");
            Assert.AreEqual(QuestState.Active, SavedQuestState(quest.Id), "QUEST-DIALOGUE-E2E-001 failed: the accepted quest was not saved right away.");
            if (dialogue.IsOpen) yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton").gameObject, "QUEST-DIALOGUE-E2E-001 failed: dialogue close");

            yield return AssertJournalShowsQuest(keyboard, quest, "QUEST-DIALOGUE-E2E-001 failed (House):");
            yield return CaptureScreenshot("output/quest-session-20261107/quest-dialogue-e2e-001-house-journal.png");
            yield return PressTabUntilJournal(keyboard, false, "QUEST-DIALOGUE-E2E-001 failed (House):");

            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", "QUEST-DIALOGUE-E2E-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return AssertJournalShowsQuest(keyboard, quest, "QUEST-DIALOGUE-E2E-002 failed (Town after House):");
            yield return CaptureScreenshot("output/quest-session-20261107/quest-dialogue-e2e-002-town-journal.png");
            yield return PressTabUntilJournal(keyboard, false, "QUEST-DIALOGUE-E2E-002 failed (Town):");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "QUEST-DIALOGUE-E2E-003 failed: Load did not return to Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var reloaded), "QUEST-DIALOGUE-E2E-003 failed: no quest session after reload.");
            Assert.AreNotSame(quests, reloaded, "QUEST-DIALOGUE-E2E-003 failed: reload must read the save, not keep the old session.");
            Assert.AreEqual(QuestState.Active, reloaded.QuestLog.GetState(quest), "QUEST-DIALOGUE-E2E-003 failed: the accepted quest did not load back.");
            yield return AssertJournalShowsQuest(keyboard, quest, "QUEST-DIALOGUE-E2E-003 failed (Town after reload):");
            yield return PressTabUntilJournal(keyboard, false, "QUEST-DIALOGUE-E2E-003 failed (Town):");

            // Back at home the accept choice is no longer offered: an accepted quest cannot be accepted twice.
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "QUEST-DIALOGUE-E2E-004 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, "QUEST-DIALOGUE-E2E-004 failed: Mom is not at home.");
            mom = GameObject.Find("TownNpc_npc_first_guide");
            momNpc = mom.GetComponent<NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "QUEST-DIALOGUE-E2E-004 failed: Mom");
            dialogue = FindInActiveScene<DialoguePanel>();
            Assert.IsTrue(dialogue != null && dialogue.IsOpen, "QUEST-DIALOGUE-E2E-004 failed: Mom's dialogue did not open after reload.");
            int againIndex = FindQuestChoice(momNpc.Session.Current, DialogueQuestAction.AcceptQuest, quest);
            Assert.IsTrue(againIndex < 0 || PlayerFlowE2EDriver.FindChild(dialogue.transform, "ChoiceButton_" + againIndex) == null,
                "QUEST-DIALOGUE-E2E-004 failed: an accepted quest is still offered for accepting.");
            Assert.AreEqual(QuestState.Active, reloaded.QuestLog.GetState(quest));
        }

        // QUEST-WOOD-E2E-001: on day two Mom's Gather Wood quest is accepted with a mouse click, the player walks out to the
        // trees in Town with WASD and gathers with E until the quest completes and wood lands in the bag, then claims the
        // reward with the mouse on the Tab journal's "Claim reward" button. After a reload the reward is not paid again.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator QUEST_WOOD_E2E_001_AcceptFromMomGatherTownTreesByInputClaimInJournalOnceAcrossReload()
        {
            const string F = "QUEST-WOOD-E2E-001 failed:";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartDayTwoAtHome(keyboard, F);
            yield return AcceptMomQuest(keyboard, "quest.gatherWood", F);
            var quest = AcceptFromMomResult;
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var quests), F + " no quest session.");

            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", F);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var points = GameObject.Find("TownResourcePoints");
            Assert.IsNotNull(points, F + " Town has no authored resource points.");
            var tree = points.transform.GetChild(0).GetComponent<Rootborn.Game.Resources.ResourceNode>();
            Assert.IsNotNull(tree, F + " the first resource point is not a resource node.");
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, tree.transform.position + new Vector3(0f, -0.9f, 0f), 0.4f, 900, F + " could not walk to the tree.");
            yield return CaptureScreenshot("output/quest-fixes-20261107/quest-wood-e2e-001-town-trees.png");

            var inventory = player.GetComponent<PlayerInventory>().Inventory;
            int woodBefore = CountById(inventory, "Wood");
            for (int i = 0; i < 5 && quests.QuestLog.GetState(quest) == QuestState.Active; i++) yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            Assert.AreEqual(QuestState.Completed, quests.QuestLog.GetState(quest), F + " pressing E at the tree did not complete the gather objective.");
            Assert.AreEqual(QuestState.Completed, SavedQuestState(quest.Id), F + " the completed objective was not saved.");
            for (int i = 0; i < 40 && !tree.IsBroken; i++) yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            Assert.IsTrue(tree.IsBroken, F + " the tree did not fall after repeated E presses with bare hands.");
            Assert.Greater(CountById(inventory, "Wood"), woodBefore, F + " the felled tree dropped no wood into the bag.");

            var reward = FirstItemReward(quest);
            int rewardBefore = inventory.CountOf(reward.Item);
            yield return ClaimFromJournal(keyboard, quest, "output/quest-fixes-20261107/quest-wood-e2e-001-journal-claim-button.png", F);
            Assert.AreEqual(QuestState.RewardClaimed, quests.QuestLog.GetState(quest), F + " the journal claim did not claim the quest.");
            Assert.AreEqual(QuestState.RewardClaimed, SavedQuestState(quest.Id), F + " the claim was not saved right away.");
            Assert.AreEqual(rewardBefore + reward.Count, inventory.CountOf(reward.Item), F + " the reward item was not paid exactly once.");
            int rewardAfterClaim = inventory.CountOf(reward.Item);

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "QUEST-WOOD-E2E-002 failed: Load did not return to Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var reloadedPlayer = GameObject.Find("Player");
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(reloadedPlayer, out var reloaded), "QUEST-WOOD-E2E-002 failed: no quest session after reload.");
            Assert.AreEqual(QuestState.RewardClaimed, reloaded.QuestLog.GetState(quest), "QUEST-WOOD-E2E-002 failed: the claim did not load back.");
            yield return null;
            Assert.AreEqual(rewardAfterClaim, reloadedPlayer.GetComponent<PlayerInventory>().Inventory.CountOf(reward.Item), "QUEST-WOOD-E2E-002 failed: the reward changed across reload.");
            yield return AssertJournalHasNoClaimButton(keyboard, quest, "QUEST-WOOD-E2E-002 failed:");
        }

        // QUEST-DECORATE-E2E-001: on day two Mom's house decorating quest is accepted with a mouse click, three pieces of
        // furniture are placed in the House with the mouse (furniture panel button, palette item, room cell), the reward is
        // claimed on the journal detail button by mouse, and after a reload it stays claimed and its effect counted once.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator QUEST_DECORATE_E2E_001_AcceptFromMomPlaceThreeFurnitureByMouseClaimInJournalOnceAcrossReload()
        {
            const string F = "QUEST-DECORATE-E2E-001 failed:";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartDayTwoAtHome(keyboard, F);
            yield return AcceptMomQuest(keyboard, "quest.tile-placement.interior", F);
            var quest = AcceptFromMomResult;
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var quests), F + " no quest session.");
            var objective = (Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective)quest.Objectives[0];
            var effect = (Rootborn.Game.Quests.Effects.TraitDeltaCompletionEffect)System.Array.Find(quest.CompletionEffects, e => e is Rootborn.Game.Quests.Effects.TraitDeltaCompletionEffect);
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int traitBefore = student.Progress.GetTraitValue(effect.Trait);

            var toggle = GameObject.Find(HousePlacementTestDriver.ToggleName);
            Assert.IsNotNull(toggle, F + " House has no furniture button.");
            yield return ClickWithMouse(toggle, F + " furniture button");
            var accepted = new System.Collections.Generic.List<string>();
            foreach (var furniture in objective.AcceptedFurniture) if (furniture != null) accepted.Add(furniture.StableId);
            var used = new System.Collections.Generic.HashSet<string>();
            for (int placed = 0; placed < 3; placed++)
            {
                int before = quests.QuestLog.GetObjectiveCount(quest, 0);
                GameObject paletteButton = null;
                for (int page = 0; page < 40 && paletteButton == null; page++)
                {
                    paletteButton = FindPaletteButton(accepted, used) ?? FindPaletteButton(accepted, null);
                    if (paletteButton != null) break;
                    var next = PlayerFlowE2EDriver.FindButton(Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>().transform, "FurniturePaletteNextPage");
                    Assert.IsTrue(next != null && next.interactable, F + " none of Mom's furniture is in the House palette.");
                    yield return ClickWithMouse(next.gameObject, F + " palette next page");
                }

                Assert.IsNotNull(paletteButton, F + " none of Mom's furniture is in the House palette.");
                used.Add(paletteButton.name.Substring("PaletteFurniture_".Length));
                yield return ClickWithMouse(paletteButton, F + " palette item " + paletteButton.name);
                var overlay = Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorPlacementPreviewOverlay>();
                for (int i = 0; i < 60 && (overlay == null || overlay.ValidCellCount == 0); i++) { yield return null; overlay = Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorPlacementPreviewOverlay>(); }
                Assert.IsTrue(overlay != null && overlay.ValidCellCount > 0, F + " choosing " + paletteButton.name + " showed no free cells.");
                Assert.IsTrue(TryFindFreeRoomCell(out var screen), F + " no free room cell is visible outside the UI.");
                yield return PressMouseInWorld(screen);
                for (int i = 0; i < 30 && quests.QuestLog.GetObjectiveCount(quest, 0) == before; i++) yield return null;
                Assert.AreEqual(before + 1, quests.QuestLog.GetObjectiveCount(quest, 0), F + " placing " + paletteButton.name + " did not count once.");
            }

            Assert.AreEqual(QuestState.Completed, quests.QuestLog.GetState(quest), F + " three placed pieces did not complete the quest.");
            yield return CaptureScreenshot("output/quest-fixes-20261107/quest-decorate-e2e-001-house-furniture.png");
            yield return ClickWithMouse(GameObject.Find(HousePlacementTestDriver.ToggleName), F + " closing the furniture panel");
            yield return ClaimFromJournal(keyboard, quest, "output/quest-fixes-20261107/quest-decorate-e2e-001-journal-claim-button.png", F);
            Assert.AreEqual(QuestState.RewardClaimed, quests.QuestLog.GetState(quest), F + " the journal claim did not claim the quest.");
            Assert.AreEqual(QuestState.RewardClaimed, SavedQuestState(quest.Id), F + " the claim was not saved right away.");
            Assert.AreEqual(traitBefore + effect.Delta, student.Progress.GetTraitValue(effect.Trait), F + " the quest effect did not apply exactly once.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "House", "QUEST-DECORATE-E2E-002 failed: Load did not return to the House.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var reloadedPlayer = GameObject.Find("Player");
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(reloadedPlayer, out var reloaded), "QUEST-DECORATE-E2E-002 failed: no quest session after reload.");
            Assert.AreEqual(QuestState.RewardClaimed, reloaded.QuestLog.GetState(quest), "QUEST-DECORATE-E2E-002 failed: the claim did not load back.");
            Assert.AreEqual(traitBefore + effect.Delta, reloadedPlayer.GetComponent<StudentLifeProgressComponent>().Progress.GetTraitValue(effect.Trait), "QUEST-DECORATE-E2E-002 failed: the effect changed across reload.");
            yield return AssertJournalHasNoClaimButton(keyboard, quest, "QUEST-DECORATE-E2E-002 failed:");
        }

        private static QuestDefinition AcceptFromMomResult;

        // New game, into the House, end day one at the home board, and wait for Mom on day two.
        private static IEnumerator StartDayTwoAtHome(Keyboard keyboard, string failure)
        {
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", failure + " New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", failure);
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int firstDay = student.Progress.CurrentDay;
            yield return EndDayAtHomeBoard(keyboard, failure);
            Assert.AreEqual(firstDay + 1, student.Progress.CurrentDay, failure + " Next Day did not start day two.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, failure + " Mom is not at home.");
        }

        // Talks to Mom with E and clicks the accept choice for the given quest with the mouse.
        private IEnumerator AcceptMomQuest(Keyboard keyboard, string questId, string failure)
        {
            var mom = GameObject.Find("TownNpc_npc_first_guide");
            var momNpc = mom.GetComponent<NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, failure + " Mom");
            Assert.IsTrue(momNpc.Session.IsOpen, failure + " E next to Mom did not open her dialogue.");
            int acceptIndex = -1;
            var choices = momNpc.Session.Current.Choices;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] != null && choices[i].QuestAction == DialogueQuestAction.AcceptQuest && choices[i].Quest != null && choices[i].Quest.Id == questId) acceptIndex = i;
            Assert.GreaterOrEqual(acceptIndex, 0, failure + " Mom does not offer " + questId + ".");
            var quest = choices[acceptIndex].Quest;
            var dialogue = FindInActiveScene<DialoguePanel>();
            Assert.IsTrue(dialogue != null && dialogue.IsOpen, failure + " Mom's dialogue panel is not on screen.");
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(dialogue.transform, "ChoiceButton_" + acceptIndex).gameObject, failure + " accept choice");
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var quests), failure + " no quest session.");
            Assert.AreEqual(QuestState.Active, quests.QuestLog.GetState(quest), failure + " clicking the accept choice did not accept " + questId + ".");
            Assert.AreEqual(QuestState.Active, SavedQuestState(quest.Id), failure + " the accepted quest was not saved right away.");
            if (dialogue.IsOpen) yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton").gameObject, failure + " dialogue close");
            AcceptFromMomResult = quest;
        }

        // Tab, Quests category and the quest row by mouse; the detail's claim button is captured on screen and clicked by mouse.
        private static IEnumerator ClaimFromJournal(Keyboard keyboard, QuestDefinition quest, string screenshotPath, string failure)
        {
            yield return PressTabUntilJournal(keyboard, true, failure);
            var journal = FindInActiveScene<ObjectiveJournalPanel>();
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Quests").gameObject, failure + " Quests category");
            var row = ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, GameText.Get(quest.DisplayNameKey), failure);
            yield return ClickWithMouse(row.gameObject, failure + " quest row");
            var claim = journal.DetailActionButton;
            Assert.IsNotNull(claim, failure + " the completed quest shows no claim button in the journal detail.\n" + journal.VisibleText);
            StringAssert.Contains(GameText.Localized("ui.journal.quest.claim", "Claim reward"), journal.VisibleText, failure + " the claim button has no label.");
            yield return CaptureScreenshot(screenshotPath);
            yield return ClickWithMouse(claim, failure + " claim button");
            StringAssert.Contains(GameText.Localized("ui.journal.quest.claimed", "Reward received."), journal.VisibleText, failure + " the journal did not confirm the claim.");
            Assert.IsNull(journal.DetailActionButton, failure + " the claim button is still offered after claiming.");
            yield return PressTabUntilJournal(keyboard, false, failure);
        }

        private static IEnumerator AssertJournalHasNoClaimButton(Keyboard keyboard, QuestDefinition quest, string failure)
        {
            yield return PressTabUntilJournal(keyboard, true, failure);
            var journal = FindInActiveScene<ObjectiveJournalPanel>();
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Quests").gameObject, failure + " Quests category");
            var row = ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, GameText.Get(quest.DisplayNameKey), failure);
            yield return ClickWithMouse(row.gameObject, failure + " quest row");
            Assert.IsNull(journal.DetailActionButton, failure + " a claimed quest offers its reward again after reload.");
            yield return PressTabUntilJournal(keyboard, false, failure);
        }

        private static GameObject FindPaletteButton(System.Collections.Generic.List<string> accepted, System.Collections.Generic.HashSet<string> exclude)
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!button.name.StartsWith("PaletteFurniture_", System.StringComparison.Ordinal)) continue;
                string id = button.name.Substring("PaletteFurniture_".Length);
                if (accepted.Contains(id) && (exclude == null || !exclude.Contains(id))) return button.gameObject;
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

        // A real mouse moves over the room cell, presses for a few frames and releases, as a player clicks.
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

        private static Rootborn.Game.Quests.Rewards.ItemQuestReward FirstItemReward(QuestDefinition quest)
        {
            foreach (var reward in quest.Rewards) if (reward is Rootborn.Game.Quests.Rewards.ItemQuestReward item && item.Item != null) return item;
            Assert.Fail(quest.Id + " has no item reward.");
            return null;
        }

        private static int CountById(Rootborn.Game.Common.Inventory inventory, string itemId)
        {
            int count = 0;
            foreach (var slot in inventory.Slots) if (slot.Item != null && slot.Item.Id == itemId) count += slot.Count;
            return count;
        }

        private static IEnumerator EndDayAtHomeBoard(Keyboard keyboard, string failure)
        {
            var board = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(board, failure + " House has no StudentDayEndBoard.");
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, board.transform.position, 0.9f, 900, failure + " could not walk to the day-end board.");
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

            Assert.IsTrue(panel != null && panel.IsOpen, failure + " the day result panel did not open.");
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(panel.transform, "NextDayButton").gameObject, failure + " Next Day");
            yield return null;
        }

        private static int FindQuestChoice(DialogueDefinition dialogue, DialogueQuestAction action, QuestDefinition quest = null)
        {
            var choices = dialogue != null ? dialogue.Choices : null;
            if (choices == null) return -1;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] != null && choices[i].QuestAction == action && choices[i].Quest != null && (quest == null || choices[i].Quest.Id == quest.Id)) return i;
            return -1;
        }

        private QuestState SavedQuestState(string questId)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, PlayerQuestRuntime.LogFileName(PlayerIdentity.DefaultPlayerId));
            Assert.IsTrue(File.Exists(path), "Quest log file missing: " + path);
            var data = JsonUtility.FromJson<QuestLogSaveData>(File.ReadAllText(path));
            foreach (var entry in data.Quests) if (entry.QuestId == questId) return entry.State;
            Assert.Fail("Quest " + questId + " missing from " + path);
            return QuestState.NotStarted;
        }

        // Tab opens the journal; the Quests category and the quest's row are clicked with the mouse.
        private static IEnumerator AssertJournalShowsQuest(Keyboard keyboard, QuestDefinition quest, string failure)
        {
            yield return PressTabUntilJournal(keyboard, true, failure);
            var journal = FindInActiveScene<ObjectiveJournalPanel>();
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Quests").gameObject, failure + " Quests category");
            string title = GameText.Get(quest.DisplayNameKey);
            var row = ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, title, failure);
            yield return ClickWithMouse(row.gameObject, failure + " quest row");
            StringAssert.Contains(title, journal.VisibleText, failure + " the journal detail does not show the quest.");
            StringAssert.Contains(GameText.Localized("ui.journal.state.Active", "Active"), journal.VisibleText, failure + " the journal does not show the quest as in progress.");
        }

        private static IEnumerator PressTabUntilJournal(Keyboard keyboard, bool visible, string failure)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var journal = FindInActiveScene<ObjectiveJournalPanel>();
                if (journal != null && journal.IsVisible == visible) yield break;
                yield return PlayerFlowE2EDriver.PressTab(keyboard);
                yield return null;
            }

            var last = FindInActiveScene<ObjectiveJournalPanel>();
            Assert.IsTrue(last != null && last.IsVisible == visible, failure + " Tab did not " + (visible ? "open" : "close") + " the journal. Locks: " + PlayerFlowE2EDriver.InputLockOwners());
        }

        private static T FindInActiveScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        // A real mouse device moves to a point the EventSystem raycast hits on the target and presses the left button.
        private static IEnumerator ClickWithMouse(GameObject target, string failure)
        {
            Assert.IsNotNull(target, failure + " target missing.");
            Assert.IsTrue(target.activeInHierarchy, failure + " target is not visible.");
            var eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem, failure + " no EventSystem.");
            // This fixture's input guard swaps in a module that ignores devices; a real click needs the module the game uses.
            if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                var passive = eventSystem.GetComponent<Rootborn.Game.Common.PassiveInputModule>();
                if (passive != null) Object.DestroyImmediate(passive);
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                yield return null;
                yield return null;
            }
            Assert.IsNotNull(eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(), failure + " pointer clicks need InputSystemUIInputModule.");
            Assert.IsTrue(TryFindRaycastPoint(target, eventSystem, out var point), failure + " nothing on screen lets a click reach " + target.name + ".");
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
        }

        private static bool TryFindRaycastPoint(GameObject target, EventSystem eventSystem, out Vector2 point)
        {
            var results = new System.Collections.Generic.List<RaycastResult>();
            var rect = target.GetComponent<RectTransform>();
            if (rect != null)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                for (int y = 1; y <= 3; y++)
                    for (int x = 1; x <= 3; x++)
                    {
                        var world = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], x / 4f), Vector3.Lerp(corners[1], corners[2], x / 4f), y / 4f);
                        var candidate = RectTransformUtility.WorldToScreenPoint(null, world);
                        results.Clear();
                        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = candidate }, results);
                        if (results.Count > 0 && (results[0].gameObject == target || results[0].gameObject.transform.IsChildOf(target.transform)))
                        {
                            point = candidate;
                            return true;
                        }
                    }
            }

            point = Vector2.zero;
            return false;
        }

        // The schedule places Mom after the scene's data finishes loading, so wait for her to show up (or disappear).
        private static IEnumerator WaitForMomInScene(string sceneName, bool present, string failure)
        {
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                var mom = GameObject.Find("TownNpc_npc_first_guide");
                bool here = mom != null && mom.scene.name == sceneName;
                if (here == present) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator CaptureScreenshot(string relativePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(relativePath));
            if (File.Exists(relativePath)) File.Delete(relativePath);
            ScreenCapture.CaptureScreenshot(relativePath);
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                if (File.Exists(relativePath) && new FileInfo(relativePath).Length > 0L) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("NPC-REL-E2E-001 failed: Game View screenshot evidence was not written: " + relativePath);
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

        private readonly struct FirstGuideRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly NpcInteractor Npc;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public FirstGuideRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, NpcInteractor npc, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Npc = npc;
                Help = help;
                DayEnd = dayEnd;
                Journal = journal;
                TrackedHud = trackedHud;
            }

            public bool IsValid => Player != null && Router != null && Student != null && Npc != null && Help != null && DayEnd != null && Journal != null && TrackedHud != null;
        }
    }
}

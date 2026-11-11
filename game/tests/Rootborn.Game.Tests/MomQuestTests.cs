// 원래 파일: Assets/Tests/PlayMode/EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs
//            (QUEST_DIALOGUE_E2E_001, QUEST_DECORATE_E2E_001, QUEST_WOOD_E2E_001의 받기 부분)
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 2일째 엄마의 대화에서 퀘스트를 받는다. 받은 퀘스트는 바로 저장되고 다시 읽어도 받은 상태이며 대화가 다시 권하지 않는다.
    /// 집 꾸미기는 받을 수 있는 가구 세 개를 실제 집 맵 방에 놓으면 끝나고, 보상은 한 번만 받는다. 나무 모으기는 채집 명령이 없어 받기까지만 본다.
    /// 자동 테스트다.
    /// </summary>
    public sealed class MomQuestTests
    {
        private const string Mom = "npc.first-guide";
        private const string Decorate = "quest.tile-placement.interior";
        private const string Wood = "quest.gatherWood";

        private static GameSession DayTwo(TempSaves saves)
        {
            var game = TestWorld.Start(saves.Store);
            game.TalkTo(Mom);
            game.CloseDialogue();
            Assert.That(game.EndDay().Ended, Is.True);
            game = TestWorld.Start(saves.Store);
            Assert.That(game.StartNextDay(), Is.True);
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(2));
            return game;
        }

        private static int AcceptIndex(DialogueDefinition dialogue, string questId)
        {
            if (dialogue == null) return -1;
            for (int i = 0; i < dialogue.Choices.Count; i++)
                if (dialogue.Choices[i].QuestAction == DialogueQuestAction.AcceptQuest && dialogue.Choices[i].Quest?.Id == questId) return i;
            return -1;
        }

        private static GameSession AcceptFromMom(TempSaves saves, string questId)
        {
            var game = DayTwo(saves);
            Assert.That(game.QuestLog.GetState(questId), Is.EqualTo(QuestStage.NotStarted), "새 게임은 받지 않은 상태");
            var dialogue = game.TalkTo(Mom);
            int index = AcceptIndex(dialogue, questId);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "엄마의 대화(" + dialogue?.Id + ")가 " + questId + "를 권하지 않는다");
            Assert.That(game.ChooseDialogueOption(index), Is.True);
            Assert.That(game.QuestLog.GetState(questId), Is.EqualTo(QuestStage.Active));
            game.CloseDialogue();
            return TestWorld.Start(saves.Store);
        }

        [TestCase(Decorate)]
        [TestCase(Wood)]
        public void QuestDialogue001_AcceptedFromMomOnDayTwo_IsSavedRightAway_AndNotOfferedAgain(string questId)
        {
            using var saves = new TempSaves();
            var game = AcceptFromMom(saves, questId);
            Assert.That(game.QuestLog.GetState(questId), Is.EqualTo(QuestStage.Active), "받은 퀘스트가 저장된다");

            var dialogue = game.TalkTo(Mom);
            int again = AcceptIndex(dialogue, questId);
            Assert.That(again < 0 || !game.ChooseDialogueOption(again), Is.True, "받은 퀘스트는 다시 받을 수 없다");
            Assert.That(TestWorld.Start(saves.Store).QuestLog.GetState(questId), Is.EqualTo(QuestStage.Active));
        }

        [Test]
        public void QuestDecorate001_ThreeAcceptedPiecesAtHome_CompleteTheQuest_AndTheRewardIsClaimedOnce()
        {
            using var saves = new TempSaves();
            var game = AcceptFromMom(saves, Decorate);
            var quest = game.Definitions.Get<QuestDefinition>(Decorate);
            var objective = quest.Objectives.OfType<FurniturePlacedQuestObjective>().Single();
            var effect = quest.CompletionEffects.OfType<TraitDeltaCompletionEffect>().Single();
            var pieces = game.AvailableFurniture().Where(f => objective.AcceptedFurnitureIds.Contains(f.StableId)).Select(f => f.StableId).ToList();
            Assert.That(pieces, Is.Not.Empty, "받을 수 있는 가구 중 목록에 보이는 것이 있어야 한다");
            int traitBefore = game.Student.GetTraitValue(effect.TraitId);

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            for (int i = 0; i < objective.RequiredCount; i++)
            {
                int before = game.QuestLog.GetObjectiveCount(quest, 0);
                var piece = pieces[i % pieces.Count];
                Assert.That(game.PlaceFurniture(piece, HouseRoom.FirstFreeAnchor(game, piece), FurniturePlacementDirection.North), Is.True, piece);
                Assert.That(game.QuestLog.GetObjectiveCount(quest, 0), Is.EqualTo(before + 1), piece + "를 놓으면 한 번 센다");
                game = TestWorld.Start(saves.Store);
                game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            }

            Assert.That(game.QuestLog.GetState(quest), Is.EqualTo(QuestStage.Completed));
            Assert.That(game.ClaimQuestReward(Decorate), Is.True);
            Assert.That(game.QuestLog.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(game.Student.GetTraitValue(effect.TraitId), Is.EqualTo(traitBefore + effect.Delta));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.QuestLog.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(again.ClaimQuestReward(Decorate), Is.False, "보상은 한 번만");
            Assert.That(again.Student.GetTraitValue(effect.TraitId), Is.EqualTo(traitBefore + effect.Delta));
        }
    }
}

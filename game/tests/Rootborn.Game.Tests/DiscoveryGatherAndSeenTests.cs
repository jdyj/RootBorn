// 원래 흐름: Assets/Scripts/Game/StudentLife/DiscoveryPointInteractor.cs(발견 → 저장 → 도감 해금 평가),
//            Assets/Scripts/Game/Player/GatherInteractor.cs(자원 치기마다 퀘스트 로그에 채집 이벤트, 부서지면 나온 물건),
//            Assets/Scripts/UI/Encyclopedia/EncyclopediaPanel(항목을 열면 새 표시 지우고 저장).
using NUnit.Framework;
using Rootborn.Core.Gathering;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>발견 지점, 도감 "봤음" 표시, 자원 채집 명령. 자동 테스트다.</summary>
    public sealed class DiscoveryGatherAndSeenTests
    {
        private const string Discovery = "discovery.clocktower-notice";
        private const string Entry = "ency.location.first-discovery";

        [Test]
        public void Discover_AppliesOnce_IsSaved_AndOpensTheEncyclopediaEntryItUnlocks()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var before = game.Student.ToSaveData();
            Assert.That(game.Encyclopedia.IsUnlocked(Entry), Is.False);

            var result = game.Discover(Discovery);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(result.OutcomeLogIds, Is.Not.Empty, "발견 결과(특성 변화)가 기록된다");
            Assert.That(game.Student.ToSaveData().ActivityLogIds, Does.Contain(Discovery));
            Assert.That(game.Encyclopedia.IsUnlocked(Entry), Is.True, "Unity 판처럼 발견 뒤 도감 해금을 평가한다");
            Assert.That(game.Encyclopedia.IsNew(Entry), Is.True);
            Assert.That(before.ActivityLogIds, Does.Not.Contain(Discovery));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Encyclopedia.IsUnlocked(Entry), Is.True);
            Assert.That(again.Discover(Discovery).Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest), "발견은 한 번만");
        }

        [Test]
        public void Discover_UnknownId_IsInvalidAndSavesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int changes = 0;
            game.Changed += _ => changes++;
            Assert.That(game.Discover("discovery.none").Kind, Is.EqualTo(LifeActivityResultKind.InvalidRequest));
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void MarkEncyclopediaSeen_ClearsTheNewMark_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.MarkEncyclopediaSeen(Entry), Is.False, "아직 열리지 않은 항목");
            game.Discover(Discovery);

            Assert.That(game.MarkEncyclopediaSeen(Entry), Is.True);
            Assert.That(game.Encyclopedia.IsNew(Entry), Is.False);
            Assert.That(game.MarkEncyclopediaSeen(Entry), Is.False, "이미 본 항목");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Encyclopedia.IsUnlocked(Entry), Is.True);
            Assert.That(again.Encyclopedia.IsNew(Entry), Is.False, "본 표시가 저장된다");
        }

        [Test]
        public void EvaluateEncyclopediaUnlocks_OpensEntriesWhoseConditionsAlreadyHold()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.EvaluateEncyclopediaUnlocks(), Is.EqualTo(0), "새 슬롯은 열 항목이 없다");
            Assert.That(game.EvaluateEncyclopediaUnlocks(), Is.EqualTo(0));
        }

        [Test]
        public void GatherWoodQuest_RunsToTheEnd_ByHittingATree()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TalkTo("npc.first-guide");
            Assert.That(game.ChooseDialogueOption(0), Is.True, "Choice_AcceptGatherWood");
            Assert.That(game.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.Active));

            var tree = new ResourceNodeState();
            Assert.That(game.GatherResource("Tree", tree), Is.EqualTo(GatherHitResult.Hit));
            Assert.That(game.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.Completed), "치기마다 채집 이벤트를 낸다(Unity와 같음)");

            var wood = game.Item("Wood");
            int hits = 1;
            GatherHitResult last = GatherHitResult.Hit;
            while (last == GatherHitResult.Hit && hits < 100) { last = game.GatherResource("Tree", tree); hits++; }
            Assert.That(last, Is.EqualTo(GatherHitResult.Broken));
            Assert.That(hits, Is.EqualTo(17), "맨손 0.3씩 5까지");
            Assert.That(game.Inventory.CountOf(wood), Is.InRange(1, 3));
            Assert.That(game.GatherResource("Tree", tree), Is.EqualTo(GatherHitResult.Ignored), "부서진 자원은 무시");

            int stone = game.Inventory.CountOf(game.Item("Stone"));
            Assert.That(game.ClaimQuestReward("quest.gatherWood"), Is.True);
            Assert.That(game.Inventory.CountOf(game.Item("Stone")), Is.EqualTo(stone + 1));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(again.Inventory.CountOf(again.Item("Wood")), Is.EqualTo(game.Inventory.CountOf(wood)), "나온 물건이 저장된다");
        }

        [Test]
        public void GatherResource_UnknownNodeOrMissingState_ChangesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int changes = 0;
            game.Changed += _ => changes++;
            Assert.That(game.GatherResource("NoSuchNode", new ResourceNodeState()), Is.EqualTo(GatherHitResult.Ignored));
            Assert.That(game.GatherResource("Tree", null), Is.EqualTo(GatherHitResult.Ignored));
            Assert.That(changes, Is.EqualTo(0));
        }
    }
}
